using System;
using NEXA.Abstractions;
using NEXA.Adapters.Output;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Scroll;

/// <summary>
/// Application adapter bridging domain swipe detection with OS scroll input injection and OpenCV visual feedback rendering.
/// <para>
/// <b>What it is:</b> The orchestration controller for swipe-to-scroll functionality.
/// </para>
/// </summary>
public class ScrollController
{
    private readonly IInputSink _inputSink;
    private readonly ScrollFeedbackRenderer _renderer;

    /// <summary>
    /// The core domain detector handling swipe analysis and momentum physics.
    /// </summary>
    public ScrollDetector Detector { get; }

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
    /// Initializes a new instance of the <see cref="ScrollController"/> class with an optional injected input sink.
    /// </summary>
    /// <param name="inputSink">The output adapter to receive wheel deltas (defaults to <see cref="Win32InputSink"/> if null).</param>
    /// <param name="detector">Optional custom scroll detector.</param>
    /// <param name="renderer">Optional custom feedback renderer.</param>
    public ScrollController(
        IInputSink? inputSink = null,
        ScrollDetector? detector = null,
        ScrollFeedbackRenderer? renderer = null)
    {
        _inputSink = inputSink ?? new Win32InputSink();
        Detector = detector ?? new ScrollDetector();
        _renderer = renderer ?? new ScrollFeedbackRenderer();
    }

    /// <summary>
    /// Processes ongoing momentum inertia coasting each frame and dispatches resulting wheel movements to the OS.
    /// </summary>
    public void UpdateMomentum()
    {
        ScrollDecision? decision = Detector.UpdateMomentum();
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
        ScrollDecision? decision = Detector.Update(hand);
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
        _renderer.Render(frame, Detector);
    }
}
