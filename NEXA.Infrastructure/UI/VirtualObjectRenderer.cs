using System;
using NEXA.Domain.Grab;
using OpenCvSharp;

namespace NEXA.Object;

/// <summary>
/// Dedicated AR renderer responsible for drawing holographic 2D virtual test objects with dynamic state badges, corner brackets, and crosshairs.
/// <para>
/// <b>What it is:</b> Visual feedback presenter for virtual object manipulation.
/// </para>
/// </summary>
public class VirtualObjectRenderer
{
    /// <summary>
    /// Renders the virtual test object window with alpha blending, corner accents, and telemetry text onto the frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="target">The virtual test object containing position and dimensions.</param>
    /// <param name="grabState">The grab state machine tracking hold and active state.</param>
    /// <param name="resizeState">The resize state machine tracking continuous magnification.</param>
    public void Render(
        Mat frame,
        TestObject target,
        GrabState grabState,
        WindowResizeState resizeState)
    {
        double scale = resizeState.IsActive && resizeState.CurrentScale > 0 ? resizeState.CurrentScale : 1.0;
        int targetW = (int)Math.Round(target.BaseWidth * scale);
        int targetH = (int)Math.Round(target.BaseHeight * scale);

        int left = (int)Math.Round(target.X - targetW / 2.0);
        int top = (int)Math.Round(target.Y - targetH / 2.0);

        Rect rect = new(
            Math.Max(2, left),
            Math.Max(2, top),
            Math.Min(frame.Width - 4, targetW),
            Math.Min(frame.Height - 4, targetH)
        );

        Scalar themeColor;
        string statusTag;

        if (grabState.Active)
        {
            themeColor = new Scalar(0, 100, 255); // Glowing Orange / Red
            statusTag = "[GRABBED - MOVING]";
        }
        else if (grabState.HoldDurationSeconds > 0)
        {
            themeColor = new Scalar(0, 165, 255); // Amber (Holding Countdown)
            double remaining = Math.Max(0, grabState.RequiredHoldTime - grabState.HoldDurationSeconds);
            statusTag = $"[HOLD: {remaining:F1}s]";
        }
        else if (resizeState.IsActive)
        {
            themeColor = new Scalar(0, 230, 255); // Glowing Gold
            statusTag = $"[ZOOM: {scale:F2}x]";
        }
        else
        {
            themeColor = new Scalar(255, 180, 50); // Futuristic Cyan
            statusTag = $"[IDLE: {scale:F2}x]";
        }

        // 1. Semi-transparent backdrop
        using (Mat overlay = frame.Clone())
        {
            Cv2.Rectangle(overlay, rect, new Scalar(18, 18, 26), -1);
            Cv2.AddWeighted(overlay, 0.40, frame, 0.60, 0, frame);
        }

        // 2. Corner Bracket Accents
        Cv2.Rectangle(frame, rect, themeColor, 1, LineTypes.AntiAlias);
        int cornerLen = Math.Min(20, Math.Min(rect.Width / 3, rect.Height / 3));

        if (cornerLen > 4)
        {
            Cv2.Line(frame, new Point(rect.Left, rect.Top), new Point(rect.Left + cornerLen, rect.Top), themeColor, 2);
            Cv2.Line(frame, new Point(rect.Left, rect.Top), new Point(rect.Left + cornerLen, rect.Top), themeColor, 2);

            Cv2.Line(frame, new Point(rect.Right, rect.Top), new Point(rect.Right - cornerLen, rect.Top), themeColor, 2);
            Cv2.Line(frame, new Point(rect.Right, rect.Top), new Point(rect.Right, rect.Top + cornerLen), themeColor, 2);

            Cv2.Line(frame, new Point(rect.Left, rect.Bottom), new Point(rect.Left + cornerLen, rect.Bottom), themeColor, 2);
            Cv2.Line(frame, new Point(rect.Left, rect.Bottom), new Point(rect.Left + cornerLen, rect.Bottom), themeColor, 2);

            Cv2.Line(frame, new Point(rect.Right, rect.Bottom), new Point(rect.Right - cornerLen, rect.Bottom), themeColor, 2);
            Cv2.Line(frame, new Point(rect.Right, rect.Bottom), new Point(rect.Right - cornerLen, rect.Bottom), themeColor, 2);
        }

        // 3. Center Crosshair
        Cv2.DrawMarker(frame, new Point((int)target.X, (int)target.Y), themeColor, MarkerTypes.Cross, 12, 1);

        // 4. Header Bar / Title
        string title = $"TEST WINDOW {statusTag}";
        Cv2.PutText(frame, title, new Point(Math.Max(8, rect.Left + 8), Math.Max(22, rect.Top + 20)),
            HersheyFonts.HersheySimplex, 0.40, new Scalar(255, 255, 255), 1, LineTypes.AntiAlias);

        string subText;
        if (grabState.Active)
        {
            subText = $"Pos: ({(int)target.X}, {(int)target.Y})";
        }
        else if (grabState.HoldDurationSeconds > 0)
        {
            subText = $"Fist hold countdown: {grabState.HoldDurationSeconds:F1}s";
        }
        else
        {
            subText = "Fist (2s): Grab | Pinch-L: Zoom";
        }

        Cv2.PutText(frame, subText, new Point(Math.Max(8, rect.Left + 8), Math.Max(40, rect.Top + 38)),
            HersheyFonts.HersheySimplex, 0.35, new Scalar(200, 200, 200), 1, LineTypes.AntiAlias);
    }
}
