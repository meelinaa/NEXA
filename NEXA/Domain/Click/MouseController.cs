using System;
using NEXA.Adapters.Output;
using NEXA.Domain.Click;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Click;

/// <summary>
/// Application adapter bridging domain pointer mapping and dwell-clicking with OS mouse events and visual feedback rendering.
/// <para>
/// <b>What it is:</b> The controller responsible for moving the Windows mouse cursor and issuing left clicks based on hand gestures.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description>Queries the physical display resolution via <see cref="IInputSink.GetScreenResolution"/> to initialize <see cref="DwellClickDetector"/>.</description></item>
/// <item><description>Dispatches mapped coordinates to <see cref="IInputSink.MoveCursor"/> and click signals to <see cref="IInputSink.Click"/>.</description></item>
/// <item><description>Renders an augmented-reality radial loading ring around the index fingertip and a glowing ripple flash when a click triggers.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Why it is used:</b> Encapsulates OS interop and visual rendering away from pure calculation logic.
/// </para>
/// <para>
/// <b>Consequence:</b> Provides an intuitive visual UI and frictionless desktop mouse navigation.
/// </para>
/// </summary>
public class MouseController
{
    /// <summary>
    /// The input sink used to send physical cursor position and click commands to the OS.
    /// </summary>
    private readonly IInputSink _inputSink;

    /// <summary>
    /// The core domain detector handling screen mapping, smoothing, and dwell clicking.
    /// </summary>
    public DwellClickDetector Detector { get; }

    /// <summary>
    /// Gets or sets a value indicating whether mouse tracking and clicking are enabled.
    /// </summary>
    public bool Enabled
    {
        get => Detector.Enabled;
        set => Detector.Enabled = value;
    }

    /// <summary>
    /// Gets the internal dwell state machine from the detector.
    /// </summary>
    public DwellClickState DwellState => Detector.DwellState;

    /// <summary>
    /// Gets the timestamp of when pointer mode was last actively tracked.
    /// </summary>
    public DateTime LastPointerActiveTime => Detector.LastPointerActiveTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="MouseController"/> class, querying monitor resolution from the input sink.
    /// </summary>
    /// <param name="inputSink">The output adapter for OS inputs (defaults to <see cref="Win32InputSink"/> if null).</param>
    public MouseController(IInputSink? inputSink = null)
    {
        _inputSink = inputSink ?? new Win32InputSink();
        (int width, int height) = _inputSink.GetScreenResolution();
        Detector = new DwellClickDetector(width, height);
    }

    /// <summary>
    /// Processes hand tracking data for the current frame, updating physical cursor position and triggering clicks if ready.
    /// </summary>
    /// <param name="hand">The tracked hand instance.</param>
    /// <param name="frameWidth">Width of the camera frame in pixels.</param>
    /// <param name="frameHeight">Height of the camera frame in pixels.</param>
    public void Update(TrackedHand? hand, int frameWidth, int frameHeight)
    {
        (int? moveX, int? moveY, bool shouldClick) = Detector.Update(hand, frameWidth, frameHeight);

        if (moveX.HasValue && moveY.HasValue)
        {
            _inputSink.MoveCursor(moveX.Value, moveY.Value);
        }

        if (shouldClick)
        {
            _inputSink.Click();
        }
    }

    /// <summary>
    /// Renders visual feedback (radial charging arc and click ripple flash) onto the camera frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="hand">The currently tracked hand.</param>
    public void RenderFeedback(Mat frame, TrackedHand? hand)
    {
        if (hand == null) return;

        Point2f indexTip = hand.SmoothedLandmarks2D[8];
        Point pt = new((int)Math.Round(indexTip.X), (int)Math.Round(indexTip.Y));

        // 1. Dwell-Click Radial Charge Ring
        if (Detector.DwellState.IsHovering && Detector.DwellState.HoverProgress > 0.05)
        {
            int radius = 22;
            int angle = (int)(Detector.DwellState.HoverProgress * 360);

            // Dark outer tracking ring
            Cv2.Circle(frame, pt, radius, new Scalar(60, 60, 80), 2, LineTypes.AntiAlias);

            // Dynamic color shift: Cyan -> Neon Green as progress approaches 100%
            Scalar arcColor = Detector.DwellState.HoverProgress > 0.7
                ? new Scalar(0, 255, 120) // Green
                : new Scalar(0, 220, 255); // Cyan

            Cv2.Ellipse(frame, pt, new Size(radius, radius), -90, 0, angle, arcColor, 3, LineTypes.AntiAlias);
            Cv2.Circle(frame, pt, 4, arcColor, -1, LineTypes.AntiAlias);

            string pct = $"{(int)(Detector.DwellState.HoverProgress * 100)}%";
            Cv2.PutText(frame, pct, new Point(pt.X + 26, pt.Y + 5),
                HersheyFonts.HersheySimplex, 0.40, arcColor, 1, LineTypes.AntiAlias);
        }

        // 2. Click Animation (Expanding ripple flash on dispatch)
        double elapsedSinceClick = (DateTime.Now - Detector.DwellState.LastClickTime).TotalMilliseconds;
        if (elapsedSinceClick < 400)
        {
            float rippleProgress = (float)(elapsedSinceClick / 400.0);
            int rippleRadius = (int)(12 + rippleProgress * 40);
            Point clickPt = new((int)Math.Round(Detector.LastClickPosition.X), (int)Math.Round(Detector.LastClickPosition.Y));

            Cv2.Circle(frame, clickPt, rippleRadius, new Scalar(0, 255, 255), 2, LineTypes.AntiAlias);
            Cv2.PutText(frame, "* CLICK *", new Point(clickPt.X - 28, clickPt.Y - rippleRadius - 6),
                HersheyFonts.HersheySimplex, 0.52, new Scalar(0, 255, 255), 1, LineTypes.AntiAlias);
        }
    }
}
