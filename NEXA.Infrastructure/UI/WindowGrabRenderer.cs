using System;
using NEXA.Common;
using OpenCvSharp;

namespace NEXA.Domain.Grab;

/// <summary>
/// Dedicated AR feedback visualizer for real OS window grabbing, countdown rings, docking bounds, and pinch calipers.
/// <para>
/// <b>What it is:</b> Viewport rendering engine for window manipulation gestures.
/// </para>
/// </summary>
public class WindowGrabRenderer
{
    /// <summary>
    /// Renders augmented-reality visual feedback (hold countdown ring, scaled corner brackets, snap preview zones, and pinch caliper) onto the camera frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="state">The window grab state container.</param>
    /// <param name="resizeState">The window resize state container.</param>
    /// <param name="detector">The window grab detector for screen-to-camera coordinate projection.</param>
    public void Render(
        Mat frame,
        WindowGrabState state,
        WindowResizeState resizeState,
        WindowGrabDetector detector)
    {
        // 1. Holding Countdown Ring around Palm Center (before grab activates)
        if (!state.IsGrabbed && state.HoldDurationSeconds > 0.1)
        {
            double progress = Math.Clamp(state.HoldDurationSeconds / state.RequiredHoldSeconds, 0.0, 1.0);
            int radius = 32;
            int angle = (int)(progress * 360);

            Point pt = new((int)Math.Round(state.LastPalmCenter.X), (int)Math.Round(state.LastPalmCenter.Y));

            Cv2.Circle(frame, pt, radius, new Scalar(40, 40, 55), 2, LineTypes.AntiAlias);
            Cv2.Ellipse(frame, pt, new Size(radius, radius), -90, 0, angle, new Scalar(0, 165, 255), 3, LineTypes.AntiAlias);

            double remaining = Math.Max(0, state.RequiredHoldSeconds - state.HoldDurationSeconds);
            string holdText = $"HOLD {remaining:F1}s";
            Cv2.PutText(frame, holdText, new Point(pt.X + 38, pt.Y + 5),
                HersheyFonts.HersheySimplex, 0.42, new Scalar(0, 165, 255), 1, LineTypes.AntiAlias);
        }

        // 2. Grabbed Window Corner-Bracket / Snap Dock Overlay
        if (state.IsGrabbed && state.InitialWindowBounds.Width > 0 && state.InitialWindowBounds.Height > 0)
        {
            int currentWinW = state.IsSnapped
                ? state.SnapBounds.Width
                : (resizeState.IsActive && resizeState.CurrentWidth > 0 ? resizeState.CurrentWidth : state.InitialWindowBounds.Width);

            int currentWinH = state.IsSnapped
                ? state.SnapBounds.Height
                : (resizeState.IsActive && resizeState.CurrentHeight > 0 ? resizeState.CurrentHeight : state.InitialWindowBounds.Height);

            int currentWinX = state.IsSnapped ? state.SnapBounds.X : state.CurrentTargetX;
            int currentWinY = state.IsSnapped ? state.SnapBounds.Y : state.CurrentTargetY;

            // Project Top-Left and Bottom-Right desktop bounds back to camera pixel coordinates
            (float camLeft, float camTop) = detector.MapFromScreen(currentWinX, currentWinY, frame.Width, frame.Height);
            (float camRight, float camBottom) = detector.MapFromScreen(
                currentWinX + currentWinW,
                currentWinY + currentWinH,
                frame.Width, frame.Height);

            int x = (int)Math.Round(camLeft);
            int y = (int)Math.Round(camTop);
            int w = (int)Math.Round(camRight - camLeft);
            int h = (int)Math.Round(camBottom - camTop);

            Rect boxRect = new(
                Math.Clamp(x, 2, frame.Width - 10),
                Math.Clamp(y, 2, frame.Height - 10),
                Math.Clamp(w, 20, frame.Width - 4),
                Math.Clamp(h, 20, frame.Height - 4)
            );

            Scalar themeColor = state.IsSnapped
                ? new Scalar(255, 160, 0) // Vibrant Azure Blue
                : (resizeState.IsActive ? new Scalar(0, 220, 255) : new Scalar(0, 100, 255));

            // Translucent backdrop
            using (Mat overlay = frame.Clone())
            {
                Cv2.Rectangle(overlay, boxRect, new Scalar(15, 15, 25), -1);
                Cv2.AddWeighted(overlay, state.IsSnapped ? 0.45 : 0.35, frame, state.IsSnapped ? 0.55 : 0.65, 0, frame);
            }

            // Outer rectangle & corner brackets
            Cv2.Rectangle(frame, boxRect, themeColor, state.IsSnapped ? 2 : 1, LineTypes.AntiAlias);
            int cornerLen = Math.Min(25, Math.Min(boxRect.Width / 4, boxRect.Height / 4));

            if (cornerLen > 4)
            {
                Cv2.Line(frame, new Point(boxRect.Left, boxRect.Top), new Point(boxRect.Left + cornerLen, boxRect.Top), themeColor, 2);
                Cv2.Line(frame, new Point(boxRect.Left, boxRect.Top), new Point(boxRect.Left + cornerLen, boxRect.Top), themeColor, 2);

                Cv2.Line(frame, new Point(boxRect.Right, boxRect.Top), new Point(boxRect.Right - cornerLen, boxRect.Top), themeColor, 2);
                Cv2.Line(frame, new Point(boxRect.Right, boxRect.Top), new Point(boxRect.Right, boxRect.Top + cornerLen), themeColor, 2);

                Cv2.Line(frame, new Point(boxRect.Left, boxRect.Bottom), new Point(boxRect.Left + cornerLen, boxRect.Bottom), themeColor, 2);
                Cv2.Line(frame, new Point(boxRect.Left, boxRect.Bottom), new Point(boxRect.Left + cornerLen, boxRect.Bottom), themeColor, 2);

                Cv2.Line(frame, new Point(boxRect.Right, boxRect.Bottom), new Point(boxRect.Right - cornerLen, boxRect.Bottom), themeColor, 2);
                Cv2.Line(frame, new Point(boxRect.Right, boxRect.Bottom), new Point(boxRect.Right - cornerLen, boxRect.Bottom), themeColor, 2);
            }

            // Window metadata header badge
            string rawTitle = TextSanitizer.ToSafeAscii(state.CachedWindowTitle);
            string titleDisplay = rawTitle.Length > 18
                ? rawTitle.Substring(0, 15) + "..."
                : rawTitle;

            string tagText;
            if (state.IsSnapped)
            {
                string snapName = state.ActiveSnap switch
                {
                    WindowSnapType.LeftHalf => "SNAP LEFT (50%)",
                    WindowSnapType.RightHalf => "SNAP RIGHT (50%)",
                    WindowSnapType.TopHalf => "SNAP TOP (50%)",
                    WindowSnapType.BottomHalf => "SNAP BOTTOM (50%)",
                    WindowSnapType.TopLeftCorner => "SNAP TOP-LEFT (25%)",
                    WindowSnapType.TopRightCorner => "SNAP TOP-RIGHT (25%)",
                    WindowSnapType.BottomLeftCorner => "SNAP BOTTOM-LEFT (25%)",
                    WindowSnapType.BottomRightCorner => "SNAP BOTTOM-RIGHT (25%)",
                    _ => "DOCKED"
                };
                tagText = $"[{snapName}] [{titleDisplay}]";
            }
            else if (resizeState.IsActive)
            {
                tagText = $"RESIZING: [{titleDisplay}] {currentWinW}x{currentWinH} ({resizeState.CurrentScale:F2}x)";
            }
            else
            {
                tagText = $"GRABBED: [{titleDisplay}] ({(int)state.CurrentTargetX}, {(int)state.CurrentTargetY})";
            }

            Cv2.PutText(frame, TextSanitizer.ToSafeAscii(tagText), new Point(Math.Max(10, boxRect.Left + 8), Math.Max(25, boxRect.Top + 20)),
                HersheyFonts.HersheySimplex, 0.44, new Scalar(255, 255, 255), 1, LineTypes.AntiAlias);
        }

        // 3. Pinch-to-Scale Measurement Caliper on Resizing Hand
        if (!state.IsSnapped && resizeState.IsActive)
        {
            Point pThumb = new((int)resizeState.ThumbTip.X, (int)resizeState.ThumbTip.Y);
            Point pIndex = new((int)resizeState.IndexTip.X, (int)resizeState.IndexTip.Y);

            Cv2.Line(frame, pThumb, pIndex, new Scalar(0, 220, 255), 2, LineTypes.AntiAlias);
            Cv2.Circle(frame, pThumb, 6, new Scalar(0, 255, 120), -1, LineTypes.AntiAlias);
            Cv2.Circle(frame, pIndex, 6, new Scalar(0, 255, 120), -1, LineTypes.AntiAlias);

            Point midPt = new((pThumb.X + pIndex.X) / 2, (pThumb.Y + pIndex.Y) / 2);
            string scaleLabel = $"SCALE: {resizeState.CurrentScale:F2}x";
            Cv2.PutText(frame, scaleLabel, new Point(midPt.X + 12, midPt.Y),
                HersheyFonts.HersheySimplex, 0.44, new Scalar(0, 220, 255), 1, LineTypes.AntiAlias);
        }
    }
}
