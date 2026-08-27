using System;
using System.Collections.Generic;
using NEXA.Abstractions;
using NEXA.Adapters.Output;
using NEXA.Face;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.EarsMute;

/// <summary>
/// Controller coordinating the "Hear No Evil" 🙉 hands-to-ears speaker audio mute gesture with OS audio sinks and AR visual feedback.
/// <para>
/// <b>What it is:</b> Application service linking spatial face/hand ear-proximity detection to Windows master speaker sound muting.
/// </para>
/// </summary>
public class HearNoEvilController
{
    private readonly IAudioSink _audioSink;
    private readonly HearNoEvilRenderer _renderer;

    /// <summary>
    /// Gets the domain detector evaluating hands at the ears.
    /// </summary>
    public HearNoEvilDetector Detector { get; }

    /// <summary>
    /// Gets the underlying state machine.
    /// </summary>
    public HearNoEvilState State => Detector.State;

    /// <summary>
    /// Gets or sets a value indicating whether the Hear No Evil gesture is enabled.
    /// </summary>
    public bool Enabled
    {
        get => Detector.Enabled;
        set => Detector.Enabled = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HearNoEvilController"/> class.
    /// </summary>
    /// <param name="audioSink">The audio hardware output port.</param>
    /// <param name="detector">Optional custom detector instance.</param>
    /// <param name="renderer">Optional custom renderer instance.</param>
    public HearNoEvilController(
        IAudioSink audioSink,
        HearNoEvilDetector? detector = null,
        HearNoEvilRenderer? renderer = null)
    {
        _audioSink = audioSink ?? throw new ArgumentNullException(nameof(audioSink));
        Detector = detector ?? new HearNoEvilDetector();
        _renderer = renderer ?? new HearNoEvilRenderer();
        State.IsSpeakerMuted = _audioSink.IsMuted();
    }

    /// <summary>
    /// Evaluates the tracked hands and face for the current frame and executes master audio speaker mute/unmute when triggered.
    /// </summary>
    /// <param name="hands">The active tracked hands.</param>
    /// <param name="face">The detected face.</param>
    public void Update(List<TrackedHand> hands, TrackedFace? face)
    {
        if (Detector.Update(hands, face))
        {
            // Toggle Windows Master Speaker Volume Mute
            _audioSink.ToggleMute();
            State.IsSpeakerMuted = _audioSink.IsMuted();
            State.LastToggleTime = DateTime.Now;
        }
    }

    /// <summary>
    /// Renders AR feedback including dynamic ear charging progress rings and sound mute state change banners.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="face">The detected face instance.</param>
    /// <param name="hands">The active tracked hands.</param>
    public void RenderFeedback(Mat frame, TrackedFace? face, List<TrackedHand> hands)
    {
        _renderer.Render(frame, face, hands, State, Enabled);
    }
}
