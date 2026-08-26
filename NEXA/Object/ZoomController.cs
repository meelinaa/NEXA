using System;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Object;

/// <summary>
/// Controller for continuous hand-gesture zoom magnification and test target rendering.
/// <para>
/// <b>What it is:</b> A gesture-to-scale controller mapping the physical distance between thumb and index fingertips to dynamic zoom factors.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description>Detects the zoom gesture continuum (ranging from closed pinch to wide L-sign with middle/ring/pinky fingers curled).</description></item>
/// <item><description>Calculates normalized aperture ratio: <c>Distance(ThumbTip, IndexTip) / PalmSize</c>.</description></item>
/// <item><description>Scales target zoom relative to the baseline ratio with a +/-3% stillness deadzone, clamped between 0.3x and 3.0x.</description></item>
/// <item><description>Renders an augmented-reality futuristic HUD window with alpha transparency and corner accents.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Why it is used:</b> Provides an intuitive, natural pinch-to-zoom experience without abrupt scale jumps.
/// </para>
/// <para>
/// <b>Consequence:</b> Enables fluid virtual object inspection and camera scaling.
/// </para>
/// </summary>
public class ZoomController
{
    /// <summary>
    /// Gets the internal zoom state machine.
    /// </summary>
    public ZoomState State { get; } = new();

    /// <summary>
    /// Updates the relative zoom scale factor based on the active hand posture.
    /// </summary>
    /// <param name="hand">The tracked hand instance.</param>
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

        // Zoom gesture is active across the continuous span between Pinch Closed and L
        bool isZoomGesture = hand.Gesture == "Pinch Closed" || hand.Gesture == "L" || hand.Gesture.Contains("Zoom");

        double palmSize = hand.Distance(0, 9); // Wrist to middle finger MCP
        double thumbIndexDist = hand.Distance(4, 8); // Thumb tip to index tip
        double ratio = palmSize > 1.0 ? thumbIndexDist / palmSize : 0.25;
        State.LiveRatio = ratio;

        if (!isZoomGesture)
        {
            if (State.Active)
            {
                // Released: Store current zoom level as the new stable anchor for subsequent zoom cycles
                State.Active = false;
                State.LastStableZoom = State.CurrentZoom;
            }
            return;
        }

        if (!State.Active)
        {
            // Enter Zoom Mode: Lock current aperture as the relative baseline
            State.Active = true;
            State.BaselineRatio = Math.Max(0.05, ratio);
            return;
        }

        // Relative zoom ratio compared to starting baseline
        double relativeScale = ratio / State.BaselineRatio;

        // Deadzone: Ignore micro-jitter within +/- 3%
        if (Math.Abs(relativeScale - 1.0) < 0.03)
        {
            relativeScale = 1.0;
        }

        // Apply relative multiplier onto the stored stable zoom level
        double targetZoom = State.LastStableZoom * relativeScale;

        // Clamp magnification between 0.3x (shrink) and 3.0x (enlarge)
        State.CurrentZoom = Math.Clamp(targetZoom, 0.3, 3.0);
    }

    /// <summary>
    /// Resets all zoom factors and baseline states back to default 1.0x.
    /// </summary>
    public void Reset()
    {
        State.Active = false;
        State.BaselineRatio = 1.0;
        State.CurrentZoom = 1.0;
        State.LastStableZoom = 1.0;
        State.LiveRatio = 0.0;
    }

    /// <summary>
    /// Renders the scaled virtual target window with translucent background and corner bracket graphics.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    public void RenderVirtualTarget(Mat frame)
    {
        int centerX = frame.Width - 220;
        int centerY = frame.Height - 180;

        int baseW = 160;
        int baseH = 110;
        int targetW = (int)Math.Round(baseW * State.CurrentZoom);
        int targetH = (int)Math.Round(baseH * State.CurrentZoom);

        int left = centerX - targetW / 2;
        int top = centerY - targetH / 2;

        Rect targetRect = new(
            Math.Max(5, left),
            Math.Max(5, top),
            Math.Min(frame.Width - 10, targetW),
            Math.Min(frame.Height - 10, targetH)
        );

        Scalar themeColor = State.Active ? new Scalar(0, 220, 255) : new Scalar(255, 180, 50);

        // 1. Alpha-blended semi-transparent dark backdrop
        using (Mat overlay = frame.Clone())
        {
            Cv2.Rectangle(overlay, targetRect, new Scalar(20, 20, 30), -1);
            Cv2.AddWeighted(overlay, 0.45, frame, 0.55, 0, frame);
        }

        // 2. Window border & corner accents
        Cv2.Rectangle(frame, targetRect, themeColor, 1, LineTypes.AntiAlias);

        int cornerLen = Math.Min(18, Math.Min(targetRect.Width / 3, targetRect.Height / 3));
        // Draw corner brackets
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

        // 3. Center alignment crosshair
        Cv2.DrawMarker(frame, new Point(centerX, centerY), themeColor, MarkerTypes.Cross, 12, 1);

        // 4. Header title and ratio telemetry
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
