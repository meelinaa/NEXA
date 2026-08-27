using System;
using NEXA.Adapters.Output;
using NEXA.Common;
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
    public ShhhMuteController(IAudioSink? audioSink = null)
    {
        _audioSink = audioSink ?? new Win32AudioSink();
        Detector = new ShhhMuteDetector();
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
        DateTime now = DateTime.Now;

        // 1. Render Charging Progress Ring when 4 fingers enter proximity
        if (face != null && Enabled)
        {
            Point mouthPt = new((int)Math.Round(face.MouthCenter.X), (int)Math.Round(face.MouthCenter.Y));
            int mouthR = (int)Math.Round(face.MouthRadius * 1.50);

            // Charging progress ring when 4 fingers enter proximity
            if (State.IsInProximity && State.HoldProgress > 0.05)
            {
                int sweepAngle = (int)(State.HoldProgress * 360);
                Scalar chargeColor = new(0, 0, 255); // Vibrant Red
                Cv2.Ellipse(frame, mouthPt, new Size(mouthR, mouthR), -90, 0, sweepAngle, chargeColor, 3, LineTypes.AntiAlias);

                string holdText = $"[4 FINGER MIC: {(int)(State.HoldProgress * 100)}%]";
                Cv2.PutText(frame, TextSanitizer.ToSafeAscii(holdText), new Point(mouthPt.X - 55, mouthPt.Y - mouthR - 8),
                    HersheyFonts.HersheySimplex, 0.38, chargeColor, 1, LineTypes.AntiAlias);
            }
        }

        // 2. Microphone Mute / Unmute State Change Banner Animation
        double elapsedToggle = (now - State.LastToggleTime).TotalMilliseconds;
        if (elapsedToggle < 1200)
        {
            Point mouthCenter = new((int)State.LastMouthCenter.X, (int)State.LastMouthCenter.Y);
            if (mouthCenter.X == 0 && mouthCenter.Y == 0)
            {
                mouthCenter = new Point(frame.Width / 2, frame.Height / 2);
            }

            float progress = (float)(elapsedToggle / 1200.0);
            int animRadius = (int)(30 + progress * 50);

            Scalar bannerColor = State.IsMuted
                ? new Scalar(0, 0, 255) // Red (Muted)
                : new Scalar(0, 255, 120); // Green (Active)

            Cv2.Circle(frame, mouthCenter, animRadius, bannerColor, 2, LineTypes.AntiAlias);

            string statusText = State.IsMuted ? "* MIKROFON STUMM (4 FINGER) *" : "* MIKROFON AKTIV *";
            Cv2.PutText(frame, TextSanitizer.ToSafeAscii(statusText), new Point(mouthCenter.X - 110, mouthCenter.Y - animRadius - 10),
                HersheyFonts.HersheySimplex, 0.52, bannerColor, 2, LineTypes.AntiAlias);
        }
    }
}
