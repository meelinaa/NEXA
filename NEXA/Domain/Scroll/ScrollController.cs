using System;
using NEXA.Adapters.Output;
using NEXA.Domain.Scroll;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Scroll;

/// <summary>
/// Application adapter bridging domain swipe detection with OS scroll input injection and OpenCV visual feedback rendering.
/// <para>
/// <b>What it is:</b> The orchestration controller for swipe-to-scroll functionality.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description>Delegates gesture tracking and physics momentum to the underlying <see cref="ScrollDetector"/>.</description></item>
/// <item><description>Receives <see cref="ScrollDecision"/> outputs and dispatches physical mouse wheel clicks through <see cref="IInputSink.Scroll"/>.</description></item>
/// <item><description>Renders dynamic on-screen floating arrow animations with real-time slope telemetry.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Why it is used:</b> Connects the abstract domain detector to concrete platform inputs while keeping the main frame loop in <c>Program.cs</c> uncluttered.
/// </para>
/// <para>
/// <b>Consequence:</b> Delivers smooth, visual, and responsive gesture-controlled scrolling.
/// </para>
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ScrollController"/> class with an optional injected input sink.
/// </remarks>
/// <param name="inputSink">The output adapter to receive wheel deltas (defaults to <see cref="Win32InputSink"/> if null).</param>
public class ScrollController(IInputSink? inputSink = null)
{
    /// <summary>
    /// The input sink used to send physical mouse wheel commands to Windows.
    /// </summary>
    private readonly IInputSink _inputSink = inputSink ?? new Win32InputSink();

    /// <summary>
    /// The core domain detector handling swipe analysis and momentum physics.
    /// </summary>
    public ScrollDetector Detector { get; } = new ScrollDetector();

    /// <summary>
    /// Gets or sets a value indicating whether scroll processing is enabled.
    /// </summary>
    public bool Enabled
    {
        get => Detector.Enabled;
        set => Detector.Enabled = value;
    }

    /// <summary>
    /// Gets the internal swipe state machine from the detector.
    /// </summary>
    public SwipeState State => Detector.State;

    /// <summary>
    /// Gets a value indicating whether the 3-second activation window is currently open.
    /// </summary>
    public bool IsWindowActive => Detector.IsWindowActive;

    /// <summary>
    /// Gets the remaining active duration in seconds before the scroll window closes.
    /// </summary>
    public double RemainingWindowSeconds => Detector.RemainingWindowSeconds;

    /// <summary>
    /// Gets or sets the timestamp when mouse pointer mode was last active.
    /// </summary>
    public DateTime LastPointerActiveTime
    {
        get => Detector.LastPointerActiveTime;
        set => Detector.LastPointerActiveTime = value;
    }

    /// <summary>
    /// Processes ongoing momentum inertia coasting each frame and dispatches resulting wheel movements to the OS.
    /// </summary>
    public void UpdateMomentum()
    {
        var decision = Detector.UpdateMomentum();
        if (decision != null)
        {
            _inputSink.Scroll(decision.WheelDelta);
        }
    }

    /// <summary>
    /// Evaluates hand movements in the current frame and dispatches immediate scroll actions to the OS.
    /// </summary>
    /// <param name="hand">The tracked hand instance.</param>
    public void Update(TrackedHand? hand)
    {
        var decision = Detector.Update(hand);
        if (decision != null)
        {
            _inputSink.Scroll(decision.WheelDelta);
        }
    }

    /// <summary>
    /// Renders floating animated swipe arrows and real-time regression slope text onto the camera frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    public void RenderFeedback(Mat frame)
    {
        double elapsed = (DateTime.Now - Detector.LastFeedbackTime).TotalMilliseconds;
        if (elapsed < 550)
        {
            float progress = (float)(elapsed / 550.0);

            int x = (int)Detector.LastSwipePoint.X;
            int y = (int)Detector.LastSwipePoint.Y;

            bool isUp = Detector.LastSwipeDirection == "UP";
            var color = Detector.LastInitialVelocity >= 100
                ? new Scalar(0, 100, 255)  // High velocity: Orange/Red
                : new Scalar(0, 240, 255); // Normal velocity: Cyan/Yellow

            int offset = (int)(progress * 40);
            int drawY = isUp ? y - offset : y + offset;

            string arrowText = isUp
                ? $"^ SCROLL UP (Slope: {Detector.State.LastSlope:+0.00;-0.00;0.00})"
                : $"v SCROLL DOWN (Slope: {Detector.State.LastSlope:+0.00;-0.00;0.00})";

            Cv2.PutText(frame, arrowText, new Point(Math.Max(10, x - 85), Math.Max(30, drawY)),
                HersheyFonts.HersheySimplex, 0.52, color, 2, LineTypes.AntiAlias);
        }
    }
}
