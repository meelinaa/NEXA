using NEXA.Abstractions;
using NEXA.Adapters.Output;
using NEXA.Face;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Mute;

/// <summary>
/// Application adapter orchestrating 4-finger mute gesture evaluation, microphone hardware input mute toggling, and holographic face/mouth reticle rendering.
/// <para>
/// <b>What it is:</b> The controller executing microphone input mute toggles when the user places 4 fingers in front of their mouth.
/// </para>
/// </summary>
public class ShhhMuteController
{
    private readonly IAudioSink _audioSink;
    private readonly ShhhMuteRenderer _renderer;

    /// <summary>
    /// The domain-level spatial posture detector.
    /// </summary>
    public ShhhMuteDetector Detector { get; }

    /// <summary>
    /// Gets or sets a value indicating whether 4-finger mute detection is active.
    /// </summary>
    public bool Enabled
    {
        get => Detector.Enabled;
        set => Detector.Enabled = value;
    }

    /// <summary>
    /// Gets the internal state machine from the detector.
    /// </summary>
    public ShhhMuteState State => Detector.State;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShhhMuteController"/> class.
    /// </summary>
    /// <param name="audioSink">The audio hardware output sink.</param>
    /// <param name="detector">Optional custom detector.</param>
    /// <param name="renderer">Optional custom renderer.</param>
    public ShhhMuteController(
        IAudioSink? audioSink = null,
        ShhhMuteDetector? detector = null,
        ShhhMuteRenderer? renderer = null)
    {
        _audioSink = audioSink ?? new Win32AudioSink();
        Detector = detector ?? new ShhhMuteDetector();
        _renderer = renderer ?? new ShhhMuteRenderer();
    }

    /// <summary>
    /// Evaluates hand and face telemetry for the current frame and toggles microphone mute when the hold duration is satisfied.
    /// </summary>
    /// <param name="hand">The primary tracked hand.</param>
    /// <param name="face">The detected face instance.</param>
    public void Update(TrackedHand? hand, TrackedFace? face)
    {
        bool shouldToggle = Detector.Update(hand, face);
        if (shouldToggle)
        {
            _audioSink.ToggleMicrophoneMute();
            State.IsMuted = _audioSink.IsMicrophoneMuted();
        }
    }

    /// <summary>
    /// Renders visual mouth reticles, charging hold progress rings, and microphone mute alert banners onto the camera frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="face">The detected face instance.</param>
    /// <param name="hand">The primary tracked hand.</param>
    public void RenderFeedback(Mat frame, TrackedFace? face, TrackedHand? hand)
    {
        _renderer.Render(frame, face, hand, State, Enabled);
    }
}
