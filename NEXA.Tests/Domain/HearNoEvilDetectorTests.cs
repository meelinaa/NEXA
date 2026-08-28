using System;
using System.Collections.Generic;
using System.Threading;
using NEXA.Domain.EarsMute;
using NEXA.Face;
using NEXA.Hand;
using OpenCvSharp;
using Xunit;

namespace NEXA.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="HearNoEvilDetector"/> evaluating two-hands-to-ears master speaker sound mute triggering,
/// 5-second cooldowns, screenshot disambiguation, and face-independent fallback.
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

        TrackedHand leftHandAtEar = CreateHandAtPosition(740, 410);
        TrackedHand rightHandAtEar = CreateHandAtPosition(460, 410);

        List<TrackedHand> earHands = new() { leftHandAtEar, rightHandAtEar };
        testEars.Update(earHands, earFace);
        Thread.Sleep(20);
        bool didToggleSound = testEars.Update(earHands, earFace);

        Assert.True(didToggleSound, "Hear-No-Evil Sound Mute gesture should trigger with 2 hands.");
        Assert.True(testEars.State.InCooldown, "5s Cooldown should engage after trigger.");
    }

    [Fact]
    public void MuteToggle_EnforcesFiveSecondCooldown()
    {
        HearNoEvilDetector testEars = new();
        testEars.State.RequiredHoldSeconds = 0.01;
        testEars.State.CooldownSeconds = 5.0;

        TrackedFace earFace = new()
        {
            LeftEar = new Point2f(750, 400),
            RightEar = new Point2f(450, 400),
            EarRadius = 60f
        };

        TrackedHand leftHandAtEar = CreateHandAtPosition(740, 410);
        TrackedHand rightHandAtEar = CreateHandAtPosition(460, 410);
        List<TrackedHand> earHands = new() { leftHandAtEar, rightHandAtEar };

        // 1. First trigger (Mute)
        testEars.Update(earHands, earFace);
        Thread.Sleep(20);
        bool firstToggle = testEars.Update(earHands, earFace);
        Assert.True(firstToggle);
        Assert.True(testEars.State.InCooldown);

        // 2. Attempt second trigger immediately during 5s cooldown window -> must be blocked
        bool blockedToggle = testEars.Update(earHands, earFace);
        Assert.False(blockedToggle, "Mute toggle must be blocked during 5-second refractory cooldown.");
    }

    [Fact]
    public void TouchingFingers_ScreenshotPriority_SuppressesEarsMute()
    {
        HearNoEvilDetector testEars = new();
        testEars.State.RequiredHoldSeconds = 0.01;

        TrackedFace earFace = new()
        {
            LeftEar = new Point2f(750, 400),
            RightEar = new Point2f(450, 400),
            EarRadius = 60f
        };

        // Two hands close together in front of face with touching index fingers (e.g. Screenshot framing)
        TrackedHand hand1 = new();
        hand1.SmoothedLandmarks2D[0] = new Point2f(600, 400);
        hand1.SmoothedLandmarks2D[8] = new Point2f(600, 350); // Index tip
        hand1.SmoothedLandmarks2D[4] = new Point2f(580, 380); // Thumb tip
        hand1.SmoothedLandmarks2D[9] = new Point2f(600, 400);

        TrackedHand hand2 = new();
        hand2.SmoothedLandmarks2D[0] = new Point2f(610, 400);
        hand2.SmoothedLandmarks2D[8] = new Point2f(610, 350); // Index tip touching hand1
        hand2.SmoothedLandmarks2D[4] = new Point2f(590, 380); // Thumb tip touching hand1
        hand2.SmoothedLandmarks2D[9] = new Point2f(610, 400);

        List<TrackedHand> framingHands = new() { hand1, hand2 };
        bool triggered = testEars.Update(framingHands, earFace);

        Assert.False(triggered, "Screenshot framing posture (touching fingers) must suppress HearNoEvil mute.");
    }

    [Fact]
    public void FaceIndependentFallback_TriggersWhenFaceIsNull()
    {
        HearNoEvilDetector testEars = new();
        testEars.State.RequiredHoldSeconds = 0.01;

        TrackedHand leftHand = CreateHandAtPosition(800, 300);
        TrackedHand rightHand = CreateHandAtPosition(300, 300);

        List<TrackedHand> earHands = new() { leftHand, rightHand };
        testEars.Update(earHands, null); // face is null
        Thread.Sleep(20);
        bool didToggleSound = testEars.Update(earHands, null);

        Assert.True(didToggleSound, "Hear-No-Evil Sound Mute should work independently of face tracking.");
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

        TrackedHand singleHandAtEar = CreateHandAtPosition(740, 410);

        List<TrackedHand> singleHandList = new() { singleHandAtEar };
        testEars.Update(singleHandList, earFace);
        Thread.Sleep(20);
        bool didToggleSound = testEars.Update(singleHandList, earFace);

        Assert.False(didToggleSound, "Single hand should never trigger Hear-No-Evil Sound Mute.");
        Assert.False(testEars.State.IsInProximity, "Proximity state should not engage with only 1 hand.");
    }

    private static TrackedHand CreateHandAtPosition(float x, float y)
    {
        TrackedHand hand = new();
        hand.SmoothedLandmarks2D[0] = new Point2f(x, y);
        hand.SmoothedLandmarks2D[4] = new Point2f(x - 10, y - 10);
        hand.SmoothedLandmarks2D[8] = new Point2f(x, y - 20);
        hand.SmoothedLandmarks2D[9] = new Point2f(x, y);
        return hand;
    }
}
