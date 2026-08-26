using System;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Undo;

/// <summary>
/// Domain-level analyzer evaluating the Peace-sign wrist-twist gesture (✌️) to execute Undo and Redo actions.
/// <para>
/// <b>What it is:</b> An angular wrist orientation analyzer tracking rotational wrist tilts like turning a key (🔑).
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="number">
/// <item><description>Detects strict Peace posture (Index and Middle extended in V-shape; other fingers curled).</description></item>
/// <item><description>Locks the initial orientation baseline upon gesture entry.</description></item>
/// <item><description>Evaluates intentional wrist tilt within a relaxed 3.0-second interactive window:</description></item>
/// <item><description>Tilt Left &le; -42&deg; (↺) &rarr; <see cref="CircleUndoAction.Undo"/> (Ctrl + Z).</description></item>
/// <item><description>Tilt Right &ge; +42&deg; (↻) &rarr; <see cref="CircleUndoAction.Redo"/> (Ctrl + Y).</description></item>
/// </list>
/// </para>
/// </summary>
public class CircleUndoDetector
{
    /// <summary>
    /// Gets the internal state machine tracking wrist angles and cooldowns.
    /// </summary>
    public CircleUndoState State { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether wrist-twist Undo/Redo detection is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Required angular tilt in degrees (42°) to trigger an Undo or Redo action.
    /// </summary>
    public double TriggerAngleThresholdDeg { get; set; } = 42.0;

    /// <summary>
    /// Evaluates the primary tracked hand for the current frame to detect intentional wrist twists.
    /// </summary>
    /// <param name="hand">The tracked hand instance.</param>
    /// <returns>A <see cref="CircleUndoAction"/> if a twist threshold is reached; otherwise, <see cref="CircleUndoAction.None"/>.</returns>
    public CircleUndoAction Update(TrackedHand? hand)
    {
        if (!Enabled || hand == null || State.InCooldown)
        {
            if (!State.InCooldown)
            {
                State.Reset();
            }
            return CircleUndoAction.None;
        }

        DateTime now = DateTime.Now;

        // 1. Verify Peace Gesture Posture
        bool isPeace = IsPeacePosture(hand);
        if (!isPeace)
        {
            State.Reset();
            return CircleUndoAction.None;
        }

        Point2f wrist = hand.SmoothedLandmarks2D[0];
        Point2f indexTip = hand.SmoothedLandmarks2D[8];
        Point2f middleTip = hand.SmoothedLandmarks2D[12];
        Point2f tipCenter = new((indexTip.X + middleTip.X) / 2f, (indexTip.Y + middleTip.Y) / 2f);

        State.WristPos = wrist;
        State.FingerTipsPos = tipCenter;
        State.IsTracking = true;

        // 2. Compute Orientation Vector from Wrist to Tips Center
        double dx = tipCenter.X - wrist.X;
        double dy = tipCenter.Y - wrist.Y;
        double angleRad = Math.Atan2(dy, dx);
        double angleDeg = angleRad * 180.0 / Math.PI;

        State.CurrentAngleDeg = angleDeg;

        // 3. Establish Baseline on Gesture Entry or after 3.0s idle reset
        if (!State.InitialAngleDeg.HasValue || State.SessionTimer.Elapsed.TotalSeconds > 3.5)
        {
            State.InitialAngleDeg = angleDeg;
            State.SessionTimer.Restart();
            State.AngleDeltaDeg = 0.0;
            return CircleUndoAction.None;
        }

        // 4. Calculate Signed Angle Delta relative to Initial Baseline
        double delta = angleDeg - State.InitialAngleDeg.Value;
        while (delta > 180.0) delta -= 360.0;
        while (delta < -180.0) delta += 360.0;

        State.AngleDeltaDeg = delta;

        // 5. Check Thresholds: Tilt Left (<= -42°) -> UNDO | Tilt Right (>= +42°) -> REDO
        if (delta <= -TriggerAngleThresholdDeg)
        {
            State.LastAction = "UNDO";
            State.LastActionTime = now;
            State.LastActionCenter = tipCenter;
            State.CooldownTimer.Restart();
            State.Reset();
            return CircleUndoAction.Undo;
        }
        else if (delta >= TriggerAngleThresholdDeg)
        {
            State.LastAction = "REDO";
            State.LastActionTime = now;
            State.LastActionCenter = tipCenter;
            State.CooldownTimer.Restart();
            State.Reset();
            return CircleUndoAction.Redo;
        }

        return CircleUndoAction.None;
    }

    /// <summary>
    /// Helper evaluating whether the hand is forming a strict Peace / Victory (✌️) posture.
    /// </summary>
    public static bool IsPeacePosture(TrackedHand hand)
    {
        if (hand.Gesture == "Peace")
            return true;

        double distIndex0 = hand.Distance(8, 0);
        double distIndex5 = hand.Distance(5, 0);
        double distMiddle0 = hand.Distance(12, 0);
        double distMiddle9 = hand.Distance(9, 0);
        double distRing0 = hand.Distance(16, 0);
        double distRing13 = hand.Distance(13, 0);
        double distPinky0 = hand.Distance(20, 0);
        double distPinky17 = hand.Distance(17, 0);

        bool isIndexExtended = distIndex0 > distIndex5 * 1.10;
        bool isMiddleExtended = distMiddle0 > distMiddle9 * 1.10;
        bool isRingCurled = distRing0 < distRing13 * 1.30;
        bool isPinkyCurled = distPinky0 < distPinky17 * 1.30;

        double distTips = hand.Distance(8, 12);
        double palmSize = hand.Distance(0, 9);
        bool areTipsSeparated = distTips >= palmSize * 0.12;

        return isIndexExtended && isMiddleExtended && isRingCurled && isPinkyCurled && areTipsSeparated;
    }
}
