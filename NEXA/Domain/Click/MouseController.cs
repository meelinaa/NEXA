using System;
using NEXA.Abstractions;
using NEXA.Adapters.Output;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Click;

/// <summary>
/// Application adapter bridging domain pointer mapping and dwell-clicking with OS mouse events and visual feedback rendering.
/// <para>
/// <b>What it is:</b> The controller responsible for moving the Windows mouse cursor and issuing left clicks based on hand gestures.
/// </para>
/// </summary>
public class MouseController
{
    private readonly IInputSink _inputSink;
    private readonly MouseFeedbackRenderer _renderer;

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
    /// <param name="detector">Optional custom dwell click detector.</param>
    /// <param name="renderer">Optional custom renderer.</param>
    public MouseController(
        IInputSink? inputSink = null,
        DwellClickDetector? detector = null,
        MouseFeedbackRenderer? renderer = null)
    {
        _inputSink = inputSink ?? new Win32InputSink();
        (int width, int height) = _inputSink.GetScreenResolution();
        Detector = detector ?? new DwellClickDetector(width, height);
        _renderer = renderer ?? new MouseFeedbackRenderer();
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
        _renderer.Render(frame, hand, DwellState, Detector.LastClickPosition);
    }
}
