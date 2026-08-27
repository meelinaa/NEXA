using System.Collections.Generic;
using NEXA.Domain.Lock;
using NEXA.Hand;
using OpenCvSharp;
using Xunit;

namespace NEXA.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="LockSequenceDetector"/> evaluating dual-hand 4-stage security sequence transitions and single-hand rejection.
/// </summary>
public class LockSequenceDetectorTests
{
    [Fact]
    public void TwoHands_FourStageSequence_OpenFistOpenFist_TriggersLock()
    {
        LockSequenceDetector testLockDetector = new();

        TrackedHand seqOpenHand1 = CreateOpenHand(400);
        TrackedHand seqOpenHand2 = CreateOpenHand(600);
        List<TrackedHand> dualOpenHands = new() { seqOpenHand1, seqOpenHand2 };

        TrackedHand seqFistHand1 = CreateFistHand(400);
        TrackedHand seqFistHand2 = CreateFistHand(600);
        List<TrackedHand> dualFistHands = new() { seqFistHand1, seqFistHand2 };

        testLockDetector.Update(dualOpenHands);
        testLockDetector.Update(dualOpenHands);
        Assert.Equal(LockSequenceStep.OpenPalm1, testLockDetector.State.CurrentStep);

        testLockDetector.Update(dualFistHands);
        testLockDetector.Update(dualFistHands);
        Assert.Equal(LockSequenceStep.Fist1, testLockDetector.State.CurrentStep);

        testLockDetector.Update(dualOpenHands);
        testLockDetector.Update(dualOpenHands);
        Assert.Equal(LockSequenceStep.OpenPalm2, testLockDetector.State.CurrentStep);

        testLockDetector.Update(dualFistHands);
        bool didTriggerLock = testLockDetector.Update(dualFistHands);

        Assert.True(didTriggerLock, "Dual-hand 4th stage should trigger workstation lock.");
        Assert.True(testLockDetector.State.InCooldown, "Cooldown should engage after trigger.");
    }

    [Fact]
    public void SingleHand_FourStageSequence_DoesNotTriggerLock()
    {
        LockSequenceDetector testLockDetector = new();

        TrackedHand singleOpen = CreateOpenHand(500);
        TrackedHand singleFist = CreateFistHand(500);

        List<TrackedHand> singleOpenList = new() { singleOpen };
        List<TrackedHand> singleFistList = new() { singleFist };

        testLockDetector.Update(singleOpenList);
        testLockDetector.Update(singleOpenList);
        Assert.Equal(LockSequenceStep.Idle, testLockDetector.State.CurrentStep);

        testLockDetector.Update(singleFistList);
        bool didTrigger = testLockDetector.Update(singleFistList);

        Assert.False(didTrigger, "Single hand should never advance or trigger the PC lock sequence.");
    }

    private static TrackedHand CreateOpenHand(float posX)
    {
        TrackedHand hand = new() { Gesture = "Open Palm" };
        hand.SmoothedLandmarks2D[0] = new Point2f(posX, 500);
        hand.SmoothedLandmarks2D[9] = new Point2f(posX, 400);
        hand.SmoothedLandmarks2D[4] = new Point2f(posX - 30, 430);
        hand.SmoothedLandmarks2D[8] = new Point2f(posX - 10, 320);
        hand.SmoothedLandmarks2D[12] = new Point2f(posX + 10, 310);
        hand.SmoothedLandmarks2D[16] = new Point2f(posX + 30, 325);
        hand.SmoothedLandmarks2D[20] = new Point2f(posX + 45, 345);
        return hand;
    }

    private static TrackedHand CreateFistHand(float posX)
    {
        TrackedHand hand = new() { Gesture = "Fist" };
        hand.SmoothedLandmarks2D[0] = new Point2f(posX, 500);
        hand.SmoothedLandmarks2D[9] = new Point2f(posX, 400);
        hand.SmoothedLandmarks2D[4] = new Point2f(posX - 10, 430);
        hand.SmoothedLandmarks2D[8] = new Point2f(posX, 440);
        hand.SmoothedLandmarks2D[12] = new Point2f(posX, 440);
        hand.SmoothedLandmarks2D[16] = new Point2f(posX, 440);
        hand.SmoothedLandmarks2D[20] = new Point2f(posX, 440);
        return hand;
    }
}
