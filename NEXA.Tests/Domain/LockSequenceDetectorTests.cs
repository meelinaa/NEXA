using NEXA.Domain.Lock;
using NEXA.Hand;
using OpenCvSharp;
using Xunit;

namespace NEXA.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="LockSequenceDetector"/> evaluating 4-stage security sequence transitions.
/// </summary>
public class LockSequenceDetectorTests
{
    [Fact]
    public void FourStageSequence_OpenFistOpenFist_TriggersLock()
    {
        LockSequenceDetector testLockDetector = new();

        TrackedHand seqOpenHand = new() { Gesture = "Open Palm" };
        seqOpenHand.SmoothedLandmarks2D[0] = new Point2f(500, 500);
        seqOpenHand.SmoothedLandmarks2D[9] = new Point2f(500, 400);
        seqOpenHand.SmoothedLandmarks2D[4] = new Point2f(470, 430);
        seqOpenHand.SmoothedLandmarks2D[8] = new Point2f(490, 320);
        seqOpenHand.SmoothedLandmarks2D[12] = new Point2f(510, 310);
        seqOpenHand.SmoothedLandmarks2D[16] = new Point2f(530, 325);
        seqOpenHand.SmoothedLandmarks2D[20] = new Point2f(545, 345);

        TrackedHand seqFistHand = new() { Gesture = "Fist" };
        seqFistHand.SmoothedLandmarks2D[0] = new Point2f(500, 500);
        seqFistHand.SmoothedLandmarks2D[9] = new Point2f(500, 400);
        seqFistHand.SmoothedLandmarks2D[4] = new Point2f(490, 430);
        seqFistHand.SmoothedLandmarks2D[8] = new Point2f(500, 440);
        seqFistHand.SmoothedLandmarks2D[12] = new Point2f(500, 440);
        seqFistHand.SmoothedLandmarks2D[16] = new Point2f(500, 440);
        seqFistHand.SmoothedLandmarks2D[20] = new Point2f(500, 440);

        testLockDetector.Update(seqOpenHand);
        testLockDetector.Update(seqOpenHand);
        Assert.Equal(LockSequenceStep.OpenPalm1, testLockDetector.State.CurrentStep);

        testLockDetector.Update(seqFistHand);
        testLockDetector.Update(seqFistHand);
        Assert.Equal(LockSequenceStep.Fist1, testLockDetector.State.CurrentStep);

        testLockDetector.Update(seqOpenHand);
        testLockDetector.Update(seqOpenHand);
        Assert.Equal(LockSequenceStep.OpenPalm2, testLockDetector.State.CurrentStep);

        testLockDetector.Update(seqFistHand);
        bool didTriggerLock = testLockDetector.Update(seqFistHand);

        Assert.True(didTriggerLock, "4th stage should trigger workstation lock.");
        Assert.True(testLockDetector.State.InCooldown, "Cooldown should engage after trigger.");
    }
}
