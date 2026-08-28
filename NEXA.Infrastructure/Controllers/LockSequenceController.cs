using System.Collections.Generic;
using NEXA.Abstractions;
using NEXA.Adapters.Output;
using NEXA.Common;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Lock;

/// <summary>
/// Application adapter orchestrating dual-hand multi-stage security gesture evaluation, Windows OS session locking, and augmented reality milestone HUD rendering.
/// <para>
/// <b>What it is:</b> The controller executing PC lock commands upon completion of the Open-Fist-Open-Fist sequence.
/// </para>
/// </summary>
public class LockSequenceController : IHudStatusProvider, IFrameProcessor
{
    private readonly IInputSink _inputSink;
    private readonly LockSequenceRenderer _renderer;

    /// <summary>
    /// The domain-level temporal sequence detector.
    /// </summary>
    public LockSequenceDetector Detector { get; }

    /// <summary>
    /// Gets or sets a value indicating whether lock sequence detection is enabled.
    /// </summary>
    public bool Enabled
    {
        get => Detector.Enabled;
        set => Detector.Enabled = value;
    }

    /// <summary>
    /// Gets the internal state machine from the detector.
    /// </summary>
    public LockSequenceState State => Detector.State;

    /// <summary>
    /// Initializes a new instance of the <see cref="LockSequenceController"/> class.
    /// </summary>
    /// <param name="inputSink">The output adapter for OS inputs.</param>
    /// <param name="detector">Optional custom lock detector.</param>
    /// <param name="renderer">Optional custom lock renderer.</param>
    public LockSequenceController(
        IInputSink? inputSink = null,
        LockSequenceDetector? detector = null,
        LockSequenceRenderer? renderer = null)
    {
        _inputSink = inputSink ?? new Win32InputSink();
        Detector = detector ?? new LockSequenceDetector();
        _renderer = renderer ?? new LockSequenceRenderer();
    }

    /// <summary>
    /// Evaluates the tracked hands for the current frame and executes a workstation lock if the 4-step dual-hand sequence completes.
    /// </summary>
    /// <param name="hands">The active tracked hands (must have at least 2 hands).</param>
    public void Update(List<TrackedHand>? hands)
    {
        bool shouldLock = Detector.Update(hands);
        if (shouldLock)
        {
            _inputSink.LockWorkstation();
        }
    }

    public void Process(FrameContext context)
    {
        if (context.Arbitrator != null && !context.Arbitrator.CanExecute("LockSequence"))
        {
            State.Reset();
            return;
        }

        Update(context.TrackedHands);

        if (State.CurrentStep != LockSequenceStep.Idle)
        {
            context.Arbitrator?.TryAcquire("LockSequence");
        }
        else
        {
            context.Arbitrator?.Release("LockSequence");
        }
    }

    /// <summary>
    /// Renders visual sequence milestones and countdown progress bars onto the camera frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="hands">The active tracked hands.</param>
    public void RenderFeedback(Mat frame, List<TrackedHand>? hands)
    {
        _renderer.Render(frame, hands, State);
    }

    /// <inheritdoc/>
    public void Render(FrameContext context)
    {
        RenderFeedback(context.Frame, context.TrackedHands);
    }

    /// <inheritdoc/>
    public string GetStatusText()
    {
        string lockStatus;
        if (!Enabled)
            lockStatus = "AUS (Taste L)";
        else if (State.CurrentStep != LockSequenceStep.Idle)
            lockStatus = $"Sequenz: {(int)State.CurrentStep}/4 ({State.StepTimeoutSeconds - State.StepTimer.Elapsed.TotalSeconds:F1}s)";
        else
            lockStatus = "Bereit (Offen-Faust-Offen-Faust)";

        return TextSanitizer.ToSafeAscii($"Sperren (L): {lockStatus}");
    }

    /// <inheritdoc/>
    public Scalar GetStatusColor()
    {
        if (!Enabled) return new Scalar(160, 160, 160);
        if (State.CurrentStep != LockSequenceStep.Idle) return new Scalar(0, 220, 255);
        return new Scalar(0, 255, 120);
    }
}

