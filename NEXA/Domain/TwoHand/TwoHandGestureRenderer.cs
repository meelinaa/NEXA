using System;
using System.Collections.Generic;
using NEXA.Abstractions;
using NEXA.Adapters.Output;
using NEXA.Common;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.TwoHand;

/// <summary>
/// Dedicated AR renderer for two-hand interaction visuals: camera viewfinder brackets, countdown charging arcs, white flash overlays, and action pulse animations.
/// <para>
/// <b>What it is:</b> Visual feedback presenter for two-hand gesture interactions.
/// </para>
/// </summary>
public class TwoHandGestureRenderer
{
    /// <summary>
    /// Renders visual feedback (3.0s window countdown badge, camera viewfinder brackets, white flash, and action animations) onto the camera frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="state">The two-hand gesture state container.</param>
    /// <param name="inputSink">The output adapter holding focused window title.</param>
    /// <param name="hands">The list of active tracked hands.</param>
    public void Render(Mat frame, TwoHandGestureState state, IInputSink inputSink, List<TrackedHand>? hands)
    {
        DateTime now = DateTime.Now;

        // 1. Live Camera-Frame Viewfinder Bounding Box (Spanned by dual "L" hands)
        if (state.IsCameraFrameActive && state.LiveCameraFrameRect.Width > 0 && state.LiveCameraFrameRect.Height > 0)
        {
            Rect box = new(
                Math.Clamp((int)Math.Round(state.LiveCameraFrameRect.X), 2, frame.Width - 10),
                Math.Clamp((int)Math.Round(state.LiveCameraFrameRect.Y), 2, frame.Height - 10),
                Math.Clamp((int)Math.Round(state.LiveCameraFrameRect.Width), 20, frame.Width - 4),
                Math.Clamp((int)Math.Round(state.LiveCameraFrameRect.Height), 20, frame.Height - 4)
            );

            Scalar frameColor = new(0, 255, 120); // Neon Green
            int cornerLen = Math.Min(30, Math.Min(box.Width / 4, box.Height / 4));

            // Translucent dark fill
            using (Mat overlay = frame.Clone())
            {
                Cv2.Rectangle(overlay, box, new Scalar(20, 30, 20), -1);
                Cv2.AddWeighted(overlay, 0.25, frame, 0.75, 0, frame);
            }

            // Outer border
            Cv2.Rectangle(frame, box, frameColor, 1, LineTypes.AntiAlias);

            // 4 Viewfinder Corner Brackets
            Cv2.Line(frame, new Point(box.Left, box.Top), new Point(box.Left + cornerLen, box.Top), frameColor, 3);
            Cv2.Line(frame, new Point(box.Left, box.Top), new Point(box.Left, box.Top + cornerLen), frameColor, 3);

            Cv2.Line(frame, new Point(box.Right, box.Top), new Point(box.Right - cornerLen, box.Top), frameColor, 3);
            Cv2.Line(frame, new Point(box.Right, box.Top), new Point(box.Right, box.Top + cornerLen), frameColor, 3);

            Cv2.Line(frame, new Point(box.Left, box.Bottom), new Point(box.Left + cornerLen, box.Bottom), frameColor, 3);
            Cv2.Line(frame, new Point(box.Left, box.Bottom), new Point(box.Left, box.Bottom - cornerLen), frameColor, 3);

            Cv2.Line(frame, new Point(box.Right, box.Bottom), new Point(box.Right - cornerLen, box.Bottom), frameColor, 3);
            Cv2.Line(frame, new Point(box.Right, box.Bottom), new Point(box.Right, box.Bottom - cornerLen), frameColor, 3);

            // Center crosshair
            Point centerPt = new(box.Left + box.Width / 2, box.Top + box.Height / 2);
            Cv2.Line(frame, new Point(centerPt.X - 10, centerPt.Y), new Point(centerPt.X + 10, centerPt.Y), frameColor, 1);
            Cv2.Line(frame, new Point(centerPt.X, centerPt.Y - 10), new Point(centerPt.X, centerPt.Y + 10), frameColor, 1);

            string tagText;
            Scalar tagColor;

            if (state.ScreenshotHoldProgress > 0.01)
            {
                double remaining = Math.Max(0.0, state.RequiredScreenshotHoldSeconds - state.ScreenshotHoldDurationSeconds);
                tagText = $"[HALTE: {remaining:F1}s ({(int)(state.ScreenshotHoldProgress * 100)}%)]";
                tagColor = new Scalar(0, 220, 255); // Cyan

                int arcAngle = (int)(state.ScreenshotHoldProgress * 360);
                Cv2.Ellipse(frame, centerPt, new Size(24, 24), -90, 0, arcAngle, tagColor, 3, LineTypes.AntiAlias);
            }
            else
            {
                tagText = "[KAMERA-RAHMEN: FINGER 2s ZUSAMMENHALTEN]";
                tagColor = frameColor;
            }

            Cv2.PutText(frame, tagText, new Point(Math.Max(10, box.Left + 6), Math.Max(25, box.Top - 8)),
                HersheyFonts.HersheySimplex, 0.40, tagColor, 1, LineTypes.AntiAlias);
        }

        // 2. White Flash Overlay Animation (~220ms on screenshot trigger)
        double elapsedFlash = (now - state.LastScreenshotTime).TotalMilliseconds;
        if (elapsedFlash < 220 && state.LastCapturedFrameRect.Width > 0)
        {
            Rect flashBox = new(
                Math.Clamp((int)Math.Round(state.LastCapturedFrameRect.X), 2, frame.Width - 10),
                Math.Clamp((int)Math.Round(state.LastCapturedFrameRect.Y), 2, frame.Height - 10),
                Math.Clamp((int)Math.Round(state.LastCapturedFrameRect.Width), 20, frame.Width - 4),
                Math.Clamp((int)Math.Round(state.LastCapturedFrameRect.Height), 20, frame.Height - 4)
            );

            double alpha = Math.Clamp(1.0 - (elapsedFlash / 220.0), 0.0, 1.0) * 0.70;
            using (Mat flashOverlay = frame.Clone())
            {
                Cv2.Rectangle(flashOverlay, flashBox, new Scalar(255, 255, 255), -1);
                Cv2.AddWeighted(flashOverlay, alpha, frame, 1.0 - alpha, 0, frame);
            }

            Cv2.Rectangle(frame, flashBox, new Scalar(255, 255, 255), 2, LineTypes.AntiAlias);
            Cv2.PutText(frame, "* SCREENSHOT SAVED & COPIED *", new Point(flashBox.Left + 10, flashBox.Top + flashBox.Height / 2),
                HersheyFonts.HersheySimplex, 0.52, new Scalar(0, 255, 255), 2, LineTypes.AntiAlias);
        }

        // 3. Active 3.0s Window Status Banner (Top Center)
        if (state.IsWindowActive && inputSink.LastFocusedHwnd != IntPtr.Zero)
        {
            string rawTitle = TextSanitizer.ToSafeAscii(inputSink.LastFocusedTitle);
            string winTitle = rawTitle.Length > 20
                ? rawTitle.Substring(0, 17) + "..."
                : rawTitle;

            string bannerText = TextSanitizer.ToSafeAscii($"[2-HAND READY ({state.RemainingWindowSeconds:F1}s): {winTitle}]");
            Size textSize = Cv2.GetTextSize(bannerText, HersheyFonts.HersheySimplex, 0.44, 1, out _);

            int bannerX = Math.Max(10, (frame.Width - textSize.Width) / 2);
            Rect bannerRect = new(bannerX - 10, 12, textSize.Width + 20, textSize.Height + 14);

            using (Mat bannerMat = frame.Clone())
            {
                Cv2.Rectangle(bannerMat, bannerRect, new Scalar(20, 20, 30), -1);
                Cv2.AddWeighted(bannerMat, 0.75, frame, 0.25, 0, frame);
            }

            Cv2.Rectangle(frame, bannerRect, new Scalar(0, 255, 120), 1, LineTypes.AntiAlias);
            Cv2.PutText(frame, bannerText, new Point(bannerX, 28),
                HersheyFonts.HersheySimplex, 0.44, new Scalar(0, 255, 120), 1, LineTypes.AntiAlias);
        }

        // 4. Touch Link Line (Maximize Gesture Indicator)
        if (state.IsTouchActive)
        {
            Point p1 = new((int)state.TouchPoint1.X, (int)state.TouchPoint1.Y);
            Point p2 = new((int)state.TouchPoint2.X, (int)state.TouchPoint2.Y);

            Cv2.Line(frame, p1, p2, new Scalar(0, 220, 255), 2, LineTypes.AntiAlias);
            Cv2.Circle(frame, p1, 6, new Scalar(0, 255, 120), -1, LineTypes.AntiAlias);
            Cv2.Circle(frame, p2, 6, new Scalar(0, 255, 120), -1, LineTypes.AntiAlias);

            Point midPt = new((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
            Cv2.PutText(frame, "SPREAD APART TO MAXIMIZE", new Point(midPt.X - 80, midPt.Y - 14),
                HersheyFonts.HersheySimplex, 0.38, new Scalar(0, 220, 255), 1, LineTypes.AntiAlias);
        }

        // 5. Trigger Flash & Action Animation Banner
        double elapsedFeedback = (now - state.LastFeedbackTime).TotalMilliseconds;
        if (elapsedFeedback < 900 && !string.IsNullOrEmpty(state.LastAction))
        {
            float progress = (float)(elapsedFeedback / 900.0);
            int animRadius = (int)(20 + progress * 50);
            Point center = new((int)state.LastFeedbackCenter.X, (int)state.LastFeedbackCenter.Y);

            Scalar actionColor;
            if (state.LastAction == "MAXIMIZE")
            {
                actionColor = new Scalar(0, 255, 120); // Green
            }
            else if (state.LastAction == "SCREENSHOT")
            {
                actionColor = new Scalar(255, 255, 255); // White
            }
            else if (state.LastAction == "PLAY / PAUSE")
            {
                actionColor = new Scalar(0, 220, 255); // Cyan
            }
            else
            {
                actionColor = new Scalar(0, 100, 255); // Red / Orange
            }

            Cv2.Circle(frame, center, animRadius, actionColor, 2, LineTypes.AntiAlias);

            string actionLabel = state.LastAction == "PLAY / PAUSE" ? "> || [PLAY / PAUSE]" : $"* {state.LastAction} *";
            Cv2.PutText(frame, TextSanitizer.ToSafeAscii(actionLabel), new Point(center.X - 60, center.Y - animRadius - 8),
                HersheyFonts.HersheySimplex, 0.60, actionColor, 2, LineTypes.AntiAlias);
        }
    }
}
