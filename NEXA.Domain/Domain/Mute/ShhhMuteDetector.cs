using System;
using NEXA.Face;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Mute;

/// <summary>
/// Domain-level analyzer evaluating the 4-finger upright posture (4 fingers extended in front of the mouth) to toggle master microphone input mute.
/// <para>
/// <b>What it is:</b> A multi-modal spatial analyzer correlating facial landmark mouth targets with the 4-finger hand posture.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="number">
/// <item><description>Detects upright vertical 4-finger posture (Index, Middle, Ring, Pinky extended, Thumb tucked).</description></item>
/// <item><description>Computes Euclidean distance between the 4 fingertips and tracked mouth center.</description></item>
/// <item><description>Requires continuous hold inside the mouth target radius for &ge; 0.35s.</description></item>
/// <item><description>Enforces a 1.5s refractory cooldown after triggering to eliminate toggle flicker.</description></item>
/// </list>
/// </para>
/// </summary>
public class ShhhMuteDetector
{
    /// <summary>
    /// Gets the internal state machine tracking hold timers, proximity, and cooldowns.
    /// </summary>
    public ShhhMuteState State { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether "Shhh" gesture detection is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShhhMuteDetector"/> class.
    /// </summary>
    public ShhhMuteDetector()
    {
        State.RequiredHoldSeconds = 0.35;
    }

    /// <summary>
    /// Evaluates the primary hand relative to the detected face for the current frame.
    /// </summary>
    /// <param name="hand">The primary tracked hand instance.</param>
    /// <param name="face">The primary tracked face instance.</param>
    /// <returns><c>true</c> if 4 fingers were held in front of the mouth for 0.35s and mute should be toggled; otherwise, <c>false</c>.</returns>
    public bool Update(TrackedHand? hand, TrackedFace? face)
    {
        if (!Enabled || hand == null || face == null || State.InCooldown)
        {
            if (!State.InCooldown)
            {
                State.Reset();
            }
            return false;
        }

        State.LastMouthCenter = face.MouthCenter;

        // 1. Verify Upright 4-Finger Posture (Index, Middle, Ring, Pinky extended, Thumb tucked)
        bool isFourFingers = IsFourFingerPosture(hand);
        if (!isFourFingers)
        {
            State.Reset();
            return false;
        }

        // Center of the 4 extended fingertips
        Point2f tip8 = hand.SmoothedLandmarks2D[8];
        Point2f tip12 = hand.SmoothedLandmarks2D[12];
        Point2f tip16 = hand.SmoothedLandmarks2D[16];
        Point2f tip20 = hand.SmoothedLandmarks2D[20];

        Point2f fourFingersCenter = new((tip8.X + tip12.X + tip16.X + tip20.X) / 4f, (tip8.Y + tip12.Y + tip16.Y + tip20.Y) / 4f);
        Point2f mouth = face.MouthCenter;

        double distCenter = Math.Sqrt(Math.Pow(fourFingersCenter.X - mouth.X, 2) + Math.Pow(fourFingersCenter.Y - mouth.Y, 2));
        double distTip8 = Math.Sqrt(Math.Pow(tip8.X - mouth.X, 2) + Math.Pow(tip8.Y - mouth.Y, 2));
        double distTip12 = Math.Sqrt(Math.Pow(tip12.X - mouth.X, 2) + Math.Pow(tip12.Y - mouth.Y, 2));

        double minDist = Math.Min(distCenter, Math.Min(distTip8, distTip12));
        State.CurrentDistanceToMouth = minDist;

        // 2. Evaluate Spatial Proximity to Mouth Target (Allowing comfortable 1.5x mouth radius margin)
        double proximityThreshold = face.MouthRadius * 1.55;
        if (minDist <= proximityThreshold)
        {
            State.IsInProximity = true;
            if (!State.HoldTimer.IsRunning)
            {
                State.HoldTimer.Restart();
            }

            // 3. Confirm Hold Duration Threshold (0.35s)
            if (State.HoldTimer.Elapsed.TotalSeconds >= State.RequiredHoldSeconds)
            {
                State.LastToggleTime = DateTime.Now;
                State.CooldownTimer.Restart();
                State.Reset();
                return true;
            }
        }
        else
        {
            State.Reset();
        }

        return false;
    }

    /// <summary>
    /// Helper evaluating whether the hand is in an upright 4-finger posture (4 fingers extended, thumb tucked).
    /// </summary>
    public static bool IsFourFingerPosture(TrackedHand hand)
    {
        double distIndex0 = hand.Distance(8, 0);
        double distIndex5 = hand.Distance(5, 0);
        double distMiddle0 = hand.Distance(12, 0);
        double distMiddle9 = hand.Distance(9, 0);
        double distRing0 = hand.Distance(16, 0);
        double distRing13 = hand.Distance(13, 0);
        double distPinky0 = hand.Distance(20, 0);
        double distPinky17 = hand.Distance(17, 0);
        double distThumb0 = hand.Distance(4, 0);
        double distThumb2 = hand.Distance(2, 0);

        bool isIndexExtended = distIndex0 > distIndex5 * 1.15;
        bool isMiddleExtended = distMiddle0 > distMiddle9 * 1.15;
        bool isRingExtended = distRing0 > distRing13 * 1.15;
        bool isPinkyExtended = distPinky0 > distPinky17 * 1.15;
        bool isThumbTucked = distThumb0 < distThumb2 * 1.45;

        // Upright vertical check (middle fingertip is higher in frame than wrist)
        bool isUpright = hand.SmoothedLandmarks2D[12].Y < hand.SmoothedLandmarks2D[0].Y;

        return isIndexExtended && isMiddleExtended && isRingExtended && isPinkyExtended && isThumbTucked && isUpright;
    }
}
