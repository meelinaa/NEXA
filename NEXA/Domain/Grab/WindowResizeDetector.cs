using System;
using NEXA.Hand;

namespace NEXA.Domain.Grab;

/// <summary>
/// Domain-level analyzer computing stepless continuous window scaling factors from secondary hand pinch/aperture gestures.
/// <para>
/// <b>What it is:</b> A continuous spatial dimension analyzer translating hand aperture into physical OS window resizing.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="number">
/// <item><description>Extracts the normalized Euclidean distance between Thumb tip (4) and Index tip (8) divided by Palm size (0-9).</description></item>
/// <item><description>Locks a baseline aperture ratio on gesture initiation and computes a relative multiplier.</description></item>
/// <item><description>Applies a +/-3.5% deadzone filter to eliminate tremor at stationary hand positions.</description></item>
/// <item><description>Smooths dimension deltas with an adaptive exponential filter and clamps to valid desktop monitor limits.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Why it is used:</b> Enables fluid, natural two-hand manipulation where one hand holds the window and the other resizes it in real time.
/// </para>
/// </summary>
public class WindowResizeDetector
{
    /// <summary>
    /// Gets the internal state container tracking aperture ratios and smoothed dimensions.
    /// </summary>
    public WindowResizeState State { get; } = new();

    /// <summary>
    /// Evaluates hand tracking data for the secondary hand to compute new window width and height.
    /// </summary>
    /// <param name="zoomHand">The secondary tracked hand performing the aperture/pinch gesture.</param>
    /// <param name="baseWidth">The original native window width at grab inception.</param>
    /// <param name="baseHeight">The original native window height at grab inception.</param>
    /// <param name="screenWidth">Primary desktop monitor width in pixels.</param>
    /// <param name="screenHeight">Primary desktop monitor height in pixels.</param>
    /// <returns>A tuple containing a boolean indicating if resizing is active and the target (newWidth, newHeight).</returns>
    public (bool shouldResize, int newWidth, int newHeight) Update(
        TrackedHand? zoomHand,
        int baseWidth,
        int baseHeight,
        int screenWidth,
        int screenHeight)
    {
        if (zoomHand == null || baseWidth <= 0 || baseHeight <= 0)
        {
            Reset();
            return (false, 0, 0);
        }

        double palmSize = zoomHand.Distance(0, 9);
        double thumbIndexDist = zoomHand.Distance(4, 8);
        double ratio = palmSize > 1.0 ? thumbIndexDist / palmSize : 0.25;

        State.LiveRatio = ratio;
        State.ThumbTip = zoomHand.SmoothedLandmarks2D[4];
        State.IndexTip = zoomHand.SmoothedLandmarks2D[8];

        string gesture = zoomHand.Gesture;
        bool isZoomGesture = gesture == "Pinch" || gesture == "Pinch Closed" || gesture == "L" ||
                             gesture == "Pointing" || gesture == "Tracking" || gesture.Contains("Zoom");

        if (!isZoomGesture)
        {
            if (State.IsActive)
            {
                State.IsActive = false;
                State.LastStableScale = State.CurrentScale;
            }
            return (false, 0, 0);
        }

        // On initial zoom engagement: establish baseline ratio and window geometry
        if (!State.IsActive)
        {
            State.IsActive = true;
            State.BaselineRatio = Math.Max(0.08, ratio);
            State.BaseWidth = baseWidth;
            State.BaseHeight = baseHeight;

            if (!State.HasInitializedSmoothing)
            {
                State.SmoothedWidth = baseWidth;
                State.SmoothedHeight = baseHeight;
                State.HasInitializedSmoothing = true;
            }
            return (false, baseWidth, baseHeight);
        }

        double relativeScale = ratio / State.BaselineRatio;

        // Deadzone: Ignore micro-fluctuations within +/- 3.5%
        if (Math.Abs(relativeScale - 1.0) < 0.035)
        {
            relativeScale = 1.0;
        }

        double targetScale = Math.Clamp(State.LastStableScale * relativeScale, 0.25, 3.5);
        double rawTargetWidth = State.BaseWidth * targetScale;
        double rawTargetHeight = State.BaseHeight * targetScale;

        // Clamp to sensible physical window bounds (min 320x240, max monitor dimensions)
        double clampedWidth = Math.Clamp(rawTargetWidth, 320.0, screenWidth);
        double clampedHeight = Math.Clamp(rawTargetHeight, 240.0, screenHeight);

        // Adaptive exponential smoothing for jitter-free window boundary scaling
        double diffW = clampedWidth - State.SmoothedWidth;
        double diffH = clampedHeight - State.SmoothedHeight;
        double diffMag = Math.Sqrt(diffW * diffW + diffH * diffH);

        if (diffMag > 4.0)
        {
            double alpha = Math.Clamp(0.18 + (diffMag / 200.0) * 0.50, 0.18, 0.75);
            State.SmoothedWidth += diffW * alpha;
            State.SmoothedHeight += diffH * alpha;
        }

        State.CurrentWidth = (int)Math.Round(State.SmoothedWidth);
        State.CurrentHeight = (int)Math.Round(State.SmoothedHeight);
        State.CurrentScale = targetScale;

        return (true, State.CurrentWidth, State.CurrentHeight);
    }

    /// <summary>
    /// Resets active zoom interaction states and re-aligns baseline metrics.
    /// </summary>
    public void Reset()
    {
        State.IsActive = false;
        State.BaselineRatio = 1.0;
        State.CurrentScale = 1.0;
        State.LastStableScale = 1.0;
        State.LiveRatio = 0.0;
        State.HasInitializedSmoothing = false;
    }
}
