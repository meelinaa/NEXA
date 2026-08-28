using System;
using System.Collections.Generic;
using NEXA.Common;
using NEXA.Face;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.EarsMute;

/// <summary>
/// Dedicated AR renderer for Hear No Evil hands-to-ears audio mute visual feedback (ear charging rings and status banners).
/// <para>
/// <b>What it is:</b> Viewport visual feedback presenter for speaker sound mute gestures.
/// </para>
/// </summary>
public class HearNoEvilRenderer
{
    /// <summary>
    /// Renders AR feedback including dynamic ear charging progress rings and sound mute state change banners.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="face">The detected face instance.</param>
    /// <param name="hands">The active tracked hands.</param>
    /// <param name="state">The hear-no-evil state container.</param>
    /// <param name="enabled">Indicates whether detection is active.</param>
    public void Render(
        Mat frame,
        TrackedFace? face,
        List<TrackedHand> hands,
        HearNoEvilState state,
        bool enabled)
    {
        DateTime now = DateTime.Now;

        // 1. Render Charging Progress Rings around both ears when hands enter ear proximity
        if (face != null && enabled && state.IsInProximity && state.HoldProgress > 0.05)
        {
            int sweepAngle = (int)(state.HoldProgress * 360);
            Scalar chargeColor = new(0, 140, 255); // Vibrant Amber / Orange

            Point leftEarPt = new((int)Math.Round(face.LeftEar.X), (int)Math.Round(face.LeftEar.Y));
            Point rightEarPt = new((int)Math.Round(face.RightEar.X), (int)Math.Round(face.RightEar.Y));
            int earR = (int)Math.Round(face.EarRadius > 10f ? face.EarRadius * 0.75 : 45.0);

            Cv2.Ellipse(frame, leftEarPt, new Size(earR, earR), -90, 0, sweepAngle, chargeColor, 3, LineTypes.AntiAlias);
            Cv2.Ellipse(frame, rightEarPt, new Size(earR, earR), -90, 0, sweepAngle, chargeColor, 3, LineTypes.AntiAlias);

            string holdText = $"[EARS SOUND: {(int)(state.HoldProgress * 100)}%]";
            Cv2.PutText(frame, TextSanitizer.ToSafeAscii(holdText), new Point(leftEarPt.X - 45, leftEarPt.Y - earR - 8),
                HersheyFonts.HersheySimplex, 0.38, chargeColor, 1, LineTypes.AntiAlias);
        }

        // 2. Speaker Sound Mute / Unmute State Change Banner Animation
        double elapsedToggle = (now - state.LastToggleTime).TotalMilliseconds;
        if (elapsedToggle < 1200)
        {
            Scalar bannerColor = state.IsSpeakerMuted
                ? new Scalar(0, 0, 255)   // Red (Speaker Muted)
                : new Scalar(0, 255, 120); // Neon Green (Speaker Unmuted)

            string bannerText = state.IsSpeakerMuted
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
