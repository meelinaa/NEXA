using System;
using OpenCvSharp;

namespace NEXA;

public class ZoomState
{
    public bool Active { get; set; } = false;
    public double BaselineRatio { get; set; } = 1.0;
    public double CurrentZoom { get; set; } = 1.0;
    public double LastStableZoom { get; set; } = 1.0;
    public double LiveRatio { get; set; } = 0.0;
}

public class ZoomController
{
    public ZoomState State { get; } = new();

    public void Update(TrackedHand? hand)
    {
        if (hand == null)
        {
            if (State.Active)
            {
                State.Active = false;
                State.LastStableZoom = State.CurrentZoom;
            }
            return;
        }

        // Zoom is active in the entire range between Pinch Closed and L (middle, ring, pinky folded)
        bool isZoomGesture = hand.Gesture == "Pinch Closed" || hand.Gesture == "L" || hand.Gesture.Contains("Zoom");

        double palmSize = hand.Distance(0, 9); // Wrist to middle MCP
        double thumbIndexDist = hand.Distance(4, 8); // Thumb tip to index tip
        double ratio = palmSize > 1.0 ? thumbIndexDist / palmSize : 0.25;
        State.LiveRatio = ratio;

        if (!isZoomGesture)
        {
            if (State.Active)
            {
                // Released: save current zoom level as new baseline for subsequent zoom gestures
                State.Active = false;
                State.LastStableZoom = State.CurrentZoom;
            }
            return;
        }

        if (!State.Active)
        {
            // Transition -> Zoom Mode Active
            State.Active = true;
            State.BaselineRatio = Math.Max(0.05, ratio);
            return;
        }

        // Relative zoom ratio
        double relativeScale = ratio / State.BaselineRatio;

        // Deadzone: ignore micro-jitter within +/- 3%
        if (Math.Abs(relativeScale - 1.0) < 0.03)
        {
            relativeScale = 1.0;
        }

        // Apply relative scale onto the last stable zoom
        double targetZoom = State.LastStableZoom * relativeScale;

        // Clamp between 0.3x and 3.0x
        State.CurrentZoom = Math.Clamp(targetZoom, 0.3, 3.0);
    }

    public void Reset()
    {
        State.Active = false;
        State.BaselineRatio = 1.0;
        State.CurrentZoom = 1.0;
        State.LastStableZoom = 1.0;
        State.LiveRatio = 0.0;
    }

    public void RenderVirtualTarget(Mat frame)
    {
        // Center position for virtual test rectangle
        int centerX = frame.Width - 220;
        int centerY = frame.Height - 180;

        // Base size 160x110 scaled by CurrentZoom
        int baseW = 160;
        int baseH = 110;
        int targetW = (int)Math.Round(baseW * State.CurrentZoom);
        int targetH = (int)Math.Round(baseH * State.CurrentZoom);

        int left = centerX - targetW / 2;
        int top = centerY - targetH / 2;

        var targetRect = new Rect(
            Math.Max(5, left),
            Math.Max(5, top),
            Math.Min(frame.Width - 10, targetW),
            Math.Min(frame.Height - 10, targetH)
        );

        // Highlight color: Cyan when idle, Vibrant Yellow/Amber when pinching
        var themeColor = State.Active ? new Scalar(0, 220, 255) : new Scalar(255, 180, 50);

        // 1. Semi-transparent backdrop for virtual test window
        using (var overlay = frame.Clone())
        {
            Cv2.Rectangle(overlay, targetRect, new Scalar(20, 20, 30), -1);
            Cv2.AddWeighted(overlay, 0.45, frame, 0.55, 0, frame);
        }

        // 2. Window Border & Corner Accents
        Cv2.Rectangle(frame, targetRect, themeColor, 1, LineTypes.AntiAlias);

        int cornerLen = Math.Min(18, Math.Min(targetRect.Width / 3, targetRect.Height / 3));
        if (cornerLen > 4)
        {
            Cv2.Line(frame, new Point(targetRect.Left, targetRect.Top), new Point(targetRect.Left + cornerLen, targetRect.Top), themeColor, 2);
            Cv2.Line(frame, new Point(targetRect.Left, targetRect.Top), new Point(targetRect.Left, targetRect.Top + cornerLen), themeColor, 2);

            Cv2.Line(frame, new Point(targetRect.Right, targetRect.Top), new Point(targetRect.Right - cornerLen, targetRect.Top), themeColor, 2);
            Cv2.Line(frame, new Point(targetRect.Right, targetRect.Top), new Point(targetRect.Right, targetRect.Top + cornerLen), themeColor, 2);

            Cv2.Line(frame, new Point(targetRect.Left, targetRect.Bottom), new Point(targetRect.Left + cornerLen, targetRect.Bottom), themeColor, 2);
            Cv2.Line(frame, new Point(targetRect.Left, targetRect.Bottom), new Point(targetRect.Left, targetRect.Bottom - cornerLen), themeColor, 2);

            Cv2.Line(frame, new Point(targetRect.Right, targetRect.Bottom), new Point(targetRect.Right - cornerLen, targetRect.Bottom), themeColor, 2);
            Cv2.Line(frame, new Point(targetRect.Right, targetRect.Bottom), new Point(targetRect.Right, targetRect.Bottom - cornerLen), themeColor, 2);
        }

        // 3. Center Crosshair
        Cv2.DrawMarker(frame, new Point(centerX, centerY), themeColor, MarkerTypes.Cross, 12, 1);

        // 4. Header Bar / Title
        string title = State.Active ? $"VIRTUAL TARGET [PINCH: {State.CurrentZoom:F2}x]" : $"VIRTUAL TARGET [{State.CurrentZoom:F2}x]";
        Cv2.PutText(frame, title, new Point(Math.Max(10, targetRect.Left + 8), Math.Max(25, targetRect.Top + 20)),
            HersheyFonts.HersheySimplex, 0.42, new Scalar(255, 255, 255), 1, LineTypes.AntiAlias);

        string subText = State.Active
            ? $"Rel: {(State.LiveRatio / Math.Max(0.01, State.BaselineRatio)):F2}x (Base: {State.BaselineRatio:F2})"
            : "Pinch to zoom (R: Reset)";

        Cv2.PutText(frame, subText, new Point(Math.Max(10, targetRect.Left + 8), Math.Max(42, targetRect.Top + 38)),
            HersheyFonts.HersheySimplex, 0.35, new Scalar(200, 200, 200), 1, LineTypes.AntiAlias);
    }
}
