using System.Collections.Generic;
using NEXA.Abstractions;
using OpenCvSharp;

namespace NEXA.UI;

/// <summary>
/// Renderer responsible for drawing the semi-transparent telemetry HUD card with live FPS, filter states, and dynamic status indicators.
/// <para>
/// <b>What it is:</b> Decoupled visual telemetry HUD presenter querying <see cref="IHudStatusProvider"/> abstractions.
/// </para>
/// </summary>
public class HudRenderer
{
    /// <summary>
    /// Renders the complete telemetry HUD card onto the specified camera image frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="fps">The current measured frames per second.</param>
    /// <param name="handsCount">Number of currently detected tracked hands.</param>
    /// <param name="smoothed">Whether OneEuroFilter smoothing is active.</param>
    /// <param name="statusProviders">Collection of status line providers decoupled via <see cref="IHudStatusProvider"/>.</param>
    public void Render(
        Mat frame,
        double fps,
        int handsCount,
        bool smoothed,
        IEnumerable<IHudStatusProvider> statusProviders)
    {
        Rect hudRect = new(10, 10, 390, 256);
        using Mat overlay = frame.Clone();
        Cv2.Rectangle(overlay, hudRect, new Scalar(10, 10, 15), -1);
        Cv2.AddWeighted(overlay, 0.7, frame, 0.3, 0, frame);
        Cv2.Rectangle(frame, hudRect, new Scalar(0, 220, 255), 1);

        Cv2.PutText(frame, "NEXA HAND MOUSE & WINDOWS (ONNX)", new Point(20, 28),
            HersheyFonts.HersheySimplex, 0.48, new Scalar(0, 255, 255), 1, LineTypes.AntiAlias);

        Cv2.PutText(frame, $"FPS: {fps:F1} | Hands: {handsCount} | Filter: {(smoothed ? "ON" : "OFF")}", new Point(20, 48),
            HersheyFonts.HersheySimplex, 0.36, new Scalar(255, 255, 255), 1, LineTypes.AntiAlias);

        int y = 66;
        foreach (IHudStatusProvider provider in statusProviders)
        {
            string text = provider.GetStatusText();
            Scalar color = provider.GetStatusColor();
            double fontScale = y >= 246 ? 0.34 : 0.36;

            Cv2.PutText(frame, text, new Point(20, y),
                HersheyFonts.HersheySimplex, fontScale, color, 1, LineTypes.AntiAlias);

            y += 18;
        }
    }
}
