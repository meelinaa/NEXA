using System;
using System.Collections.Generic;
using System.Threading;
using NEXA.Adapters.Output;
using NEXA.Domain.TwoHand;
using NEXA.Hand;
using OpenCvSharp;
using Xunit;

namespace NEXA.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="TwoHandGestureDetector"/> including Maximize, Minimize, Screenshot, and Clap Play/Pause gestures.
/// </summary>
public class TwoHandGestureDetectorTests
{
    [Fact]
    public void TwoHand_MaximizeGesture_TriggersSuccessfully()
    {
        TwoHandGestureDetector testTwoHand = new();
        Win32InputSink inputSink = new();
        inputSink.LastFocusedHwnd = new IntPtr(12345);

        Assert.False(testTwoHand.State.IsWindowActive, "2-Hand window should be inactive before fist release.");

        testTwoHand.NotifyFistReleased();
        Assert.True(testTwoHand.State.IsWindowActive, "2-Hand window should be active after fist release.");

        TrackedHand h1 = new();
        TrackedHand h2 = new();
        h1.SmoothedLandmarks2D[8] = new Point2f(600, 300);
        h2.SmoothedLandmarks2D[8] = new Point2f(615, 300);
        h1.SmoothedLandmarks2D[0] = new Point2f(600, 400);
        h1.SmoothedLandmarks2D[9] = new Point2f(600, 350);
        h2.SmoothedLandmarks2D[0] = new Point2f(615, 400);
        h2.SmoothedLandmarks2D[9] = new Point2f(615, 350);

        List<TrackedHand> twoHandsList = new() { h1, h2 };
        testTwoHand.Update(twoHandsList, inputSink);
        testTwoHand.Update(twoHandsList, inputSink);
        Assert.True(testTwoHand.State.IsTouchActive, "Touch anchor should be active.");

        h1.SmoothedLandmarks2D[8] = new Point2f(500, 300);
        h2.SmoothedLandmarks2D[8] = new Point2f(720, 300);
        TwoHandGestureDecision? maxDecision = testTwoHand.Update(twoHandsList, inputSink);

        Assert.NotNull(maxDecision);
        Assert.Equal(TwoHandAction.Maximize, maxDecision.Action);
        Assert.True(testTwoHand.State.InCooldown, "Cooldown should be active after trigger.");
    }

    [Fact]
    public void TwoHand_CameraFrameScreenshot_TriggersAfterHold()
    {
        TwoHandGestureDetector testScreenDetector = new();
        Win32InputSink inputSink = new();

        TrackedHand lHand1 = new() { Gesture = "L" };
        lHand1.SmoothedLandmarks2D[0] = new Point2f(400, 500);
        lHand1.SmoothedLandmarks2D[2] = new Point2f(360, 470);
        lHand1.SmoothedLandmarks2D[4] = new Point2f(320, 470);
        lHand1.SmoothedLandmarks2D[5] = new Point2f(400, 430);
        lHand1.SmoothedLandmarks2D[8] = new Point2f(400, 360);
        lHand1.SmoothedLandmarks2D[9] = new Point2f(400, 430);
        lHand1.SmoothedLandmarks2D[12] = new Point2f(400, 470);
        lHand1.SmoothedLandmarks2D[13] = new Point2f(420, 440);
        lHand1.SmoothedLandmarks2D[16] = new Point2f(420, 480);
        lHand1.SmoothedLandmarks2D[17] = new Point2f(440, 450);
        lHand1.SmoothedLandmarks2D[20] = new Point2f(440, 490);

        TrackedHand lHand2 = new() { Gesture = "L" };
        lHand2.SmoothedLandmarks2D[0] = new Point2f(600, 500);
        lHand2.SmoothedLandmarks2D[2] = new Point2f(560, 470);
        lHand2.SmoothedLandmarks2D[4] = new Point2f(330, 470);
        lHand2.SmoothedLandmarks2D[5] = new Point2f(600, 430);
        lHand2.SmoothedLandmarks2D[8] = new Point2f(410, 360);
        lHand2.SmoothedLandmarks2D[9] = new Point2f(600, 430);
        lHand2.SmoothedLandmarks2D[12] = new Point2f(600, 470);
        lHand2.SmoothedLandmarks2D[13] = new Point2f(620, 440);
        lHand2.SmoothedLandmarks2D[16] = new Point2f(620, 480);
        lHand2.SmoothedLandmarks2D[17] = new Point2f(640, 450);
        lHand2.SmoothedLandmarks2D[20] = new Point2f(640, 490);

        testScreenDetector.State.RequiredScreenshotHoldSeconds = 0.01;
        List<TrackedHand> dualLHands = new() { lHand1, lHand2 };

        testScreenDetector.Update(dualLHands, inputSink);
        Thread.Sleep(20);
        TwoHandGestureDecision? screenDecision = testScreenDetector.Update(dualLHands, inputSink);

        Assert.NotNull(screenDecision);
        Assert.Equal(TwoHandAction.Screenshot, screenDecision.Action);
        Assert.True(testScreenDetector.State.IsScreenshotBlocked, "Screenshot cooldown should engage.");
    }

    [Fact]
    public void TwoHand_ClapPlayPause_TriggersMediaToggle()
    {
        TwoHandGestureDetector testPlayPauseDetector = new();
        Win32InputSink inputSink = new();

        TrackedHand palmHand1 = new() { Gesture = "Open Palm" };
        palmHand1.SmoothedLandmarks2D[0] = new Point2f(500, 500);
        palmHand1.SmoothedLandmarks2D[9] = new Point2f(500, 400);
        palmHand1.SmoothedLandmarks2D[4] = new Point2f(470, 430);
        palmHand1.SmoothedLandmarks2D[8] = new Point2f(490, 320);
        palmHand1.SmoothedLandmarks2D[12] = new Point2f(510, 310);
        palmHand1.SmoothedLandmarks2D[16] = new Point2f(530, 325);
        palmHand1.SmoothedLandmarks2D[20] = new Point2f(545, 345);

        TrackedHand palmHand2 = new() { Gesture = "Open Palm" };
        palmHand2.SmoothedLandmarks2D[0] = new Point2f(515, 500);
        palmHand2.SmoothedLandmarks2D[9] = new Point2f(515, 400);
        palmHand2.SmoothedLandmarks2D[4] = new Point2f(545, 430);
        palmHand2.SmoothedLandmarks2D[8] = new Point2f(525, 320);
        palmHand2.SmoothedLandmarks2D[12] = new Point2f(505, 310);
        palmHand2.SmoothedLandmarks2D[16] = new Point2f(485, 325);
        palmHand2.SmoothedLandmarks2D[20] = new Point2f(470, 345);

        List<TrackedHand> dualPalmHands = new() { palmHand1, palmHand2 };

        testPlayPauseDetector.Update(dualPalmHands, inputSink);
        TwoHandGestureDecision? playPauseDecision = testPlayPauseDetector.Update(dualPalmHands, inputSink);

        Assert.NotNull(playPauseDecision);
        Assert.Equal(TwoHandAction.PlayPause, playPauseDecision.Action);
        Assert.True(testPlayPauseDetector.State.IsMediaPlayPauseInCooldown, "Play/Pause cooldown should engage.");
    }
}
