using System;
using System.Collections.Generic;
using NEXA.Adapters.Output;
using NEXA.Common;
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
    public HearNoEvilController(IAudioSink audioSink, HearNoEvilDetector? detector = null)
    {
        _audioSink = audioSink ?? throw new ArgumentNullException(nameof(audioSink));
        Detector = detector ?? new HearNoEvilDetector();
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

            Console.ForegroundColor = State.IsSpeakerMuted ? ConsoleColor.Red : ConsoleColor.Green;
            Console.WriteLine($"\n[AUDIO TOGGLE] Hear No Evil (Hands to Ears) -> Master Speaker Output {(State.IsSpeakerMuted ? "MUTED 🔇" : "UNMUTED 🔊")}\n");
            Console.ResetColor();
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
        DateTime now = DateTime.Now;

        // 1. Render Charging Progress Rings around both ears when hands enter ear proximity
        if (face != null && Enabled && State.IsInProximity && State.HoldProgress > 0.05)
        {
            int sweepAngle = (int)(State.HoldProgress * 360);
            Scalar chargeColor = new(0, 140, 255); // Vibrant Amber / Orange

            Point leftEarPt = new((int)Math.Round(face.LeftEar.X), (int)Math.Round(face.LeftEar.Y));
            Point rightEarPt = new((int)Math.Round(face.RightEar.X), (int)Math.Round(face.RightEar.Y));
            int earR = (int)Math.Round(face.EarRadius > 10f ? face.EarRadius * 0.75 : 45.0);

            Cv2.Ellipse(frame, leftEarPt, new Size(earR, earR), -90, 0, sweepAngle, chargeColor, 3, LineTypes.AntiAlias);
            Cv2.Ellipse(frame, rightEarPt, new Size(earR, earR), -90, 0, sweepAngle, chargeColor, 3, LineTypes.AntiAlias);

            string holdText = $"[EARS SOUND: {(int)(State.HoldProgress * 100)}%]";
            Cv2.PutText(frame, TextSanitizer.ToSafeAscii(holdText), new Point(leftEarPt.X - 45, leftEarPt.Y - earR - 8),
                HersheyFonts.HersheySimplex, 0.38, chargeColor, 1, LineTypes.AntiAlias);
        }

        // 2. Speaker Sound Mute / Unmute State Change Banner Animation
        double elapsedToggle = (now - State.LastToggleTime).TotalMilliseconds;
        if (elapsedToggle < 1200)
        {
            float progress = (float)(elapsedToggle / 1200.0);
            Scalar bannerColor = State.IsSpeakerMuted
                ? new Scalar(0, 0, 255)   // Red (Speaker Muted)
                : new Scalar(0, 255, 120); // Neon Green (Speaker Unmuted)

            string bannerText = State.IsSpeakerMuted
                ? "[SPEAKER SOUND MUTED (EARS GESTURE)]"
                : "[SPEAKER SOUND UNMUTED (EARS GESTURE)]";

            int bannerY = 85;
            int boxWidth = 360;
            int boxHeight = 36;
            int boxX = (frame.Width - boxWidth) / 2;

            using Mat overlay = frame.Clone();
            Cv2.Rectangle(overlay, new Rect(boxX, bannerY, boxWidth, boxHeight), new Scalar(15, 15, 20), -1);
            Cv2.AddWeighted(overlay, 0.70, frame, 0.30, 0, frame);
            Cv2.Rectangle(frame, new Rect(boxX, bannerY, boxWidth, boxHeight), bannerColor, 2, LineTypes.AntiAlias);

            Cv2.PutText(
                frame,
                TextSanitizer.ToSafeAscii(bannerText),
                new Point(boxX + 16, bannerY + 24),
                HersheyFonts.HersheySimplex,
                0.48,
                bannerColor,
                1,
                LineTypes.AntiAlias
            );
        }
    }
}
