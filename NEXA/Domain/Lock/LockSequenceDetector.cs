using System;
using System.Collections.Generic;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Lock;

/// <summary>
/// Domain-level analyzer evaluating the dual-hand 4-stage intentional security sequence (🖐️🖐️ &rarr; ✊✊ &rarr; 🖐️🖐️ &rarr; ✊✊) to lock the Windows PC.
/// <para>
/// <b>What it is:</b> A multi-state temporal posture state machine requiring both hands to guard the OS lock workstation function against accidental triggers.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="number">
/// <item><description>Requires two tracked hands in the scene simultaneously.</description></item>
/// <item><description>Validates the sequence: BothOpen1 &rarr; BothFist1 &rarr; BothOpen2 &rarr; BothFist2.</description></item>
/// <item><description>Enforces a 800ms transition timeout between consecutive posture changes.</description></item>
/// <item><description>Debounces each stage for &ge; 2 frames to eliminate sensor flicker.</description></item>
/// <item><description>Dispatches a lock trigger only when all 4 milestones are successfully completed by both hands in order.</description></item>
/// </list>
/// </para>
/// </summary>
public class LockSequenceDetector
{
    /// <summary>
    /// Gets the internal state machine tracking sequence milestones and timers.
    /// </summary>
    public LockSequenceState State { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether PC lock sequence detection is active.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Evaluates the tracked hands for the current frame to advance or reset the lock sequence.
    /// </summary>
    /// <param name="hands">The list of active tracked hands (must contain at least 2 hands).</param>
    /// <returns><c>true</c> if all 4 steps were successfully completed by both hands and the PC should be locked; otherwise, <c>false</c>.</returns>
    public bool Update(List<TrackedHand>? hands)
    {
        // Require at least 2 hands to prevent accidental single-hand triggers
        if (!Enabled || hands == null || hands.Count < 2)
        {
            if (State.CurrentStep != LockSequenceStep.Idle && State.StepTimer.Elapsed.TotalSeconds > State.StepTimeoutSeconds)
            {
                State.Reset();
            }
            return false;
        }

        if (State.InCooldown)
        {
            State.Reset();
            return false;
        }

        TrackedHand hand1 = hands[0];
        TrackedHand hand2 = hands[1];

        // Midpoint between both hands for AR badge position
        float midX = (hand1.SmoothedLandmarks2D[9].X + hand2.SmoothedLandmarks2D[9].X) / 2f;
        float midY = (hand1.SmoothedLandmarks2D[9].Y + hand2.SmoothedLandmarks2D[9].Y) / 2f;
        State.LastHandPos = new Point2f(midX, midY);

        bool bothOpen = IsOpenPalm(hand1) && IsOpenPalm(hand2);
        bool bothFist = IsFist(hand1) && IsFist(hand2);

        switch (State.CurrentStep)
        {
            case LockSequenceStep.Idle:
                if (bothOpen)
                {
                    State.ConsecutivePoseFrames++;
                    if (State.ConsecutivePoseFrames >= 2)
                    {
                        State.CurrentStep = LockSequenceStep.OpenPalm1;
                        State.StepTimer.Restart();
                        State.ConsecutivePoseFrames = 0;
                    }
                }
                else
                {
                    State.ConsecutivePoseFrames = 0;
                }
                break;

            case LockSequenceStep.OpenPalm1:
                if (State.StepTimer.Elapsed.TotalSeconds > State.StepTimeoutSeconds)
                {
                    State.Reset();
                }
                else if (bothFist)
                {
                    State.ConsecutivePoseFrames++;
                    if (State.ConsecutivePoseFrames >= 2)
                    {
                        State.CurrentStep = LockSequenceStep.Fist1;
                        State.StepTimer.Restart();
                        State.ConsecutivePoseFrames = 0;
                    }
                }
                break;

            case LockSequenceStep.Fist1:
                if (State.StepTimer.Elapsed.TotalSeconds > State.StepTimeoutSeconds)
                {
                    State.Reset();
                }
                else if (bothOpen)
                {
                    State.ConsecutivePoseFrames++;
                    if (State.ConsecutivePoseFrames >= 2)
                    {
                        State.CurrentStep = LockSequenceStep.OpenPalm2;
                        State.StepTimer.Restart();
                        State.ConsecutivePoseFrames = 0;
                    }
                }
                break;

            case LockSequenceStep.OpenPalm2:
                if (State.StepTimer.Elapsed.TotalSeconds > State.StepTimeoutSeconds)
                {
                    State.Reset();
                }
                else if (bothFist)
                {
                    State.ConsecutivePoseFrames++;
                    if (State.ConsecutivePoseFrames >= 2)
                    {
                        State.CurrentStep = LockSequenceStep.Fist2;
                        State.LastLockTriggerTime = DateTime.Now;
                        State.LockCooldownTimer.Restart();
                        State.Reset();
                        return true;
                    }
                }
                break;
        }

        return false;
    }

    /// <summary>
    /// Evaluates whether the hand is in an Open Palm posture.
    /// </summary>
    private static bool IsOpenPalm(TrackedHand hand)
    {
        if (hand.Gesture == "Open Palm")
        {
            return true;
        }

        double distThumb0 = hand.Distance(4, 0);
        double distThumb2 = hand.Distance(2, 0);
        double distIndex0 = hand.Distance(8, 0);
        double distIndex5 = hand.Distance(5, 0);
        double distMiddle0 = hand.Distance(12, 0);
        double distMiddle9 = hand.Distance(9, 0);
        double distRing0 = hand.Distance(16, 0);
        double distRing13 = hand.Distance(13, 0);
        double distPinky0 = hand.Distance(20, 0);
        double distPinky17 = hand.Distance(17, 0);

        return distThumb0 > distThumb2 * 1.05 &&
               distIndex0 > distIndex5 * 1.10 &&
               distMiddle0 > distMiddle9 * 1.10 &&
               distRing0 > distRing13 * 1.10 &&
               distPinky0 > distPinky17 * 1.10;
    }

    /// <summary>
    /// Evaluates whether the hand is in a Fist posture.
    /// </summary>
    private static bool IsFist(TrackedHand hand)
    {
        if (hand.Gesture == "Fist")
        {
            return true;
        }

        double distIndex0 = hand.Distance(8, 0);
        double distIndex5 = hand.Distance(5, 0);
        double distMiddle0 = hand.Distance(12, 0);
        double distMiddle9 = hand.Distance(9, 0);
        double distRing0 = hand.Distance(16, 0);
        double distRing13 = hand.Distance(13, 0);
        double distPinky0 = hand.Distance(20, 0);
        double distPinky17 = hand.Distance(17, 0);

        return distIndex0 < distIndex5 * 1.15 &&
               distMiddle0 < distMiddle9 * 1.15 &&
               distRing0 < distRing13 * 1.15 &&
               distPinky0 < distPinky17 * 1.15;
    }
}
