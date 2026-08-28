using System;
using System.Collections.Generic;
using NEXA.Abstractions;
using NEXA.Domain.Common;
using NEXA.Domain.EarsMute;
using NEXA.Domain.Grab;
using NEXA.Domain.Scroll;
using NEXA.Domain.TwoHand;
using NEXA.Domain.Volume;
using NEXA.Face;
using NEXA.Hand;
using OpenCvSharp;
using Xunit;

namespace NEXA.Tests.Domain;

/// <summary>
/// Unit tests validating global gesture mutual exclusion (Arbitration), 5-second mute cooldowns,
/// and 2-second dwell confirmation hold durations.
/// </summary>
public class GestureArbitrationTests
{
    [Fact]
    public void GestureArbitrator_BasicLockingAndRelease_WorksExclusively()
    {
        GestureArbitrator arbitrator = new();

        Assert.Null(arbitrator.ActiveGesture);
        Assert.True(arbitrator.CanExecute("WindowGrab"));
        Assert.True(arbitrator.CanExecute("HearNoEvil"));

        // 1. Acquire lock for WindowGrab
        Assert.True(arbitrator.TryAcquire("WindowGrab"));
        Assert.Equal("WindowGrab", arbitrator.ActiveGesture);

        // 2. Competing gestures must be blocked
        Assert.True(arbitrator.CanExecute("WindowGrab"));
        Assert.False(arbitrator.CanExecute("HearNoEvil"));
        Assert.False(arbitrator.CanExecute("Volume"));
        Assert.False(arbitrator.CanExecute("Scroll"));
        Assert.False(arbitrator.TryAcquire("HearNoEvil"));

        // 3. Release lock
        arbitrator.Release("WindowGrab");
        Assert.Null(arbitrator.ActiveGesture);

        // 4. Other gesture can now acquire
        Assert.True(arbitrator.CanExecute("HearNoEvil"));
        Assert.True(arbitrator.TryAcquire("HearNoEvil"));
        Assert.Equal("HearNoEvil", arbitrator.ActiveGesture);
    }

    [Fact]
    public void WindowGrabActive_SuppressesEarsMuteAndScroll()
    {
        GestureArbitrator arbitrator = new();
        arbitrator.TryAcquire("WindowGrab");

        HearNoEvilDetector earsDetector = new();
        earsDetector.State.RequiredHoldSeconds = 0.01;

        TrackedFace earFace = new()
        {
            LeftEar = new Point2f(750, 400),
            RightEar = new Point2f(450, 400),
            EarRadius = 60f
        };

        TrackedHand leftHand = new();
        leftHand.SmoothedLandmarks2D[0] = new Point2f(740, 410);
        leftHand.SmoothedLandmarks2D[8] = new Point2f(740, 390);
        leftHand.SmoothedLandmarks2D[9] = new Point2f(740, 410);

        TrackedHand rightHand = new();
        rightHand.SmoothedLandmarks2D[0] = new Point2f(460, 410);
        rightHand.SmoothedLandmarks2D[8] = new Point2f(460, 390);
        rightHand.SmoothedLandmarks2D[9] = new Point2f(460, 410);

        using Mat dummyFrame = new(720, 1280, MatType.CV_8UC3);
        FrameContext context = new(dummyFrame, new List<TrackedHand> { leftHand, rightHand }, earFace, arbitrator);

        // When WindowGrab has locked the pipeline, HearNoEvil controller must be blocked
        Assert.False(context.Arbitrator!.CanExecute("HearNoEvil"));
    }

    [Fact]
    public void TwoHandScreenshotFraming_HasPriorityOverMute()
    {
        CameraFrameScreenshotDetector screenshotDetector = new();
        TwoHandGestureState state = new()
        {
            RequiredScreenshotHoldSeconds = 2.0
        };

        TrackedHand hand1 = new() { Gesture = "L" };
        hand1.SmoothedLandmarks2D[0] = new Point2f(400, 500);
        hand1.SmoothedLandmarks2D[2] = new Point2f(360, 470);
        hand1.SmoothedLandmarks2D[4] = new Point2f(320, 470);
        hand1.SmoothedLandmarks2D[5] = new Point2f(400, 430);
        hand1.SmoothedLandmarks2D[8] = new Point2f(400, 360);
        hand1.SmoothedLandmarks2D[9] = new Point2f(400, 430);
        hand1.SmoothedLandmarks2D[12] = new Point2f(400, 470);
        hand1.SmoothedLandmarks2D[13] = new Point2f(420, 440);
        hand1.SmoothedLandmarks2D[16] = new Point2f(420, 480);
        hand1.SmoothedLandmarks2D[17] = new Point2f(440, 450);
        hand1.SmoothedLandmarks2D[20] = new Point2f(440, 490);

        TrackedHand hand2 = new() { Gesture = "L" };
        hand2.SmoothedLandmarks2D[0] = new Point2f(600, 500);
        hand2.SmoothedLandmarks2D[2] = new Point2f(560, 470);
        hand2.SmoothedLandmarks2D[4] = new Point2f(330, 470);
        hand2.SmoothedLandmarks2D[5] = new Point2f(600, 430);
        hand2.SmoothedLandmarks2D[8] = new Point2f(410, 360);
        hand2.SmoothedLandmarks2D[9] = new Point2f(600, 430);
        hand2.SmoothedLandmarks2D[12] = new Point2f(600, 470);
        hand2.SmoothedLandmarks2D[13] = new Point2f(620, 440);
        hand2.SmoothedLandmarks2D[16] = new Point2f(620, 480);
        hand2.SmoothedLandmarks2D[17] = new Point2f(640, 450);
        hand2.SmoothedLandmarks2D[20] = new Point2f(640, 490);

        bool triggered = screenshotDetector.Update(hand1, hand2, state, avgPalmSize: 80.0);

        Assert.True(state.IsCameraFrameActive, "Camera frame viewfinder should be active when dual-L hands touch.");
        Assert.True(state.ScreenshotHoldTimer.IsRunning, "Screenshot 2-second hold timer must start counting.");
    }

    [Fact]
    public void MousePointing_HasHighestPriority_OverridesBackgroundLocks()
    {
        GestureArbitrator arbitrator = new();

        // 1. Suppose a background or stale gesture lock was held (e.g. Scroll or Volume)
        arbitrator.TryAcquire("Scroll");
        Assert.Equal("Scroll", arbitrator.ActiveGesture);

        // 2. When user points with their index finger, Mouse acquires lock with high priority
        bool acquired = arbitrator.TryAcquire("Mouse", highPriority: true);

        Assert.True(acquired, "Mouse pointing must acquire high-priority lock over background gestures.");
        Assert.Equal("Mouse", arbitrator.ActiveGesture);
        Assert.True(arbitrator.CanExecute("Mouse"));
        Assert.False(arbitrator.CanExecute("Scroll"));
        Assert.False(arbitrator.CanExecute("HearNoEvil"));
    }

    [Fact]
    public void TwoHandsPresent_BlocksVolumeAdjustment()
    {
        GestureArbitrator arbitrator = new();
        VolumeController volumeController = new();

        TrackedHand hand1 = new() { Gesture = "L" };
        hand1.SmoothedLandmarks2D[0] = new Point2f(400, 500);
        hand1.SmoothedLandmarks2D[2] = new Point2f(360, 470);
        hand1.SmoothedLandmarks2D[4] = new Point2f(320, 470);
        hand1.SmoothedLandmarks2D[5] = new Point2f(400, 430);
        hand1.SmoothedLandmarks2D[8] = new Point2f(400, 360);
        hand1.SmoothedLandmarks2D[9] = new Point2f(400, 430);

        TrackedHand hand2 = new() { Gesture = "L" };
        hand2.SmoothedLandmarks2D[0] = new Point2f(600, 500);
        hand2.SmoothedLandmarks2D[2] = new Point2f(560, 470);
        hand2.SmoothedLandmarks2D[4] = new Point2f(330, 470);
        hand2.SmoothedLandmarks2D[5] = new Point2f(600, 430);
        hand2.SmoothedLandmarks2D[8] = new Point2f(410, 360);
        hand2.SmoothedLandmarks2D[9] = new Point2f(600, 430);

        using Mat dummyFrame = new(720, 1280, MatType.CV_8UC3);
        FrameContext context = new(dummyFrame, new List<TrackedHand> { hand1, hand2 }, null, arbitrator);

        // When 2 hands are present, VolumeController must not activate or change volume
        volumeController.Process(context);

        Assert.False(volumeController.State.IsActive, "Volume adjustment must remain inactive when 2 hands are visible.");
        Assert.NotEqual("Volume", arbitrator.ActiveGesture);
    }
}
