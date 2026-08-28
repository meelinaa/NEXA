using System;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Click;

/// <summary>
/// Dedicated AR renderer responsible for visual feedback of mouse cursor dwell charging rings and click ripple flashes.
/// <para>
/// <b>What it is:</b> Viewport visual feedback presenter for gesture mouse pointing and dwell clicking.
/// </para>
/// </summary>
public class MouseFeedbackRenderer
{
    /// <summary>
    /// Renders visual feedback (radial charging arc and click ripple flash) onto the camera frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="hand">The currently tracked hand.</param>
    /// <param name="dwellState">The dwell click state container.</param>
    /// <param name="lastClickPosition">The camera coordinate of the most recent click.</param>
    public void Render(
        Mat frame,
        TrackedHand? hand,
        DwellClickState dwellState,
        Point2f lastClickPosition)
    {
        if (hand == null)
        {
            return;
        }

        Point2f indexTip = hand.SmoothedLandmarks2D[8];
        Point pt = new((int)Math.Round(indexTip.X), (int)Math.Round(indexTip.Y));

        // 1. Dwell-Click Radial Charge Ring
        if (dwellState.IsHovering && dwellState.HoverProgress > 0.05)
        {
            int radius = 22;
            int angle = (int)(dwellState.HoverProgress * 360);

            Cv2.Circle(frame, pt, radius, new Scalar(60, 60, 80), 2, LineTypes.AntiAlias);

            Scalar arcColor = dwellState.HoverProgress > 0.7
                ? new Scalar(0, 255, 120) // Green
                : new Scalar(0, 220, 255); // Cyan

            Cv2.Ellipse(frame, pt, new Size(radius, radius), -90, 0, angle, arcColor, 3, LineTypes.AntiAlias);
            Cv2.Circle(frame, pt, 4, arcColor, -1, LineTypes.AntiAlias);

            string pct = $"{(int)(dwellState.HoverProgress * 100)}%";
            Cv2.PutText(frame, pct, new Point(pt.X + 26, pt.Y + 5),
                HersheyFonts.HersheySimplex, 0.40, arcColor, 1, LineTypes.AntiAlias);
        }

        // 2. Click Animation (Expanding ripple flash on dispatch)
        double elapsedSinceClick = (DateTime.Now - dwellState.LastClickTime).TotalMilliseconds;
        if (elapsedSinceClick < 400)
        {
            float rippleProgress = (float)(elapsedSinceClick / 400.0);
            int rippleRadius = (int)(12 + rippleProgress * 40);
            Point clickPt = new((int)Math.Round(lastClickPosition.X), (int)Math.Round(lastClickPosition.Y));

            Cv2.Circle(frame, clickPt, rippleRadius, new Scalar(0, 255, 255), 2, LineTypes.AntiAlias);
            Cv2.PutText(frame, "* CLICK *", new Point(clickPt.X - 28, clickPt.Y - rippleRadius - 6),
                HersheyFonts.HersheySimplex, 0.52, new Scalar(0, 255, 255), 1, LineTypes.AntiAlias);
        }
    }
}
