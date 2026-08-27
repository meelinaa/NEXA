using System;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.MonitorThrow;

/// <summary>
/// Dedicated AR renderer for multi-monitor window transfer visual feedback (edge-on blade line highlights and animated transfer arrows).
/// <para>
/// <b>What it is:</b> Viewport visual feedback presenter for cross-display window relocations.
/// </para>
/// </summary>
public class MonitorThrowRenderer
{
    /// <summary>
    /// Renders edge-on posture indicators and holographic monitor transfer arrows onto the camera frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="hand">The active tracked hand.</param>
    /// <param name="state">The monitor throw state container.</param>
    public void Render(Mat frame, TrackedHand? hand, MonitorThrowState state)
    {
        // 1. Edge-On Blade Hand Highlight Indicator
        if (state.IsEdgeOnPosture && hand != null)
        {
            Point pWrist = new((int)hand.SmoothedLandmarks2D[0].X, (int)hand.SmoothedLandmarks2D[0].Y);
            Point pPinky = new((int)hand.SmoothedLandmarks2D[20].X, (int)hand.SmoothedLandmarks2D[20].Y);

            Cv2.Line(frame, pWrist, pPinky, new Scalar(255, 100, 200), 3, LineTypes.AntiAlias);
            Cv2.Circle(frame, pPinky, 5, new Scalar(0, 255, 255), -1, LineTypes.AntiAlias);

            Point tagPt = new((pWrist.X + pPinky.X) / 2 + 15, (pWrist.Y + pPinky.Y) / 2);
            Cv2.PutText(frame, "BLADE (MONITOR THROW)", tagPt,
                HersheyFonts.HersheySimplex, 0.40, new Scalar(255, 100, 200), 1, LineTypes.AntiAlias);
        }

        // 2. Holographic Monitor Transfer Animation
        double elapsed = (DateTime.Now - state.LastFeedbackTime).TotalMilliseconds;
        if (elapsed < 600.0)
        {
            float progress = (float)(elapsed / 600.0);
            int cx = (int)state.LastSwipeCenter.X;
            int cy = (int)state.LastSwipeCenter.Y;

            bool isRight = state.LastDirection == "RIGHT";
            int spread = (int)(progress * 70);
            Scalar arrowColor = new(0, 255, 255); // Vibrant Yellow/Cyan

            string transferText = isRight
                ? "==> MONITOR [Right] ==>"
                : "<== MONITOR [Left] <==";

            int textX = isRight ? cx + spread - 30 : cx - spread - 100;
            Cv2.PutText(frame, transferText, new Point(Math.Clamp(textX, 10, frame.Width - 250), Math.Max(35, cy)),
                HersheyFonts.HersheySimplex, 0.65, arrowColor, 2, LineTypes.AntiAlias);

            if (isRight)
            {
                Cv2.ArrowedLine(frame, new Point(cx, cy + 20), new Point(cx + spread + 40, cy + 20), arrowColor, 3);
            }
            else
            {
                Cv2.ArrowedLine(frame, new Point(cx, cy + 20), new Point(cx - spread - 40, cy + 20), arrowColor, 3);
            }
        }
    }
}
