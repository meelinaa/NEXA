using System;
using NEXA.Common;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Undo;

/// <summary>
/// Dedicated AR renderer for Peace wrist-twist Undo/Redo visual feedback (rotary dials, pointer vectors, and action pulse animations).
/// <para>
/// <b>What it is:</b> Viewport visual feedback presenter for gesture Undo/Redo commands.
/// </para>
/// </summary>
public class CircleUndoRenderer
{
    /// <summary>
    /// Renders visual holographic rotary dials, wrist vectors, and trigger feedback animations onto the camera frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="hand">The primary tracked hand.</param>
    /// <param name="state">The circle undo state container.</param>
    public void Render(Mat frame, TrackedHand? hand, CircleUndoState state)
    {
        DateTime now = DateTime.Now;

        // 1. Render Holographic Rotary Dial & Pointer
        if (state.IsTracking && hand != null)
        {
            Point wristPt = new((int)Math.Round(state.WristPos.X), (int)Math.Round(state.WristPos.Y));
            Point tipsPt = new((int)Math.Round(state.FingerTipsPos.X), (int)Math.Round(state.FingerTipsPos.Y));

            int radius = 45;
            Cv2.Circle(frame, wristPt, radius, new Scalar(40, 45, 60), 2, LineTypes.AntiAlias);

            bool isUndo = state.AngleDeltaDeg < -5.0;
            bool isRedo = state.AngleDeltaDeg > 5.0;

            Scalar dialColor = isUndo
                ? new Scalar(0, 220, 255) // Cyan for Undo
                : (isRedo ? new Scalar(255, 160, 0) : new Scalar(0, 255, 120)); // Amber for Redo, Green for Neutral

            // Pointer line from wrist to fingertips
            Cv2.Line(frame, wristPt, tipsPt, dialColor, 2, LineTypes.AntiAlias);
            Cv2.Circle(frame, tipsPt, 6, dialColor, -1, LineTypes.AntiAlias);

            // Floating Dial Badge
            string sign = state.AngleDeltaDeg >= 0 ? "+" : "";
            string actionHint = isUndo ? "UNDO <--" : (isRedo ? "REDO -->" : "DREHEN");
            string badgeText = $"[PEACE: {actionHint} ({sign}{state.AngleDeltaDeg:F0} deg / 42 deg)]";

            Point badgePos = new(Math.Max(10, wristPt.X - 85), Math.Max(25, wristPt.Y - radius - 10));
            Cv2.PutText(frame, TextSanitizer.ToSafeAscii(badgeText), badgePos,
                HersheyFonts.HersheySimplex, 0.40, dialColor, 1, LineTypes.AntiAlias);
        }

        // 2. Action Triggered Pulse Animation
        double elapsedAction = (now - state.LastActionTime).TotalMilliseconds;
        if (elapsedAction < 1000 && !string.IsNullOrEmpty(state.LastAction))
        {
            float progress = (float)(elapsedAction / 1000.0);
            int animRadius = (int)(20 + progress * 55);
            Point center = new((int)state.LastActionCenter.X, (int)state.LastActionCenter.Y);

            Scalar actionColor = state.LastAction == "UNDO"
                ? new Scalar(0, 220, 255) // Cyan
                : new Scalar(255, 160, 0); // Amber

            Cv2.Circle(frame, center, animRadius, actionColor, 3, LineTypes.AntiAlias);

            string actionLabel = state.LastAction == "UNDO" ? "* UNDO (Ctrl+Z) *" : "* REDO (Ctrl+Y) *";
            Cv2.PutText(frame, TextSanitizer.ToSafeAscii(actionLabel), new Point(center.X - 70, center.Y - animRadius - 8),
                HersheyFonts.HersheySimplex, 0.54, actionColor, 2, LineTypes.AntiAlias);
        }
    }
}
