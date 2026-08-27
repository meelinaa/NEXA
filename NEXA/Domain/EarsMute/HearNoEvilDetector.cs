using System;
using System.Collections.Generic;
using NEXA.Face;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.EarsMute;

/// <summary>
/// Domain-level analyzer evaluating the "Hear No Evil" 🙉 hands-to-ears posture to toggle master speaker sound output mute.
/// <para>
/// <b>What it is:</b> A multi-modal spatial analyzer correlating facial ear landmarks with hand positions.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="number">
/// <item><description>Detects hands held beside the head at ear/temple height (palms facing forward towards camera as in 🙉).</description></item>
/// <item><description>Evaluates Euclidean proximity across fingertips, MCP knuckles, palm centers, and wrists.</description></item>
/// <item><description>Requires continuous hold inside ear proximity for &ge; 0.35s (identical timing to microphone mute).</description></item>
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
    /// <param name="hands">The list of active tracked hands.</param>
    /// <param name="face">The primary tracked face instance.</param>
    /// <returns><c>true</c> if hands were held at the ears for 0.35s and speaker sound mute should be toggled; otherwise, <c>false</c>.</returns>
    public bool Update(List<TrackedHand> hands, TrackedFace? face)
    {
        if (!Enabled || hands == null || hands.Count == 0 || face == null || State.InCooldown)
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

        bool leftEarCovered = false;
        bool rightEarCovered = false;

        // Key joint indices to test: Wrist (0), Thumb Tip (4), Index Tip (8), Middle Tip (12), Ring Tip (16), Pinky Tip (20), Knuckles (5, 9, 17)
        int[] probeIndices = [0, 4, 8, 12, 16, 20, 5, 9, 17];

        for (int i = 0; i < hands.Count; i++)
        {
            TrackedHand hand = hands[i];

            for (int p = 0; p < probeIndices.Length; p++)
            {
                int jointIdx = probeIndices[p];
                if (jointIdx >= hand.SmoothedLandmarks2D.Length)
                    continue;

                Point2f joint = hand.SmoothedLandmarks2D[jointIdx];

                // Check Left Ear proximity
                double distLeft = Math.Sqrt(Math.Pow(joint.X - face.LeftEar.X, 2) + Math.Pow(joint.Y - face.LeftEar.Y, 2));
                if (distLeft <= earRadius)
                {
                    leftEarCovered = true;
                }

                // Check Right Ear proximity
                double distRight = Math.Sqrt(Math.Pow(joint.X - face.RightEar.X, 2) + Math.Pow(joint.Y - face.RightEar.Y, 2));
                if (distRight <= earRadius)
                {
                    rightEarCovered = true;
                }
            }

            // Also check Hand Bounding Box center proximity
            Rect2f box = hand.BoundingBox;
            Point2f boxCenter = new(box.X + box.Width / 2f, box.Y + box.Height / 2f);

            double boxDistLeft = Math.Sqrt(Math.Pow(boxCenter.X - face.LeftEar.X, 2) + Math.Pow(boxCenter.Y - face.LeftEar.Y, 2));
            if (boxDistLeft <= earRadius * 1.25f)
            {
                leftEarCovered = true;
            }

            double boxDistRight = Math.Sqrt(Math.Pow(boxCenter.X - face.RightEar.X, 2) + Math.Pow(boxCenter.Y - face.RightEar.Y, 2));
            if (boxDistRight <= earRadius * 1.25f)
            {
                rightEarCovered = true;
            }
        }

        // The 🙉 pose is satisfied when both ears have hands beside them (or 1 hand held beside ear)
        bool inEarPose = (hands.Count >= 2 && leftEarCovered && rightEarCovered) ||
                         (leftEarCovered && rightEarCovered) ||
                         (hands.Count == 1 && (leftEarCovered || rightEarCovered));

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
}
