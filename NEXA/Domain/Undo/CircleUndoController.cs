using NEXA.Adapters.Output;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Undo;

/// <summary>
/// Application adapter orchestrating Peace-sign wrist-twist gesture evaluation, Undo/Redo keyboard injection, and holographic AR rotary dial rendering.
/// <para>
/// <b>What it is:</b> The controller executing Undo (Ctrl+Z) and Redo (Ctrl+Y) when the user twists their wrist with a Peace sign (✌️).
/// </para>
/// </summary>
public class CircleUndoController
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

    /// <summary>
    /// Renders visual holographic rotary dials, wrist vectors, and trigger feedback animations onto the camera frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="hand">The primary tracked hand.</param>
    public void RenderFeedback(Mat frame, TrackedHand? hand)
    {
        _renderer.Render(frame, hand, State);
    }
}
