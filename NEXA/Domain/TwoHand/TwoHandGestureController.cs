using System.Collections.Generic;
using NEXA.Abstractions;
using NEXA.Adapters.Output;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.TwoHand;

/// <summary>
/// Application controller orchestrating two-hand Maximize/Minimize window operations, Camera-Frame Screenshot capture, and Clap/Prayer Media Play/Pause.
/// <para>
/// <b>What it is:</b> The high-level application coordinator managing detection, command execution, and visual rendering for multi-hand interactions.
/// </para>
/// </summary>
public class TwoHandGestureController
{
    private readonly IInputSink _inputSink;
    private readonly TwoHandActionExecutor _actionExecutor;
    private readonly TwoHandGestureRenderer _renderer;

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
    public string OutputDirectory
    {
        get => _actionExecutor.OutputDirectory;
        set => _actionExecutor.OutputDirectory = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TwoHandGestureController"/> class.
    /// </summary>
    /// <param name="inputSink">The output adapter for OS inputs.</param>
    /// <param name="screenshotSink">The output adapter for desktop screen capture.</param>
    /// <param name="detector">Optional custom detector instance.</param>
    /// <param name="actionExecutor">Optional custom action executor.</param>
    /// <param name="renderer">Optional custom renderer instance.</param>
    public TwoHandGestureController(
        IInputSink? inputSink = null,
        IScreenshotSink? screenshotSink = null,
        TwoHandGestureDetector? detector = null,
        TwoHandActionExecutor? actionExecutor = null,
        TwoHandGestureRenderer? renderer = null)
    {
        _inputSink = inputSink ?? new Win32InputSink();
        _actionExecutor = actionExecutor ?? new TwoHandActionExecutor(_inputSink, screenshotSink);
        Detector = detector ?? new TwoHandGestureDetector();
        _renderer = renderer ?? new TwoHandGestureRenderer();
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
            _actionExecutor.Execute(decision, State);
        }
    }

    /// <summary>
    /// Renders visual feedback (3.0s window countdown badge, camera viewfinder brackets, white flash, and action animations) onto the camera frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="hands">The list of active tracked hands.</param>
    public void RenderFeedback(Mat frame, List<TrackedHand>? hands)
    {
        _renderer.Render(frame, State, _inputSink, hands);
    }
}
