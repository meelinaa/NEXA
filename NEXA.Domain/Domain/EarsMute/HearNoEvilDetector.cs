using System;
using System.Collections.Generic;
using NEXA.Face;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.EarsMute;

/// <summary>
/// Domain-level analyzer evaluating the "Hear No Evil" two-hands-to-ears posture to toggle master speaker sound output mute.
/// <para>
/// <b>What it is:</b> A multi-modal spatial analyzer correlating facial ear landmarks with two simultaneous hand positions.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="number">
/// <item><description>Requires two distinct hands held beside both sides of the head at ear/temple height (palms facing forward).</description></item>
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
        State.RequiredHoldSeconds = 2.0;
        State.CooldownSeconds = 5.0;
    }

    /// <summary>
    /// Evaluates tracked hands relative to the detected face for the current frame.
    /// </summary>
    /// <param name="hands">The list of active tracked hands (must contain at least 2 hands).</param>
    /// <param name="face">The primary tracked face instance.</param>
    /// <returns><c>true</c> if both hands were held at the ears for 2.0s and speaker sound mute should be toggled; otherwise, <c>false</c>.</returns>
    public bool Update(List<TrackedHand> hands, TrackedFace? face)
    {
        // Require at least 2 hands present in the scene
        if (!Enabled || hands == null || hands.Count < 2 || State.InCooldown)
        {
            if (!State.InCooldown)
            {
                State.Reset();
            }
            return false;
        }

        TrackedHand hand1 = hands[0];
        TrackedHand hand2 = hands[1];

        // Disambiguation: If fingers are touching (e.g. Screenshot framing box in front of face), Screenshot has strict priority over Mute!
        Point2f index1 = hand1.SmoothedLandmarks2D[8];
        Point2f index2 = hand2.SmoothedLandmarks2D[8];
        Point2f thumb1 = hand1.SmoothedLandmarks2D[4];
        Point2f thumb2 = hand2.SmoothedLandmarks2D[4];

        double distIndex = Math.Sqrt(Math.Pow(index1.X - index2.X, 2) + Math.Pow(index1.Y - index2.Y, 2));
        double distThumb = Math.Sqrt(Math.Pow(thumb1.X - thumb2.X, 2) + Math.Pow(thumb1.Y - thumb2.Y, 2));
        double palmDist = Math.Sqrt(Math.Pow(hand1.PalmCenter.X - hand2.PalmCenter.X, 2) + Math.Pow(hand1.PalmCenter.Y - hand2.PalmCenter.Y, 2));

        if (distIndex < 80.0 || distThumb < 80.0 || palmDist < 120.0)
        {
            // Hands are touching or close together in the center -> Screenshot / Clap / Framing, not HearNoEvil
            State.Reset();
            return false;
        }

        Point2f leftEarTarget;
        Point2f rightEarTarget;
        float earRadius;

        if (face != null)
        {
            leftEarTarget = face.LeftEar;
            rightEarTarget = face.RightEar;
            earRadius = Math.Max(75f, face.EarRadius > 10f ? face.EarRadius * 1.35f : 85f);
        }
        else
        {
            // Face-independent fallback: If hands are wide apart on left and right sides of the viewport
            float minX = Math.Min(hand1.PalmCenter.X, hand2.PalmCenter.X);
            float maxX = Math.Max(hand1.PalmCenter.X, hand2.PalmCenter.X);
            float avgY = (hand1.PalmCenter.Y + hand2.PalmCenter.Y) / 2f;

            leftEarTarget = new Point2f(minX, avgY);
            rightEarTarget = new Point2f(maxX, avgY);
            earRadius = 95f;
        }

        State.LastLeftEar = leftEarTarget;
        State.LastRightEar = rightEarTarget;

        DateTime now = DateTime.Now;

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

                bool leftCovered = IsHandNearTarget(hands[i], leftEarTarget, earRadius, probeIndices);
                bool rightCovered = IsHandNearTarget(hands[j], rightEarTarget, earRadius, probeIndices);

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

        // 2. Accumulate continuous hold duration (2.0s requirement)
        if (!State.IsInProximity || State.LastHoldTime == DateTime.MinValue)
        {
            State.IsInProximity = true;
            State.HoldDurationSeconds = 0;
            State.LastHoldTime = now;
            return false;
        }

        double deltaSeconds = (now - State.LastHoldTime).TotalSeconds;
        State.LastHoldTime = now;

        if (deltaSeconds > 0.35)
        {
            State.HoldDurationSeconds = 0;
            return false;
        }

        State.HoldDurationSeconds += deltaSeconds;

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
