using System;
using NEXA.Common;
using NEXA.Face;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Mute;

/// <summary>
/// Dedicated AR renderer for 4-finger microphone mute visual feedback (mouth reticles, charging progress rings, and mute alert banners).
/// <para>
/// <b>What it is:</b> Viewport visual feedback presenter for microphone mute gestures.
/// </para>
/// </summary>
public class ShhhMuteRenderer
{
    /// <summary>
    /// Renders visual mouth reticles, charging hold progress rings, and microphone mute alert banners onto the camera frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="face">The detected face instance.</param>
    /// <param name="hand">The primary tracked hand.</param>
    /// <param name="state">The microphone mute state container.</param>
    /// <param name="enabled">Indicates whether detection is active.</param>
    public void Render(
        Mat frame,
        TrackedFace? face,
        TrackedHand? hand,
        ShhhMuteState state,
        bool enabled)
    {
        DateTime now = DateTime.Now;

        // 1. Render Charging Progress Ring when 4 fingers enter proximity
        if (face != null && enabled)
        {
            Point mouthPt = new((int)Math.Round(face.MouthCenter.X), (int)Math.Round(face.MouthCenter.Y));
            int mouthR = (int)Math.Round(face.MouthRadius * 1.50);

            if (state.IsInProximity && state.HoldProgress > 0.05)
            {
                int sweepAngle = (int)(state.HoldProgress * 360);
                Scalar chargeColor = new(0, 0, 255); // Vibrant Red
                Cv2.Ellipse(frame, mouthPt, new Size(mouthR, mouthR), -90, 0, sweepAngle, chargeColor, 3, LineTypes.AntiAlias);

                string holdText = $"[4 FINGER MIC: {(int)(state.HoldProgress * 100)}%]";
                Cv2.PutText(frame, TextSanitizer.ToSafeAscii(holdText), new Point(mouthPt.X - 55, mouthPt.Y - mouthR - 8),
                    HersheyFonts.HersheySimplex, 0.38, chargeColor, 1, LineTypes.AntiAlias);
            }
        }

        // 2. Microphone Mute / Unmute State Change Banner Animation
        double elapsedToggle = (now - state.LastToggleTime).TotalMilliseconds;
        if (elapsedToggle < 1200)
        {
            Point mouthCenter = new((int)state.LastMouthCenter.X, (int)state.LastMouthCenter.Y);
            if (mouthCenter.X == 0 && mouthCenter.Y == 0)
            {
                mouthCenter = new Point(frame.Width / 2, frame.Height / 2);
            }

            float progress = (float)(elapsedToggle / 1200.0);
            int animRadius = (int)(30 + progress * 50);

            Scalar bannerColor = state.IsMuted
                ? new Scalar(0, 0, 255) // Red (Muted)
                : new Scalar(0, 255, 120); // Green (Active)

            Cv2.Circle(frame, mouthCenter, animRadius, bannerColor, 2, LineTypes.AntiAlias);

            string statusText = state.IsMuted ? "* MIKROFON STUMM (4 FINGER) *" : "* MIKROFON AKTIV *";
            Cv2.PutText(frame, TextSanitizer.ToSafeAscii(statusText), new Point(mouthCenter.X - 110, mouthCenter.Y - animRadius - 10),
                HersheyFonts.HersheySimplex, 0.52, bannerColor, 2, LineTypes.AntiAlias);
        }
    }
}
