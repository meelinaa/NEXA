using System;
using System.Collections.Generic;
using NEXA.Face;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.EarsMute;

/// <summary>
/// Domain-level analyzer evaluating the "Hear No Evil" 🙉 two-hands-to-ears posture to toggle master speaker sound output mute.
/// <para>
/// <b>What it is:</b> A multi-modal spatial analyzer correlating facial ear landmarks with two simultaneous hand positions.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="number">
/// <item><description>Requires two distinct hands held beside both sides of the head at ear/temple height (palms facing forward as in 🙉).</description></item>
/// <item><description>Evaluates Euclidean proximity across fingertips, MCP knuckles, palm centers, and wrists for both ears simultaneously.</description></item>
/// <item><description>Requires continuous dual-hand hold inside ear proximity for &ge; 0.35s.</description></item>
/// <item><description>Enforces a 1.5s refractory cooldown after triggering to eliminate toggle flicker.</description></item>
/// </list>
/// </para>
/// </summary>
public class HearNoEvilDetector
{
    /// <summary>
    /// Gets the internal state machine tracking hold timers, proximity, and cooldowns.
    /// </summary>
    public HearNoEvilState State { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether "Hear No Evil" gesture detection is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="HearNoEvilDetector"/> class.
    /// </summary>
    public HearNoEvilDetector()
    {
        State.RequiredHoldSeconds = 0.35;
    }

    /// <summary>
    /// Evaluates tracked hands relative to the detected face for the current frame.
    /// </summary>
    /// <param name="hands">The list of active tracked hands (must contain at least 2 hands).</param>
    /// <param name="face">The primary tracked face instance.</param>
    /// <returns><c>true</c> if both hands were held at the ears for 0.35s and speaker sound mute should be toggled; otherwise, <c>false</c>.</returns>
    public bool Update(List<TrackedHand> hands, TrackedFace? face)
    {
        // Require at least 2 hands present in the scene to avoid accidental single-hand false positives
        if (!Enabled || hands == null || hands.Count < 2 || face == null || State.InCooldown)
        {
            if (!State.InCooldown)
            {
                State.Reset();
            }
            return false;
        }

        State.LastLeftEar = face.LeftEar;
        State.LastRightEar = face.RightEar;

        DateTime now = DateTime.Now;

        // Effective ear proximity radius (generous margin for forward-facing palms)
        float earRadius = Math.Max(75f, face.EarRadius > 10f ? face.EarRadius * 1.35f : 85f);

        // Key joint indices to test: Wrist (0), Thumb Tip (4), Index Tip (8), Middle Tip (12), Ring Tip (16), Pinky Tip (20), Knuckles (5, 9, 17)
        int[] probeIndices = [0, 4, 8, 12, 16, 20, 5, 9, 17];

        // 1. Verify that two distinct hands are covering the left and right ears respectively
        bool inEarPose = false;
        for (int i = 0; i < hands.Count; i++)
        {
            for (int j = 0; j < hands.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }

                bool leftCovered = IsHandNearTarget(hands[i], face.LeftEar, earRadius, probeIndices);
                bool rightCovered = IsHandNearTarget(hands[j], face.RightEar, earRadius, probeIndices);

                if (leftCovered && rightCovered)
                {
                    inEarPose = true;
                    break;
                }
            }

            if (inEarPose)
            {
                break;
            }
        }

        if (!inEarPose)
        {
            State.Reset();
            return false;
        }

        // 2. Accumulate continuous hold duration
        if (!State.IsInProximity || State.LastHoldTime == DateTime.MinValue)
        {
            State.IsInProximity = true;
            State.HoldDurationSeconds = 0;
            State.LastHoldTime = now;
            return false;
        }

        double dt = (now - State.LastHoldTime).TotalSeconds;
        State.LastHoldTime = now;

        if (dt is > 0 and < 0.25)
        {
            State.HoldDurationSeconds += dt;
        }
        else
        {
            State.HoldDurationSeconds = 0;
        }

        // 3. Trigger Mute Toggle upon reaching required hold duration (0.35s)
        if (State.HoldDurationSeconds >= State.RequiredHoldSeconds)
        {
            State.LastToggleTime = now;
            State.Reset();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether any joint landmark or bounding box of the given hand falls within proximity of a target coordinate.
    /// </summary>
    private static bool IsHandNearTarget(
        TrackedHand hand,
        Point2f target,
        float radius,
        int[] probeIndices)
    {
        for (int p = 0; p < probeIndices.Length; p++)
        {
            int jointIdx = probeIndices[p];
            if (jointIdx >= hand.SmoothedLandmarks2D.Length)
            {
                continue;
            }

            Point2f joint = hand.SmoothedLandmarks2D[jointIdx];
            double dist = Math.Sqrt(Math.Pow(joint.X - target.X, 2) + Math.Pow(joint.Y - target.Y, 2));
            if (dist <= radius)
            {
                return true;
            }
        }

        Rect2f box = hand.BoundingBox;
        Point2f boxCenter = new(box.X + box.Width / 2f, box.Y + box.Height / 2f);
        double boxDist = Math.Sqrt(Math.Pow(boxCenter.X - target.X, 2) + Math.Pow(boxCenter.Y - target.Y, 2));
        return boxDist <= radius * 1.25f;
    }
}
