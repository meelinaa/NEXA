using System.Threading;
using NEXA.Domain.Mute;
using NEXA.Face;
using NEXA.Hand;
using OpenCvSharp;
using Xunit;

namespace NEXA.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="ShhhMuteDetector"/> evaluating 4-fingers-to-mouth microphone mute triggering.
/// </summary>
public class ShhhMuteDetectorTests
{
    [Fact]
    public void FourFingersToMouth_HoldSatisfied_TriggersMicrophoneMute()
    {
        ShhhMuteDetector testShhh = new();
        testShhh.State.RequiredHoldSeconds = 0.01;

        TrackedFace simulatedFace = new()
        {
            BoundingBox = new Rect2f(500, 200, 200, 260),
            MouthCenter = new Point2f(600, 400),
            MouthRadius = 50.0f
        };

        TrackedHand fourFingerHand = new();
        fourFingerHand.SmoothedLandmarks2D[0] = new Point2f(600, 560);
        fourFingerHand.SmoothedLandmarks2D[2] = new Point2f(570, 520);
        fourFingerHand.SmoothedLandmarks2D[4] = new Point2f(580, 500);
        fourFingerHand.SmoothedLandmarks2D[5] = new Point2f(590, 480);
        fourFingerHand.SmoothedLandmarks2D[8] = new Point2f(590, 400);
        fourFingerHand.SmoothedLandmarks2D[9] = new Point2f(600, 475);
        fourFingerHand.SmoothedLandmarks2D[12] = new Point2f(600, 395);
        fourFingerHand.SmoothedLandmarks2D[13] = new Point2f(610, 480);
        fourFingerHand.SmoothedLandmarks2D[16] = new Point2f(610, 400);
        fourFingerHand.SmoothedLandmarks2D[17] = new Point2f(620, 490);
        fourFingerHand.SmoothedLandmarks2D[20] = new Point2f(620, 410);

        testShhh.Update(fourFingerHand, simulatedFace);
        Thread.Sleep(20);
        bool didToggleMute = testShhh.Update(fourFingerHand, simulatedFace);

        Assert.True(didToggleMute, "4-Finger Mute gesture should trigger.");
        Assert.True(testShhh.State.InCooldown, "Cooldown should engage after trigger.");
    }
}
