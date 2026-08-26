using System;
using System.Collections.Generic;
using System.Linq;
using NEXA.Adapters.Output;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Grab;

/// <summary>
/// Application adapter orchestrating real OS window grabbing, delta relocation, two-hand pinch resizing, and camera viewport feedback rendering.
/// <para>
/// <b>What it is:</b> The controller responsible for moving and resizing physical Windows desktop applications via hand gestures.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description>Initializes <see cref="WindowGrabDetector"/> and <see cref="WindowResizeDetector"/> with monitor resolution from <see cref="IInputSink"/>.</description></item>
/// <item><description>Brings the grabbed window to the foreground via <see cref="IInputSink.BringWindowToForeground"/> upon latching.</description></item>
/// <item><description>Invokes <see cref="IInputSink.MoveWindow"/> for position updates and <see cref="IInputSink.ResizeWindow"/> for pinch-aperture scaling.</description></item>
/// <item><description>Raises <see cref="OnFistReleased"/> when a grabbed window is released to trigger downstream action windows.</description></item>
/// <item><description>Renders back-projected corner brackets and AR pinch-to-scale calipers directly on the camera viewport.</description></item>
/// </list>
/// </para>
/// </summary>
public class WindowGrabController
{
    /// <summary>
    /// The input sink used to query, move, and resize physical OS windows.
    /// </summary>
    private readonly IInputSink _inputSink;

    /// <summary>
    /// Desktop primary monitor horizontal resolution in pixels.
    /// </summary>
    private readonly int _screenWidth;

    /// <summary>
    /// Desktop primary monitor vertical resolution in pixels.
    /// </summary>
    private readonly int _screenHeight;

    /// <summary>
    /// Track the HWND that was activated to ensure BringWindowToForeground is called once per grab cycle.
    /// </summary>
    private IntPtr _lastForegroundHwnd = IntPtr.Zero;

    /// <summary>
    /// Flag tracking whether the grab was active in the previous frame to detect release transitions.
    /// </summary>
    private bool _wasGrabbedLastFrame = false;

    /// <summary>
    /// Event fired immediately when a fist-grab gesture is released.
    /// </summary>
    public event Action? OnFistReleased;

    /// <summary>
    /// The core domain detector handling hold timing, coordinate mapping, and delta dragging.
    /// </summary>
    public WindowGrabDetector Detector { get; }

    /// <summary>
    /// The secondary domain detector handling continuous two-hand pinch aperture scaling.
    /// </summary>
    public WindowResizeDetector ResizeDetector { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether window grabbing and resizing are enabled.
    /// </summary>
    public bool Enabled
    {
        get => Detector.Enabled;
        set => Detector.Enabled = value;
    }

    /// <summary>
    /// Gets the internal window grab state machine.
    /// </summary>
    public WindowGrabState State => Detector.State;

    /// <summary>
    /// Gets the internal window resize state machine.
    /// </summary>
    public WindowResizeState ResizeState => ResizeDetector.State;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowGrabController"/> class, querying display resolution from the input sink.
    /// </summary>
    /// <param name="inputSink">The output adapter for OS inputs (defaults to <see cref="Win32InputSink"/> if null).</param>
    public WindowGrabController(IInputSink? inputSink = null)
    {
        _inputSink = inputSink ?? new Win32InputSink();
        (_screenWidth, _screenHeight) = _inputSink.GetScreenResolution();
        Detector = new WindowGrabDetector(_screenWidth, _screenHeight);
    }

    /// <summary>
    /// Single-hand overload: Evaluates hand tracking data for moving the grabbed desktop window.
    /// </summary>
    public void Update(TrackedHand? hand, int frameWidth, int frameHeight)
    {
        List<TrackedHand> list = hand != null ? [hand] : [];
        Update(list, frameWidth, frameHeight);
    }

    /// <summary>
    /// Multi-hand evaluation: Moves grabbed window with primary fist hand, and resizes window with secondary pinch hand.
    /// </summary>
    /// <param name="hands">The list of active tracked hands.</param>
    /// <param name="frameWidth">Width of the camera frame in pixels.</param>
    /// <param name="frameHeight">Height of the camera frame in pixels.</param>
    public void Update(List<TrackedHand>? hands, int frameWidth, int frameHeight)
    {
        TrackedHand? primaryHand = null;
        TrackedHand? secondaryHand = null;

        if (hands != null && hands.Count > 0)
        {
            if (State.IsGrabbed)
            {
                // Find hand closest to last tracked palm center as the primary dragging hand
                primaryHand = hands.OrderBy(h => Math.Pow(h.SmoothedLandmarks2D[9].X - State.LastPalmCenter.X, 2) +
                                                 Math.Pow(h.SmoothedLandmarks2D[9].Y - State.LastPalmCenter.Y, 2)).FirstOrDefault();
                secondaryHand = hands.FirstOrDefault(h => h != primaryHand);
            }
            else
            {
                // Prefer hand making a Fist, otherwise first hand
                primaryHand = hands.FirstOrDefault(h => h.Gesture == "Fist") ?? hands[0];
                secondaryHand = hands.FirstOrDefault(h => h != primaryHand);
            }
        }

        (bool isGrabbed, IntPtr hwnd, int targetX, int targetY) = Detector.Update(primaryHand, frameWidth, frameHeight, _inputSink);

        if (isGrabbed && hwnd != IntPtr.Zero)
        {
            // Bring window to foreground and record focus once at grab start
            if (_lastForegroundHwnd != hwnd)
            {
                _inputSink.BringWindowToForeground(hwnd);
                _lastForegroundHwnd = hwnd;
                _inputSink.LastFocusedHwnd = hwnd;
                _inputSink.LastFocusedTitle = State.CachedWindowTitle;
            }

            _inputSink.MoveWindow(hwnd, targetX, targetY);
            _wasGrabbedLastFrame = true;

            // Process secondary hand pinch-zoom resizing
            if (secondaryHand != null)
            {
                (bool shouldResize, int newWidth, int newHeight) = ResizeDetector.Update(
                    secondaryHand,
                    State.InitialWindowBounds.Width,
                    State.InitialWindowBounds.Height,
                    _screenWidth,
                    _screenHeight);

                if (shouldResize)
                {
                    _inputSink.ResizeWindow(hwnd, newWidth, newHeight);
                }
            }
            else
            {
                ResizeDetector.Reset();
            }
        }
        else
        {
            ResizeDetector.Reset();

            if (_wasGrabbedLastFrame)
            {
                OnFistReleased?.Invoke();
                _wasGrabbedLastFrame = false;
            }
            _lastForegroundHwnd = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Renders augmented-reality visual feedback (hold countdown ring, scaled corner brackets, and pinch caliper) onto the camera frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    public void RenderFeedback(Mat frame)
    {
        // 1. Holding Countdown Ring around Palm Center (before grab activates)
        if (!State.IsGrabbed && State.HoldDurationSeconds > 0.1)
        {
            double progress = Math.Clamp(State.HoldDurationSeconds / State.RequiredHoldSeconds, 0.0, 1.0);
            int radius = 32;
            int angle = (int)(progress * 360);

            Point pt = new((int)Math.Round(State.LastPalmCenter.X), (int)Math.Round(State.LastPalmCenter.Y));

            Cv2.Circle(frame, pt, radius, new Scalar(40, 40, 55), 2, LineTypes.AntiAlias);
            Cv2.Ellipse(frame, pt, new Size(radius, radius), -90, 0, angle, new Scalar(0, 165, 255), 3, LineTypes.AntiAlias);

            double remaining = Math.Max(0, State.RequiredHoldSeconds - State.HoldDurationSeconds);
            string holdText = $"HOLD {remaining:F1}s";
            Cv2.PutText(frame, holdText, new Point(pt.X + 38, pt.Y + 5),
                HersheyFonts.HersheySimplex, 0.42, new Scalar(0, 165, 255), 1, LineTypes.AntiAlias);
        }

        // 2. Grabbed Window Corner-Bracket Overlay (Back-projected from Desktop Screen-Space)
        if (State.IsGrabbed && State.InitialWindowBounds.Width > 0 && State.InitialWindowBounds.Height > 0)
        {
            int currentWinW = ResizeState.IsActive && ResizeState.CurrentWidth > 0
                ? ResizeState.CurrentWidth
                : State.InitialWindowBounds.Width;

            int currentWinH = ResizeState.IsActive && ResizeState.CurrentHeight > 0
                ? ResizeState.CurrentHeight
                : State.InitialWindowBounds.Height;

            // Project Top-Left and Bottom-Right desktop bounds back to camera pixel coordinates
            (float camLeft, float camTop) = Detector.MapFromScreen(State.CurrentTargetX, State.CurrentTargetY, frame.Width, frame.Height);
            (float camRight, float camBottom) = Detector.MapFromScreen(
                State.CurrentTargetX + currentWinW,
                State.CurrentTargetY + currentWinH,
                frame.Width, frame.Height);

            int x = (int)Math.Round(camLeft);
            int y = (int)Math.Round(camTop);
            int w = (int)Math.Round(camRight - camLeft);
            int h = (int)Math.Round(camBottom - camTop);

            Rect boxRect = new(
                Math.Clamp(x, 2, frame.Width - 10),
                Math.Clamp(y, 2, frame.Height - 10),
                Math.Clamp(w, 20, frame.Width - 4),
                Math.Clamp(h, 20, frame.Height - 4)
            );

            Scalar themeColor = ResizeState.IsActive ? new Scalar(0, 220, 255) : new Scalar(0, 100, 255);

            // Translucent backdrop
            using (Mat overlay = frame.Clone())
            {
                Cv2.Rectangle(overlay, boxRect, new Scalar(15, 15, 25), -1);
                Cv2.AddWeighted(overlay, 0.35, frame, 0.65, 0, frame);
            }

            // Outer rectangle & corner brackets
            Cv2.Rectangle(frame, boxRect, themeColor, 1, LineTypes.AntiAlias);
            int cornerLen = Math.Min(25, Math.Min(boxRect.Width / 4, boxRect.Height / 4));

            if (cornerLen > 4)
            {
                Cv2.Line(frame, new Point(boxRect.Left, boxRect.Top), new Point(boxRect.Left + cornerLen, boxRect.Top), themeColor, 2);
                Cv2.Line(frame, new Point(boxRect.Left, boxRect.Top), new Point(boxRect.Left, boxRect.Top + cornerLen), themeColor, 2);

                Cv2.Line(frame, new Point(boxRect.Right, boxRect.Top), new Point(boxRect.Right - cornerLen, boxRect.Top), themeColor, 2);
                Cv2.Line(frame, new Point(boxRect.Right, boxRect.Top), new Point(boxRect.Right, boxRect.Top + cornerLen), themeColor, 2);

                Cv2.Line(frame, new Point(boxRect.Left, boxRect.Bottom), new Point(boxRect.Left + cornerLen, boxRect.Bottom), themeColor, 2);
                Cv2.Line(frame, new Point(boxRect.Left, boxRect.Bottom), new Point(boxRect.Left, boxRect.Bottom - cornerLen), themeColor, 2);

                Cv2.Line(frame, new Point(boxRect.Right, boxRect.Bottom), new Point(boxRect.Right - cornerLen, boxRect.Bottom), themeColor, 2);
                Cv2.Line(frame, new Point(boxRect.Right, boxRect.Bottom), new Point(boxRect.Right, boxRect.Bottom + cornerLen), themeColor, 2);
            }

            // Window metadata header badge
            string titleDisplay = State.CachedWindowTitle.Length > 20
                ? State.CachedWindowTitle.Substring(0, 17) + "..."
                : State.CachedWindowTitle;

            string tagText = ResizeState.IsActive
                ? $"RESIZING: [{titleDisplay}] {currentWinW}x{currentWinH} ({ResizeState.CurrentScale:F2}x)"
                : $"GRABBED: [{titleDisplay}] ({(int)State.CurrentTargetX}, {(int)State.CurrentTargetY})";

            Cv2.PutText(frame, tagText, new Point(Math.Max(10, boxRect.Left + 8), Math.Max(25, boxRect.Top + 20)),
                HersheyFonts.HersheySimplex, 0.44, new Scalar(255, 255, 255), 1, LineTypes.AntiAlias);
        }

        // 3. Pinch-to-Scale Measurement Caliper on Resizing Hand
        if (ResizeState.IsActive)
        {
            Point pThumb = new((int)ResizeState.ThumbTip.X, (int)ResizeState.ThumbTip.Y);
            Point pIndex = new((int)ResizeState.IndexTip.X, (int)ResizeState.IndexTip.Y);

            // Glowing caliper line between thumb and index
            Cv2.Line(frame, pThumb, pIndex, new Scalar(0, 220, 255), 2, LineTypes.AntiAlias);
            Cv2.Circle(frame, pThumb, 6, new Scalar(0, 255, 120), -1, LineTypes.AntiAlias);
            Cv2.Circle(frame, pIndex, 6, new Scalar(0, 255, 120), -1, LineTypes.AntiAlias);

            Point midPt = new((pThumb.X + pIndex.X) / 2, (pThumb.Y + pIndex.Y) / 2);
            string scaleLabel = $"SCALE: {ResizeState.CurrentScale:F2}x";
            Cv2.PutText(frame, scaleLabel, new Point(midPt.X + 12, midPt.Y),
                HersheyFonts.HersheySimplex, 0.44, new Scalar(0, 220, 255), 1, LineTypes.AntiAlias);
        }
    }
}
