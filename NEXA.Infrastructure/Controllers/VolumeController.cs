using NEXA.Abstractions;
using NEXA.Adapters.Output;
using NEXA.Common;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Volume;

/// <summary>
/// Application adapter orchestrating continuous system master volume adjustments and holographic AR rotary dial rendering.
/// <para>
/// <b>What it is:</b> The controller managing audio level manipulation via hand tilt.
/// </para>
/// </summary>
public class VolumeController : IHudStatusProvider, IFrameProcessor
{
    private readonly IAudioSink _audioSink;
    private readonly VolumeFeedbackRenderer _renderer;

    /// <summary>
    /// The core domain detector evaluating L-gesture posture and angular tilt.
    /// </summary>
    public VolumeDetector Detector { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether volume control is enabled.
    /// </summary>
    public bool Enabled
    {
        get => Detector.Enabled;
        set => Detector.Enabled = value;
    }

    /// <summary>
    /// Gets the internal volume state machine.
    /// </summary>
    public VolumeState State => Detector.State;

    /// <summary>
    /// Initializes a new instance of the <see cref="VolumeController"/> class.
    /// </summary>
    /// <param name="audioSink">The audio adapter for OS master volume (defaults to <see cref="Win32AudioSink"/> if null).</param>
    /// <param name="detector">Optional custom volume detector.</param>
    /// <param name="renderer">Optional custom volume renderer.</param>
    public VolumeController(
        IAudioSink? audioSink = null,
        VolumeDetector? detector = null,
        VolumeFeedbackRenderer? renderer = null)
    {
        _audioSink = audioSink ?? new Win32AudioSink();
        Detector = detector ?? new VolumeDetector();
        _renderer = renderer ?? new VolumeFeedbackRenderer();
    }

    /// <summary>
    /// Evaluates tracked hand orientation and applies continuous volume adjustments to the OS.
    /// </summary>
    /// <param name="hand">The tracked hand instance.</param>
    public void Update(TrackedHand? hand)
    {
        (bool isActive, float targetVolume) = Detector.Update(hand, _audioSink);
        if (isActive)
        {
            _audioSink.SetMasterVolume(targetVolume);
        }
    }

    /// <inheritdoc/>
    public void Process(FrameContext context)
    {
        Update(context.PrimaryHand);
    }

    /// <summary>
    /// Renders augmented-reality rotary gauge visuals and live volume percentages onto the camera frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    public void RenderFeedback(Mat frame)
    {
        _renderer.Render(frame, State);
    }

    /// <inheritdoc/>
    public void Render(FrameContext context)
    {
        RenderFeedback(context.Frame);
    }

    /// <inheritdoc/>
    public string GetStatusText()
    {
        string volStatus;
        if (!Enabled)
            volStatus = "AUS (Taste V)";
        else if (State.IsActive)
            volStatus = $"Aktiv: {(int)(State.SmoothedVolume * 100)}% ({State.AngleDelta:+0;-0;0} deg)";
        else
            volStatus = "Bereit (L-Geste + Drehung)";

        return TextSanitizer.ToSafeAscii($"Lautstaerke (V): {volStatus}");
    }

    /// <inheritdoc/>
    public Scalar GetStatusColor() =>
        Enabled ? new Scalar(0, 255, 120) : new Scalar(160, 160, 160);
}

