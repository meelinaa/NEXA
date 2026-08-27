using System;
using OpenCvSharp;

namespace NEXA.Domain.Scroll;

/// <summary>
/// Dedicated AR renderer for gesture swipe-to-scroll visual feedback (floating animated arrows, velocity themes, and slope telemetry).
/// <para>
/// <b>What it is:</b> Visual feedback presenter for scroll interactions.
/// </para>
/// </summary>
public class ScrollFeedbackRenderer
{
    /// <summary>
    /// Renders floating animated swipe arrows and real-time regression slope text onto the camera frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="detector">The scroll detector containing telemetry and feedback state.</param>
    public void Render(Mat frame, ScrollDetector detector)
    {
        double elapsed = (DateTime.Now - detector.LastFeedbackTime).TotalMilliseconds;
        if (elapsed < 550)
        {
            float progress = (float)(elapsed / 550.0);

            int x = (int)detector.LastSwipePoint.X;
            int y = (int)detector.LastSwipePoint.Y;

            bool isUp = detector.LastSwipeDirection == "UP";
            Scalar color = detector.LastInitialVelocity >= 100
                ? new Scalar(0, 100, 255)  // High velocity: Orange/Red
                : new Scalar(0, 240, 255); // Normal velocity: Cyan/Yellow

            int offset = (int)(progress * 40);
            int drawY = isUp ? y - offset : y + offset;

            string arrowText = isUp
                ? $"^ SCROLL UP (Slope: {detector.State.LastSlope:+0.00;-0.00;0.00})"
                : $"v SCROLL DOWN (Slope: {detector.State.LastSlope:+0.00;-0.00;0.00})";

            Cv2.PutText(frame, arrowText, new Point(Math.Max(10, x - 85), Math.Max(30, drawY)),
                HersheyFonts.HersheySimplex, 0.52, color, 2, LineTypes.AntiAlias);
        }
    }
}
