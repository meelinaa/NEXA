using NEXA.Abstractions;
using NEXA.Adapters.Output;
using NEXA.Common;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.MonitorThrow;

/// <summary>
/// Application adapter orchestrating multi-monitor window transfers and augmented reality holographic feedback rendering.
/// <para>
/// <b>What it is:</b> The controller managing cross-display window translocation.
/// </para>
/// </summary>
public class MonitorThrowController : IHudStatusProvider, IFrameProcessor
{
    private readonly IInputSink _inputSink;
    private readonly MonitorThrowRenderer _renderer;

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
    /// <param name="detector">Optional custom monitor throw detector.</param>
    /// <param name="renderer">Optional custom renderer.</param>
    public MonitorThrowController(
        IInputSink? inputSink = null,
        MonitorThrowDetector? detector = null,
        MonitorThrowRenderer? renderer = null)
    {
        _inputSink = inputSink ?? new Win32InputSink();
        Detector = detector ?? new MonitorThrowDetector();
        _renderer = renderer ?? new MonitorThrowRenderer();
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

    /// <inheritdoc/>
    public void Process(FrameContext context)
    {
        Update(context.PrimaryHand);
    }

    /// <summary>
    /// Renders edge-on posture indicators and holographic monitor transfer arrows onto the camera frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="hand">The active tracked hand.</param>
    public void RenderFeedback(Mat frame, TrackedHand? hand)
    {
        _renderer.Render(frame, hand, State);
    }

    /// <inheritdoc/>
    public void Render(FrameContext context)
    {
        RenderFeedback(context.Frame, context.PrimaryHand);
    }

    /// <inheritdoc/>
    public string GetStatusText()
    {
        string throwStatus = Enabled
            ? (State.InCooldown ? "Cooldown (800ms)" : (State.IsEdgeOnPosture ? "Handkante erkannt!" : "Bereit (Handkante Wisch)"))
            : "AUS (Taste M)";
        return TextSanitizer.ToSafeAscii($"Monitor (M): {throwStatus}");
    }

    /// <inheritdoc/>
    public Scalar GetStatusColor()
    {
        if (!Enabled) return new Scalar(160, 160, 160);
        return State.IsEdgeOnPosture ? new Scalar(255, 100, 200) : new Scalar(0, 255, 120);
    }
}

