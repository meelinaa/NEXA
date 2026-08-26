using System;
using NEXA.Adapters.Output;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Grab;

/// <summary>
/// Application adapter orchestrating real OS window grabbing, delta relocation, and camera viewport feedback rendering.
/// <para>
/// <b>What it is:</b> The controller responsible for moving physical Windows desktop applications via hand gestures.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description>Initializes the <see cref="WindowGrabDetector"/> with screen resolution obtained from <see cref="IInputSink.GetScreenResolution"/>.</description></item>
/// <item><description>Brings the grabbed window to the foreground via <see cref="IInputSink.BringWindowToForeground"/> upon latching.</description></item>
/// <item><description>Invokes <see cref="IInputSink.MoveWindow"/> when a window is grabbed and moved.</description></item>
/// <item><description>Inverse-projects desktop window bounds into camera space via <see cref="WindowGrabDetector.MapFromScreen"/> to draw glowing corner brackets and metadata tags.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Why it is used:</b> Connects the abstract domain detector to concrete platform inputs while keeping the main frame loop in <c>Program.cs</c> clean.
/// </para>
/// <para>
/// <b>Consequence:</b> Provides an intuitive, responsive, augmented-reality window manipulation experience.
/// </para>
/// </summary>
public class WindowGrabController
{
    /// <summary>
    /// The input sink used to query and move physical OS windows.
    /// </summary>
    private readonly IInputSink _inputSink;

    /// <summary>
    /// Track the HWND that was activated to ensure BringWindowToForeground is called once per grab cycle.
    /// </summary>
    private IntPtr _lastForegroundHwnd = IntPtr.Zero;

    /// <summary>
    /// The core domain detector handling hold timing, coordinate mapping, and delta dragging.
    /// </summary>
    public WindowGrabDetector Detector { get; }

    /// <summary>
    /// Gets or sets a value indicating whether window grabbing is enabled.
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
    /// Initializes a new instance of the <see cref="WindowGrabController"/> class, querying display resolution from the input sink.
    /// </summary>
    /// <param name="inputSink">The output adapter for OS inputs (defaults to <see cref="Win32InputSink"/> if null).</param>
    public WindowGrabController(IInputSink? inputSink = null)
    {
        _inputSink = inputSink ?? new Win32InputSink();
        var (width, height) = _inputSink.GetScreenResolution();
        Detector = new WindowGrabDetector(width, height);
    }

    /// <summary>
    /// Evaluates hand tracking data for the current frame and moves the grabbed desktop window if active.
    /// </summary>
    /// <param name="hand">The tracked hand instance.</param>
    /// <param name="frameWidth">Width of the camera frame in pixels.</param>
    /// <param name="frameHeight">Height of the camera frame in pixels.</param>
    public void Update(TrackedHand? hand, int frameWidth, int frameHeight)
    {
        var (isGrabbed, hwnd, targetX, targetY) = Detector.Update(hand, frameWidth, frameHeight, _inputSink);

        if (isGrabbed && hwnd != IntPtr.Zero)
        {
            // Bring window to foreground once at grab start
            if (_lastForegroundHwnd != hwnd)
            {
                _inputSink.BringWindowToForeground(hwnd);
                _lastForegroundHwnd = hwnd;
            }

            _inputSink.MoveWindow(hwnd, targetX, targetY);
        }
        else
        {
            _lastForegroundHwnd = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Renders augmented-reality visual feedback (hold countdown ring and window tracking overlay) onto the camera frame.
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

            var pt = new Point((int)Math.Round(State.LastPalmCenter.X), (int)Math.Round(State.LastPalmCenter.Y));

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
            // Project Top-Left and Bottom-Right desktop bounds back to camera pixel coordinates
            var (camLeft, camTop) = Detector.MapFromScreen(State.CurrentTargetX, State.CurrentTargetY, frame.Width, frame.Height);
            var (camRight, camBottom) = Detector.MapFromScreen(
                State.CurrentTargetX + State.InitialWindowBounds.Width,
                State.CurrentTargetY + State.InitialWindowBounds.Height,
                frame.Width, frame.Height);

            int x = (int)Math.Round(camLeft);
            int y = (int)Math.Round(camTop);
            int w = (int)Math.Round(camRight - camLeft);
            int h = (int)Math.Round(camBottom - camTop);

            var boxRect = new Rect(
                Math.Clamp(x, 2, frame.Width - 10),
                Math.Clamp(y, 2, frame.Height - 10),
                Math.Clamp(w, 20, frame.Width - 4),
                Math.Clamp(h, 20, frame.Height - 4)
            );

            var themeColor = new Scalar(0, 100, 255); // Glowing Orange / Red

            // Translucent backdrop
            using (var overlay = frame.Clone())
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
                Cv2.Line(frame, new Point(boxRect.Left, boxRect.Top), new Point(boxRect.Left + cornerLen, boxRect.Top), themeColor, 2);

                Cv2.Line(frame, new Point(boxRect.Right, boxRect.Top), new Point(boxRect.Right - cornerLen, boxRect.Top), themeColor, 2);
                Cv2.Line(frame, new Point(boxRect.Right, boxRect.Top), new Point(boxRect.Right, boxRect.Top + cornerLen), themeColor, 2);

                Cv2.Line(frame, new Point(boxRect.Left, boxRect.Bottom), new Point(boxRect.Left + cornerLen, boxRect.Bottom), themeColor, 2);
                Cv2.Line(frame, new Point(boxRect.Left, boxRect.Bottom), new Point(boxRect.Left + cornerLen, boxRect.Bottom), themeColor, 2);

                Cv2.Line(frame, new Point(boxRect.Right, boxRect.Bottom), new Point(boxRect.Right - cornerLen, boxRect.Bottom), themeColor, 2);
                Cv2.Line(frame, new Point(boxRect.Right, boxRect.Bottom), new Point(boxRect.Right, boxRect.Bottom + cornerLen), themeColor, 2);
            }

            // Window metadata header badge
            string titleDisplay = State.CachedWindowTitle.Length > 24
                ? State.CachedWindowTitle.Substring(0, 21) + "..."
                : State.CachedWindowTitle;

            string tagText = $"GRABBED: [{titleDisplay}] ({(int)State.CurrentTargetX}, {(int)State.CurrentTargetY})";
            Cv2.PutText(frame, tagText, new Point(Math.Max(10, boxRect.Left + 8), Math.Max(25, boxRect.Top + 20)),
                HersheyFonts.HersheySimplex, 0.45, new Scalar(255, 255, 255), 1, LineTypes.AntiAlias);
        }
    }
}
