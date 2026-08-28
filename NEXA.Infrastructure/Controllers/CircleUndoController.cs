using System;
using NEXA.Abstractions;
using NEXA.Adapters.Output;
using NEXA.Common;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Undo;

/// <summary>
/// Controller coordinating Peace-sign rotational wrist-twist Undo / Redo gestures with OS keyboard injection and AR visual feedback.
/// <para>
/// <b>What it is:</b> The controller executing Undo (Ctrl+Z) and Redo (Ctrl+Y) when the user twists their wrist with a Peace sign.
/// </para>
/// </summary>
public class CircleUndoController : IHudStatusProvider, IFrameProcessor
{
    private readonly IInputSink _inputSink;
    private readonly CircleUndoRenderer _renderer;

    /// <summary>
    /// The domain-level wrist twist analyzer.
    /// </summary>
    public CircleUndoDetector Detector { get; }

    /// <summary>
    /// Gets or sets a value indicating whether wrist-twist Undo/Redo detection is enabled.
    /// </summary>
    public bool Enabled
    {
        get => Detector.Enabled;
        set => Detector.Enabled = value;
    }

    /// <summary>
    /// Gets the internal state machine from the detector.
    /// </summary>
    public CircleUndoState State => Detector.State;

    /// <summary>
    /// Initializes a new instance of the <see cref="CircleUndoController"/> class.
    /// </summary>
    /// <param name="inputSink">The output adapter for OS inputs.</param>
    /// <param name="detector">Optional custom circle undo detector.</param>
    /// <param name="renderer">Optional custom renderer.</param>
    public CircleUndoController(
        IInputSink? inputSink = null,
        CircleUndoDetector? detector = null,
        CircleUndoRenderer? renderer = null)
    {
        _inputSink = inputSink ?? new Win32InputSink();
        Detector = detector ?? new CircleUndoDetector();
        _renderer = renderer ?? new CircleUndoRenderer();
    }

    /// <summary>
    /// Evaluates tracked hand motion for the current frame and triggers Undo or Redo.
    /// </summary>
    /// <param name="hand">The primary tracked hand.</param>
    public void Update(TrackedHand? hand)
    {
        CircleUndoAction action = Detector.Update(hand);
        if (action == CircleUndoAction.Undo)
        {
            _inputSink.SendUndo();
        }
        else if (action == CircleUndoAction.Redo)
        {
            _inputSink.SendRedo();
        }
    }

    public void Process(FrameContext context)
    {
        if (context.Arbitrator != null && !context.Arbitrator.CanExecute("CircleUndo"))
        {
            return;
        }

        Update(context.PrimaryHand);

        if (State.IsActive)
        {
            context.Arbitrator?.TryAcquire("CircleUndo");
        }
        else
        {
            context.Arbitrator?.Release("CircleUndo");
        }
    }

    /// <summary>
    /// Renders visual holographic rotary dials, wrist vectors, and trigger feedback animations onto the camera frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="hand">The primary tracked hand.</param>
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
        string undoStatus;
        if (!Enabled)
            undoStatus = "AUS (Taste U)";
        else if (State.IsTracking && Math.Abs(State.AngleDeltaDeg) > 5.0)
        {
            string dir = State.AngleDeltaDeg < 0.0 ? "Undo <--" : "Redo -->";
            string sign = State.AngleDeltaDeg >= 0 ? "+" : "";
            undoStatus = $"{dir} ({sign}{State.AngleDeltaDeg:F0} deg / 42 deg)";
        }
        else
            undoStatus = "Bereit (Peace Handgelenk-Dreh)";

        return TextSanitizer.ToSafeAscii($"Undo/Redo (U): {undoStatus}");
    }

    /// <inheritdoc/>
    public Scalar GetStatusColor()
    {
        if (!Enabled) return new Scalar(160, 160, 160);
        if (State.IsTracking && Math.Abs(State.AngleDeltaDeg) > 5.0)
            return State.AngleDeltaDeg < 0.0 ? new Scalar(0, 220, 255) : new Scalar(255, 160, 0);
        return new Scalar(0, 255, 120);
    }
}

