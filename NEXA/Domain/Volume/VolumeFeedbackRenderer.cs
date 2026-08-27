using System;
using OpenCvSharp;

namespace NEXA.Domain.Volume;

/// <summary>
/// Dedicated AR renderer for gesture rotary volume visual feedback (circular dial track, sweep arc, and volume percentage badge).
/// <para>
/// <b>What it is:</b> Viewport visual feedback presenter for audio volume control.
/// </para>
/// </summary>
public class VolumeFeedbackRenderer
{
    /// <summary>
    /// Renders augmented-reality rotary gauge visuals and live volume percentages onto the camera frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="state">The volume state container.</param>
    public void Render(Mat frame, VolumeState state)
    {
        if (!state.IsActive)
        {
            return;
        }

        Point center = new((int)Math.Round(state.DialCenter.X), (int)Math.Round(state.DialCenter.Y));
        int radius = 46;

        // 1. Background Arc Track (Dark Slate)
        Cv2.Circle(frame, center, radius, new Scalar(40, 45, 60), 4, LineTypes.AntiAlias);

        // 2. Active Volume Level Arc (Cyan / Lime Green)
        float volPercent = Math.Clamp(state.SmoothedVolume, 0.0f, 1.0f);
        int sweepAngle = (int)(volPercent * 360);

        Scalar volColor = volPercent > 0.70f
            ? new Scalar(0, 220, 255) // Gold / Orange for high volume
            : new Scalar(0, 255, 120); // Neon Green for safe volume

        Cv2.Ellipse(frame, center, new Size(radius, radius), -90, 0, sweepAngle, volColor, 5, LineTypes.AntiAlias);

        // 3. Rotary Pointer Line toward Index Tip
        Point indexPt = new((int)Math.Round(state.IndexTip.X), (int)Math.Round(state.IndexTip.Y));
        Cv2.Line(frame, center, indexPt, new Scalar(255, 255, 255), 2, LineTypes.AntiAlias);
        Cv2.Circle(frame, indexPt, 5, volColor, -1, LineTypes.AntiAlias);

        // 4. Floating HUD Tag: Volume Percentage & Tilt Delta
        int volInt = (int)Math.Round(volPercent * 100);
        string sign = state.AngleDelta >= 0 ? "+" : "";
        string tagText = $"VOL: {volInt}% ({sign}{state.AngleDelta:F0} deg)";

        Point tagPos = new(center.X + radius + 10, center.Y + 6);
        Cv2.PutText(frame, tagText, tagPos, HersheyFonts.HersheySimplex, 0.46, volColor, 1, LineTypes.AntiAlias);
    }
}
