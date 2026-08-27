using System.Collections.Generic;
using System.Threading;
using NEXA.Domain.EarsMute;
using NEXA.Face;
using NEXA.Hand;
using OpenCvSharp;
using Xunit;

namespace NEXA.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="HearNoEvilDetector"/> evaluating two-hands-to-ears master speaker sound mute triggering.
/// </summary>
public class HearNoEvilDetectorTests
{
    [Fact]
    public void TwoHandsToEars_HoldSatisfied_TriggersSpeakerMute()
    {
        HearNoEvilDetector testEars = new();
        testEars.State.RequiredHoldSeconds = 0.01;

        TrackedFace earFace = new()
        {
            LeftEar = new Point2f(750, 400),
            RightEar = new Point2f(450, 400),
            EarRadius = 60f
        };

        TrackedHand leftHandAtEar = new();
        leftHandAtEar.SmoothedLandmarks2D[0] = new Point2f(740, 410);
        TrackedHand rightHandAtEar = new();
        rightHandAtEar.SmoothedLandmarks2D[0] = new Point2f(460, 410);

        List<TrackedHand> earHands = new() { leftHandAtEar, rightHandAtEar };
        testEars.Update(earHands, earFace);
        Thread.Sleep(20);
        bool didToggleSound = testEars.Update(earHands, earFace);

        Assert.True(didToggleSound, "Hear-No-Evil Sound Mute gesture should trigger with 2 hands.");
        Assert.True(testEars.State.InCooldown, "Cooldown should engage after trigger.");
    }

    [Fact]
    public void SingleHandAtEar_DoesNotTriggerSpeakerMute()
    {
        HearNoEvilDetector testEars = new();
        testEars.State.RequiredHoldSeconds = 0.01;

        TrackedFace earFace = new()
        {
            LeftEar = new Point2f(750, 400),
            RightEar = new Point2f(450, 400),
            EarRadius = 60f
        };

        TrackedHand singleHandAtEar = new();
        singleHandAtEar.SmoothedLandmarks2D[0] = new Point2f(740, 410);

        List<TrackedHand> singleHandList = new() { singleHandAtEar };
        testEars.Update(singleHandList, earFace);
        Thread.Sleep(20);
        bool didToggleSound = testEars.Update(singleHandList, earFace);

        Assert.False(didToggleSound, "Single hand should never trigger Hear-No-Evil Sound Mute.");
        Assert.False(testEars.State.IsInProximity, "Proximity state should not engage with only 1 hand.");
    }
}
