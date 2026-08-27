using System;
using NEXA.Hand;

namespace NEXA.Object;

/// <summary>
/// Domain engine calculating continuous hand-gesture pinch-to-zoom magnification factors for virtual test objects.
/// <para>
/// <b>What it is:</b> Continuous aperture ratio tracker and optical scale factor calculator.
/// </para>
/// </summary>
public class VirtualObjectZoomEngine
{
    /// <summary>
    /// Gets the internal zoom state machine.
    /// </summary>
    public ZoomState State { get; } = new();

    /// <summary>
    /// Updates the relative zoom scale factor based on the active hand posture.
    /// </summary>
    /// <param name="hand">The tracked hand instance.</param>
    /// <param name="isGrabActive">Indicates whether a grab drag interaction is currently active (suppresses zoom).</param>
    public void UpdateZoom(TrackedHand hand, bool isGrabActive)
    {
        if (isGrabActive)
            return;

        bool isZoomGesture = hand.Gesture == "Pinch Closed" || hand.Gesture == "L" || hand.Gesture.Contains("Zoom");

        double palmSize = hand.Distance(0, 9);
        double thumbIndexDist = hand.Distance(4, 8);
        double ratio = palmSize > 1.0 ? thumbIndexDist / palmSize : 0.25;
        State.LiveRatio = ratio;

        if (!isZoomGesture)
        {
            if (State.Active)
            {
                State.Active = false;
                State.LastStableZoom = State.CurrentZoom;
            }
            return;
        }

        if (!State.Active)
        {
            State.Active = true;
            State.BaselineRatio = Math.Max(0.05, ratio);
            return;
        }

        double relativeScale = ratio / State.BaselineRatio;

        // Deadzone: Ignore minor drifts within +/- 3%
        if (Math.Abs(relativeScale - 1.0) < 0.03)
        {
            relativeScale = 1.0;
        }

        double targetZoom = State.LastStableZoom * relativeScale;
        State.CurrentZoom = Math.Clamp(targetZoom, 0.3, 3.0);
    }

    /// <summary>
    /// Resets active zoom state when hand tracking is lost.
    /// </summary>
    public void HandleNoHand()
    {
        if (State.Active)
        {
            State.Active = false;
            State.LastStableZoom = State.CurrentZoom;
        }
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
}
