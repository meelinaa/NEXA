using System;
using NEXA.Adapters.Output;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.MonitorThrow;

/// <summary>
/// Application adapter orchestrating multi-monitor window transfers and augmented reality holographic feedback rendering.
/// <para>
/// <b>What it is:</b> The controller managing cross-display window translocation.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description>Evaluates tracked hand orientation and velocity via <see cref="MonitorThrowDetector"/>.</description></item>
/// <item><description>Invokes <see cref="IInputSink.MoveWindowToAdjacentMonitor"/> upon detecting an edge-on swipe.</description></item>
/// <item><description>Renders AR visual cues: edge-on blade contour highlights and animated holographic monitor warp arrows.</description></item>
/// </list>
/// </para>
/// </summary>
public class MonitorThrowController
{
    /// <summary>
    /// The input sink used to query display topology and relocate windows across monitors.
    /// </summary>
    private readonly IInputSink _inputSink;

    /// <summary>
    /// The core domain detector evaluating edge-on hand posture and swipe kinematics.
    /// </summary>
    public MonitorThrowDetector Detector { get; }

    /// <summary>
    /// Gets or sets a value indicating whether monitor throw gestures are active.
    /// </summary>
    public bool Enabled
    {
        get => Detector.Enabled;
        set => Detector.Enabled = value;
    }

    /// <summary>
    /// Gets the internal state machine from the detector.
    /// </summary>
    public MonitorThrowState State => Detector.State;

    /// <summary>
    /// Initializes a new instance of the <see cref="MonitorThrowController"/> class.
    /// </summary>
    /// <param name="inputSink">The output adapter for OS display topology and window inputs.</param>
    public MonitorThrowController(IInputSink? inputSink = null)
    {
        _inputSink = inputSink ?? new Win32InputSink();
        Detector = new MonitorThrowDetector();
    }

    /// <summary>
    /// Evaluates the tracked hand for the current frame and transfers the focused window across monitors if triggered.
    /// </summary>
    /// <param name="hand">The tracked hand instance.</param>
    public void Update(TrackedHand? hand)
    {
        MonitorThrowDecision? decision = Detector.Update(hand, _inputSink);
        if (decision != null)
        {
            _inputSink.MoveWindowToAdjacentMonitor(decision.TargetHwnd, decision.Direction == MonitorThrowDirection.Right);
        }
    }

    /// <summary>
    /// Renders edge-on posture indicators and holographic monitor transfer arrows onto the camera frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="hand">The active tracked hand.</param>
    public void RenderFeedback(Mat frame, TrackedHand? hand)
    {
        // 1. Edge-On Blade Hand Highlight Indicator
        if (State.IsEdgeOnPosture && hand != null)
        {
            Point pWrist = new((int)hand.SmoothedLandmarks2D[0].X, (int)hand.SmoothedLandmarks2D[0].Y);
            Point pPinky = new((int)hand.SmoothedLandmarks2D[20].X, (int)hand.SmoothedLandmarks2D[20].Y);

            // Glowing magenta/cyan blade line
            Cv2.Line(frame, pWrist, pPinky, new Scalar(255, 100, 200), 3, LineTypes.AntiAlias);
            Cv2.Circle(frame, pPinky, 5, new Scalar(0, 255, 255), -1, LineTypes.AntiAlias);

            Point tagPt = new((pWrist.X + pPinky.X) / 2 + 15, (pWrist.Y + pPinky.Y) / 2);
            Cv2.PutText(frame, "BLADE (MONITOR THROW)", tagPt,
                HersheyFonts.HersheySimplex, 0.40, new Scalar(255, 100, 200), 1, LineTypes.AntiAlias);
        }

        // 2. Holographic Monitor Transfer Animation
        double elapsed = (DateTime.Now - State.LastFeedbackTime).TotalMilliseconds;
        if (elapsed < 600.0)
        {
            float progress = (float)(elapsed / 600.0);
            int cx = (int)State.LastSwipeCenter.X;
            int cy = (int)State.LastSwipeCenter.Y;

            bool isRight = State.LastDirection == "RIGHT";
            int spread = (int)(progress * 70);
            Scalar arrowColor = new(0, 255, 255); // Vibrant Yellow/Cyan

            string transferText = isRight
                ? "==> MONITOR [Right] ==>"
                : "<== MONITOR [Left] <==";

            int textX = isRight ? cx + spread - 30 : cx - spread - 100;
            Cv2.PutText(frame, transferText, new Point(Math.Clamp(textX, 10, frame.Width - 250), Math.Max(35, cy)),
                HersheyFonts.HersheySimplex, 0.65, arrowColor, 2, LineTypes.AntiAlias);

            // Animated horizontal arrow rays
            if (isRight)
            {
                Cv2.ArrowedLine(frame, new Point(cx, cy + 20), new Point(cx + spread + 40, cy + 20), arrowColor, 3);
            }
            else
            {
                Cv2.ArrowedLine(frame, new Point(cx, cy + 20), new Point(cx - spread - 40, cy + 20), arrowColor, 3);
            }
        }
    }
}
