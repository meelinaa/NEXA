using System;
using System.Collections.Generic;
using System.IO;
using NEXA.Adapters.Output;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.TwoHand;

/// <summary>
/// Application adapter orchestrating two-hand Maximize/Minimize window operations, Camera-Frame Screenshot capture, Clap/Prayer Media Play/Pause, and augmented reality HUD rendering.
/// <para>
/// <b>What it is:</b> The controller executing multi-hand window state transitions, screen region captures, and media playback injections.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description>Passes dual tracked hands into <see cref="TwoHandGestureDetector"/>.</description></item>
/// <item><description>Dispatches <see cref="IInputSink.MaximizeWindow"/>, <see cref="IInputSink.MinimizeWindow"/>, <see cref="IScreenshotSink.CaptureScreenRegion"/>, or <see cref="IInputSink.SendMediaPlayPause"/>.</description></item>
/// <item><description>Renders AR visual cues: active window countdown banner, viewfinder framing corner brackets, white flash animations, and Play/Pause pulse overlays.</description></item>
/// </list>
/// </para>
/// </summary>
public class TwoHandGestureController
{
    /// <summary>
    /// The input sink used to apply window state changes and read focused window handles.
    /// </summary>
    private readonly IInputSink _inputSink;

    /// <summary>
    /// The screenshot sink used to capture desktop regions and copy/save bitmaps.
    /// </summary>
    private readonly IScreenshotSink _screenshotSink;

    /// <summary>
    /// The core domain detector evaluating Maximize, Minimize, Screenshot, and Play/Pause gestures.
    /// </summary>
    public TwoHandGestureDetector Detector { get; }

    /// <summary>
    /// Gets or sets a value indicating whether two-hand gesture processing is active.
    /// </summary>
    public bool Enabled
    {
        get => Detector.Enabled;
        set => Detector.Enabled = value;
    }

    /// <summary>
    /// Gets the internal state machine from the detector.
    /// </summary>
    public TwoHandGestureState State => Detector.State;

    /// <summary>
    /// Gets or sets the target output directory for saving screenshot PNG files.
    /// </summary>
    public string OutputDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        "NEXA-Screenshots"
    );

    /// <summary>
    /// Primary desktop display width in pixels.
    /// </summary>
    private readonly int _screenWidth;

    /// <summary>
    /// Primary desktop display height in pixels.
    /// </summary>
    private readonly int _screenHeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="TwoHandGestureController"/> class.
    /// </summary>
    /// <param name="inputSink">The output adapter for OS inputs.</param>
    /// <param name="screenshotSink">The output adapter for desktop screen capture.</param>
    public TwoHandGestureController(IInputSink? inputSink = null, IScreenshotSink? screenshotSink = null)
    {
        _inputSink = inputSink ?? new Win32InputSink();
        _screenshotSink = screenshotSink ?? new Win32ScreenshotSink();
        Detector = new TwoHandGestureDetector();

        (int w, int h) = _inputSink.GetScreenResolution();
        _screenWidth = w > 0 ? w : 1920;
        _screenHeight = h > 0 ? h : 1080;
    }

    /// <summary>
    /// Evaluates tracked hands for the current frame and executes window commands, screenshots, or media play/pause.
    /// </summary>
    /// <param name="hands">The list of active tracked hands.</param>
    /// <param name="frameWidth">Camera frame width in pixels.</param>
    /// <param name="frameHeight">Camera frame height in pixels.</param>
    public void Update(List<TrackedHand>? hands, int frameWidth = 1280, int frameHeight = 720)
    {
        TwoHandGestureDecision? decision = Detector.Update(hands, _inputSink);
        if (decision != null)
        {
            if (decision.Action == TwoHandAction.Maximize)
            {
                _inputSink.MaximizeWindow(decision.TargetHwnd);
            }
            else if (decision.Action == TwoHandAction.Minimize)
            {
                _inputSink.MinimizeWindow(decision.TargetHwnd);
            }
            else if (decision.Action == TwoHandAction.Screenshot)
            {
                // Capture full primary desktop screen
                _screenshotSink.CaptureScreenRegion(0, 0, _screenWidth, _screenHeight, OutputDirectory, out string savedFilePath);
                State.LastSavedFilePath = savedFilePath;
            }
            else if (decision.Action == TwoHandAction.PlayPause)
            {
                _inputSink.SendMediaPlayPause();
            }
        }
    }

    /// <summary>
    /// Renders visual feedback (3.0s window countdown badge, camera viewfinder brackets, white flash, and action animations) onto the camera frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="hands">The list of active tracked hands.</param>
    public void RenderFeedback(Mat frame, List<TrackedHand>? hands)
    {
        DateTime now = DateTime.Now;

        // 1. Live Camera-Frame Viewfinder Bounding Box (Spanned by dual "L" hands)
        if (State.IsCameraFrameActive && State.LiveCameraFrameRect.Width > 0 && State.LiveCameraFrameRect.Height > 0)
        {
            Rect box = new(
                Math.Clamp((int)Math.Round(State.LiveCameraFrameRect.X), 2, frame.Width - 10),
                Math.Clamp((int)Math.Round(State.LiveCameraFrameRect.Y), 2, frame.Height - 10),
                Math.Clamp((int)Math.Round(State.LiveCameraFrameRect.Width), 20, frame.Width - 4),
                Math.Clamp((int)Math.Round(State.LiveCameraFrameRect.Height), 20, frame.Height - 4)
            );

            Scalar frameColor = new(0, 255, 120); // Neon Green
            int cornerLen = Math.Min(30, Math.Min(box.Width / 4, box.Height / 4));

            // Translucent dark fill
            using (Mat overlay = frame.Clone())
            {
                Cv2.Rectangle(overlay, box, new Scalar(20, 30, 20), -1);
                Cv2.AddWeighted(overlay, 0.25, frame, 0.75, 0, frame);
            }

            // Outer border
            Cv2.Rectangle(frame, box, frameColor, 1, LineTypes.AntiAlias);

            // 4 Viewfinder Corner Brackets
            Cv2.Line(frame, new Point(box.Left, box.Top), new Point(box.Left + cornerLen, box.Top), frameColor, 3);
            Cv2.Line(frame, new Point(box.Left, box.Top), new Point(box.Left, box.Top + cornerLen), frameColor, 3);

            Cv2.Line(frame, new Point(box.Right, box.Top), new Point(box.Right - cornerLen, box.Top), frameColor, 3);
            Cv2.Line(frame, new Point(box.Right, box.Top), new Point(box.Right, box.Top + cornerLen), frameColor, 3);

            Cv2.Line(frame, new Point(box.Left, box.Bottom), new Point(box.Left + cornerLen, box.Bottom), frameColor, 3);
            Cv2.Line(frame, new Point(box.Left, box.Bottom), new Point(box.Left, box.Bottom - cornerLen), frameColor, 3);

            Cv2.Line(frame, new Point(box.Right, box.Bottom), new Point(box.Right - cornerLen, box.Bottom), frameColor, 3);
            Cv2.Line(frame, new Point(box.Right, box.Bottom), new Point(box.Right, box.Bottom - cornerLen), frameColor, 3);

            // Center crosshair
            Point centerPt = new(box.Left + box.Width / 2, box.Top + box.Height / 2);
            Cv2.Line(frame, new Point(centerPt.X - 10, centerPt.Y), new Point(centerPt.X + 10, centerPt.Y), frameColor, 1);
            Cv2.Line(frame, new Point(centerPt.X, centerPt.Y - 10), new Point(centerPt.X, centerPt.Y + 10), frameColor, 1);

            string tagText;
            Scalar tagColor;

            if (State.ScreenshotHoldProgress > 0.01)
            {
                double remaining = Math.Max(0.0, State.RequiredScreenshotHoldSeconds - State.ScreenshotHoldDurationSeconds);
                tagText = $"[HALTE: {remaining:F1}s ({(int)(State.ScreenshotHoldProgress * 100)}%)]";
                tagColor = new Scalar(0, 220, 255); // Cyan

                // Center charging radial arc
                int arcAngle = (int)(State.ScreenshotHoldProgress * 360);
                Cv2.Ellipse(frame, centerPt, new Size(24, 24), -90, 0, arcAngle, tagColor, 3, LineTypes.AntiAlias);
            }
            else
            {
                tagText = "[KAMERA-RAHMEN: FINGER 2s ZUSAMMENHALTEN]";
                tagColor = frameColor;
            }

            Cv2.PutText(frame, tagText, new Point(Math.Max(10, box.Left + 6), Math.Max(25, box.Top - 8)),
                HersheyFonts.HersheySimplex, 0.40, tagColor, 1, LineTypes.AntiAlias);
        }

        // 2. White Flash Overlay Animation (~220ms on screenshot trigger)
        double elapsedFlash = (now - State.LastScreenshotTime).TotalMilliseconds;
        if (elapsedFlash < 220 && State.LastCapturedFrameRect.Width > 0)
        {
            Rect flashBox = new(
                Math.Clamp((int)Math.Round(State.LastCapturedFrameRect.X), 2, frame.Width - 10),
                Math.Clamp((int)Math.Round(State.LastCapturedFrameRect.Y), 2, frame.Height - 10),
                Math.Clamp((int)Math.Round(State.LastCapturedFrameRect.Width), 20, frame.Width - 4),
                Math.Clamp((int)Math.Round(State.LastCapturedFrameRect.Height), 20, frame.Height - 4)
            );

            double alpha = Math.Clamp(1.0 - (elapsedFlash / 220.0), 0.0, 1.0) * 0.70;
            using (Mat flashOverlay = frame.Clone())
            {
                Cv2.Rectangle(flashOverlay, flashBox, new Scalar(255, 255, 255), -1);
                Cv2.AddWeighted(flashOverlay, alpha, frame, 1.0 - alpha, 0, frame);
            }

            Cv2.Rectangle(frame, flashBox, new Scalar(255, 255, 255), 2, LineTypes.AntiAlias);
            Cv2.PutText(frame, "* SCREENSHOT SAVED & COPIED *", new Point(flashBox.Left + 10, flashBox.Top + flashBox.Height / 2),
                HersheyFonts.HersheySimplex, 0.52, new Scalar(0, 255, 255), 2, LineTypes.AntiAlias);
        }

        // 3. Active 3.0s Window Status Banner (Top Center)
        if (State.IsWindowActive && _inputSink.LastFocusedHwnd != IntPtr.Zero)
        {
            string rawTitle = NEXA.Common.TextSanitizer.ToSafeAscii(_inputSink.LastFocusedTitle);
            string winTitle = rawTitle.Length > 20
                ? rawTitle.Substring(0, 17) + "..."
                : rawTitle;

            string bannerText = NEXA.Common.TextSanitizer.ToSafeAscii($"[2-HAND READY ({State.RemainingWindowSeconds:F1}s): {winTitle}]");
            Size textSize = Cv2.GetTextSize(bannerText, HersheyFonts.HersheySimplex, 0.44, 1, out _);

            int bannerX = Math.Max(10, (frame.Width - textSize.Width) / 2);
            Rect bannerRect = new(bannerX - 10, 12, textSize.Width + 20, textSize.Height + 14);

            using (Mat bannerMat = frame.Clone())
            {
                Cv2.Rectangle(bannerMat, bannerRect, new Scalar(20, 20, 30), -1);
                Cv2.AddWeighted(bannerMat, 0.75, frame, 0.25, 0, frame);
            }

            Cv2.Rectangle(frame, bannerRect, new Scalar(0, 255, 120), 1, LineTypes.AntiAlias);
            Cv2.PutText(frame, bannerText, new Point(bannerX, 28),
                HersheyFonts.HersheySimplex, 0.44, new Scalar(0, 255, 120), 1, LineTypes.AntiAlias);
        }

        // 4. Touch Link Line (Maximize Gesture Indicator)
        if (State.IsTouchActive)
        {
            Point p1 = new((int)State.TouchPoint1.X, (int)State.TouchPoint1.Y);
            Point p2 = new((int)State.TouchPoint2.X, (int)State.TouchPoint2.Y);

            Cv2.Line(frame, p1, p2, new Scalar(0, 220, 255), 2, LineTypes.AntiAlias);
            Cv2.Circle(frame, p1, 6, new Scalar(0, 255, 120), -1, LineTypes.AntiAlias);
            Cv2.Circle(frame, p2, 6, new Scalar(0, 255, 120), -1, LineTypes.AntiAlias);

            Point midPt = new((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
            Cv2.PutText(frame, "SPREAD APART TO MAXIMIZE", new Point(midPt.X - 80, midPt.Y - 14),
                HersheyFonts.HersheySimplex, 0.38, new Scalar(0, 220, 255), 1, LineTypes.AntiAlias);
        }

        // 5. Trigger Flash & Action Animation Banner
        double elapsedFeedback = (now - State.LastFeedbackTime).TotalMilliseconds;
        if (elapsedFeedback < 900 && !string.IsNullOrEmpty(State.LastAction))
        {
            float progress = (float)(elapsedFeedback / 900.0);
            int animRadius = (int)(20 + progress * 50);
            Point center = new((int)State.LastFeedbackCenter.X, (int)State.LastFeedbackCenter.Y);

            Scalar actionColor;
            if (State.LastAction == "MAXIMIZE")
            {
                actionColor = new Scalar(0, 255, 120); // Green
            }
            else if (State.LastAction == "SCREENSHOT")
            {
                actionColor = new Scalar(255, 255, 255); // White
            }
            else if (State.LastAction == "PLAY / PAUSE")
            {
                actionColor = new Scalar(0, 220, 255); // Cyan
            }
            else
            {
                actionColor = new Scalar(0, 100, 255); // Red / Orange
            }

            Cv2.Circle(frame, center, animRadius, actionColor, 2, LineTypes.AntiAlias);

            string actionLabel = State.LastAction == "PLAY / PAUSE" ? "> || [PLAY / PAUSE]" : $"* {State.LastAction} *";
            Cv2.PutText(frame, NEXA.Common.TextSanitizer.ToSafeAscii(actionLabel), new Point(center.X - 60, center.Y - animRadius - 8),
                HersheyFonts.HersheySimplex, 0.60, actionColor, 2, LineTypes.AntiAlias);
        }
    }

    /// <summary>
    /// Maps 2D camera coordinates into desktop screen coordinates with 15% comfort margins.
    /// </summary>
    private (double screenX, double screenY) MapToScreen(float x, float y, int frameWidth, int frameHeight)
    {
        float marginX = frameWidth * 0.15f;
        float marginY = frameHeight * 0.15f;

        float normX = Math.Clamp((x - marginX) / (frameWidth - 2 * marginX), 0.0f, 1.0f);
        float normY = Math.Clamp((y - marginY) / (frameHeight - 2 * marginY), 0.0f, 1.0f);

        double screenX = normX * _screenWidth;
        double screenY = normY * _screenHeight;

        return (screenX, screenY);
    }
}
