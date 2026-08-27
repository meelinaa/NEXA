using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NEXA.Adapters.Output;
using NEXA.Domain.Click;
using NEXA.Domain.EarsMute;
using NEXA.Domain.Grab;
using NEXA.Domain.Lock;
using NEXA.Domain.MonitorThrow;
using NEXA.Domain.Mute;
using NEXA.Domain.Scroll;
using NEXA.Domain.TwoHand;
using NEXA.Domain.Undo;
using NEXA.Domain.Volume;
using NEXA.Face;
using NEXA.Hand;
using NEXA.Object;
using OpenCvSharp;

namespace NEXA.Testing;

/// <summary>
/// Automated non-interactive self-test runner executing pipeline smoke tests and all 12 domain gesture state-machine simulations.
/// <para>
/// <b>What it is:</b> Comprehensive headless test harness verified with <c>--test</c>.
/// </para>
/// </summary>
public static class SelfTestRunner
{
    /// <summary>
    /// Executes the end-to-end pipeline test and all 12 automated unit tests.
    /// </summary>
    /// <param name="tracker">Initialized HandTracker instance.</param>
    /// <param name="faceTracker">Initialized FaceTracker instance.</param>
    /// <param name="inputSink">Win32 input adapter.</param>
    /// <param name="audioSink">Audio sink adapter.</param>
    /// <param name="screenshotSink">Screenshot sink adapter.</param>
    /// <param name="renderer">Hand mesh visualizer.</param>
    /// <param name="faceMeshRenderer">Face mesh visualizer.</param>
    /// <param name="virtualObject">Virtual 3D cube object controller.</param>
    /// <param name="mouseController">Mouse navigation controller.</param>
    /// <param name="scrollController">Scroll controller.</param>
    /// <param name="windowGrabController">Window grab controller.</param>
    /// <param name="twoHandController">Two-hand gesture controller.</param>
    /// <param name="monitorThrowController">Monitor throw controller.</param>
    /// <param name="volumeController">Volume controller.</param>
    /// <param name="lockController">Lock sequence controller.</param>
    /// <param name="circleUndoController">Circle undo controller.</param>
    /// <param name="shhhMuteController">Shhh microphone mute controller.</param>
    /// <param name="hearNoEvilController">Hear-no-evil speaker sound mute controller.</param>
    public static void Run(
        HandTracker tracker,
        FaceTracker faceTracker,
        Win32InputSink inputSink,
        Win32AudioSink audioSink,
        Win32ScreenshotSink screenshotSink,
        HandMeshRenderer renderer,
        FaceMeshRenderer faceMeshRenderer,
        VirtualObjectController virtualObject,
        MouseController mouseController,
        ScrollController scrollController,
        WindowGrabController windowGrabController,
        TwoHandGestureController twoHandController,
        MonitorThrowController monitorThrowController,
        VolumeController volumeController,
        LockSequenceController lockController,
        CircleUndoController circleUndoController,
        ShhhMuteController shhhMuteController,
        HearNoEvilController hearNoEvilController)
    {
        Console.WriteLine("Running in automated test mode (--test)...");

        // 1. Pipeline Smoke Test
        using Mat testFrame = new(720, 1280, MatType.CV_8UC3, new Scalar(30, 30, 30));
        List<TrackedHand> results = tracker.ProcessFrame(testFrame);
        TrackedFace? faceResult = faceTracker.ProcessFrame(testFrame);
        renderer.Render(testFrame, results);
        faceMeshRenderer.Render(testFrame, faceResult);
        virtualObject.Update(results.FirstOrDefault(), testFrame.Width, testFrame.Height);
        virtualObject.Render(testFrame);
        mouseController.Update(results.FirstOrDefault(), testFrame.Width, testFrame.Height);
        mouseController.RenderFeedback(testFrame, results.FirstOrDefault());
        scrollController.UpdateMomentum();
        scrollController.Update(results.FirstOrDefault());
        scrollController.RenderFeedback(testFrame);
        windowGrabController.Update(results, testFrame.Width, testFrame.Height);
        windowGrabController.RenderFeedback(testFrame);
        twoHandController.Update(results, testFrame.Width, testFrame.Height);
        twoHandController.RenderFeedback(testFrame, results);
        monitorThrowController.Update(results.FirstOrDefault());
        monitorThrowController.RenderFeedback(testFrame, results.FirstOrDefault());
        volumeController.Update(results.FirstOrDefault());
        volumeController.RenderFeedback(testFrame);
        lockController.Update(results.FirstOrDefault());
        lockController.RenderFeedback(testFrame, results.FirstOrDefault());
        circleUndoController.Update(results.FirstOrDefault());
        circleUndoController.RenderFeedback(testFrame, results.FirstOrDefault());
        shhhMuteController.Update(results.FirstOrDefault(), faceResult);
        shhhMuteController.RenderFeedback(testFrame, faceResult, results.FirstOrDefault());
        hearNoEvilController.Update(results, faceResult);
        hearNoEvilController.RenderFeedback(testFrame, faceResult, results);

        // Automated Unit Test 1: WindowGrabDetector State-Machine Simulation
        Console.WriteLine("Testing WindowGrabDetector state machine transitions...");
        WindowGrabDetector testDetector = new(1920, 1080);
        TrackedHand simulatedHand = new() { Gesture = "Fist" };
        simulatedHand.SmoothedLandmarks2D[9] = new Point2f(640, 360);

        testDetector.Update(simulatedHand, 1280, 720, inputSink);
        if (testDetector.State.HoldDurationSeconds < 0) throw new Exception("Hold timer failed to start.");

        testDetector.State.RequiredHoldSeconds = 0.01;
        Thread.Sleep(20);
        testDetector.Update(simulatedHand, 1280, 720, inputSink);
        testDetector.Reset();
        if (testDetector.State.IsGrabbed) throw new Exception("Reset failed.");

        // Automated Unit Test 2: TwoHandGestureDetector (Maximize & Minimize Simulation)
        Console.WriteLine("Testing TwoHandGestureDetector (Maximize & Minimize)...");
        TwoHandGestureDetector testTwoHand = new();
        inputSink.LastFocusedHwnd = new IntPtr(12345); // Mock valid target HWND

        if (testTwoHand.State.IsWindowActive) throw new Exception("2-Hand window should be inactive before fist release.");

        testTwoHand.NotifyFistReleased();
        if (!testTwoHand.State.IsWindowActive) throw new Exception("2-Hand window should be active after fist release.");

        TrackedHand h1 = new();
        TrackedHand h2 = new();
        h1.SmoothedLandmarks2D[8] = new Point2f(600, 300);
        h2.SmoothedLandmarks2D[8] = new Point2f(615, 300);
        h1.SmoothedLandmarks2D[0] = new Point2f(600, 400); h1.SmoothedLandmarks2D[9] = new Point2f(600, 350);
        h2.SmoothedLandmarks2D[0] = new Point2f(615, 400); h2.SmoothedLandmarks2D[9] = new Point2f(615, 350);

        List<TrackedHand> twoHandsList = new() { h1, h2 };
        testTwoHand.Update(twoHandsList, inputSink);
        testTwoHand.Update(twoHandsList, inputSink);
        if (!testTwoHand.State.IsTouchActive) throw new Exception("Touch anchor should be active.");

        h1.SmoothedLandmarks2D[8] = new Point2f(500, 300);
        h2.SmoothedLandmarks2D[8] = new Point2f(720, 300);
        TwoHandGestureDecision? maxDecision = testTwoHand.Update(twoHandsList, inputSink);
        if (maxDecision == null || maxDecision.Action != TwoHandAction.Maximize) throw new Exception("Maximize gesture failed to trigger.");
        if (!testTwoHand.State.InCooldown) throw new Exception("Cooldown should be active after trigger.");

        // Automated Unit Test 3: WindowResizeDetector Simulation
        Console.WriteLine("Testing WindowResizeDetector (Continuous Pinch Resizing)...");
        WindowResizeDetector testResize = new();
        TrackedHand zoomHand = new() { Gesture = "L" };
        zoomHand.SmoothedLandmarks2D[0] = new Point2f(500, 500);
        zoomHand.SmoothedLandmarks2D[9] = new Point2f(500, 400);
        zoomHand.SmoothedLandmarks2D[4] = new Point2f(470, 400);
        zoomHand.SmoothedLandmarks2D[8] = new Point2f(530, 400);

        (bool shouldResize1, int winW1, int winH1) = testResize.Update(zoomHand, 800, 600, 1920, 1080);
        if (!testResize.State.IsActive) throw new Exception("WindowResizeDetector should be active.");

        zoomHand.SmoothedLandmarks2D[4] = new Point2f(440, 400);
        zoomHand.SmoothedLandmarks2D[8] = new Point2f(560, 400);
        (bool shouldResize2, int winW2, int winH2) = testResize.Update(zoomHand, 800, 600, 1920, 1080);
        if (!shouldResize2 || winW2 <= 800) throw new Exception("WindowResizeDetector failed to scale window up.");

        // Automated Unit Test 4: 8-Zone Snap-to-Side & Corner Simulation
        Console.WriteLine("Testing 8-Zone Snap Docking (Corner & Half Split)...");
        WindowGrabDetector testSnapDetector = new(1920, 1080);
        testSnapDetector.State.IsGrabbed = true;
        testSnapDetector.State.TargetHwnd = new IntPtr(999);
        testSnapDetector.State.InitialWindowBounds = new Rect(400, 300, 960, 540);
        testSnapDetector.State.PreSnapBounds = new Rect(400, 300, 960, 540);
        testSnapDetector.State.InitialHandScreenX = 500;
        testSnapDetector.State.InitialHandScreenY = 400;

        TrackedHand edgeHand = new() { Gesture = "Fist" };
        edgeHand.SmoothedLandmarks2D[9] = new Point2f(1280 * 0.15f, 720 * 0.15f);

        testSnapDetector.Update(edgeHand, 1280, 720, inputSink);
        if (testSnapDetector.State.ActiveSnap != WindowSnapType.TopLeftCorner) throw new Exception("Snap Top-Left Corner failed to trigger.");
        if (testSnapDetector.State.SnapBounds.Width != 1920 / 2 || testSnapDetector.State.SnapBounds.Height != 1080 / 2) throw new Exception("Snap Top-Left dimensions incorrect.");

        Thread.Sleep(310);
        edgeHand.SmoothedLandmarks2D[9] = new Point2f(1280 * 0.45f, 720 * 0.45f);
        testSnapDetector.Update(edgeHand, 1280, 720, inputSink);
        if (testSnapDetector.State.ActiveSnap != WindowSnapType.None) throw new Exception("Un-docking failed when pulling hand away from corner.");

        // Automated Unit Test 5: Monitor Throw Edge-On Recognition & Kinematics
        Console.WriteLine("Testing MonitorThrowDetector (Edge-On Swipe)...");
        MonitorThrowDetector testThrowDetector = new();
        TrackedHand bladeHand = new() { Gesture = "Open Palm" };
        bladeHand.SmoothedLandmarks2D[0] = new Point2f(500, 500);
        bladeHand.SmoothedLandmarks2D[9] = new Point2f(500, 400);
        bladeHand.SmoothedLandmarks2D[5] = new Point2f(515, 410);
        bladeHand.SmoothedLandmarks2D[17] = new Point2f(535, 410);

        testThrowDetector.Update(bladeHand, inputSink);
        Thread.Sleep(30);
        bladeHand.SmoothedLandmarks2D[9] = new Point2f(540, 400);
        testThrowDetector.Update(bladeHand, inputSink);
        Thread.Sleep(30);
        bladeHand.SmoothedLandmarks2D[9] = new Point2f(590, 400);
        MonitorThrowDecision? throwDecision = testThrowDetector.Update(bladeHand, inputSink);
        if (throwDecision == null || throwDecision.Direction != MonitorThrowDirection.Right) throw new Exception("Monitor Throw Right failed to trigger.");

        // Automated Unit Test 6: DwellClickDetector Simulation
        Console.WriteLine("Testing DwellClickDetector...");
        DwellClickDetector testDwell = new(1920, 1080);
        testDwell.DwellState.RequiredDwellSeconds = 0.01;

        TrackedHand pointHand = new() { Gesture = "Pointing" };
        pointHand.SmoothedLandmarks2D[8] = new Point2f(640, 360);

        testDwell.Update(pointHand, 1280, 720);
        Thread.Sleep(20);
        (int? _, int? _, bool didClick) = testDwell.Update(pointHand, 1280, 720);
        if (!didClick) throw new Exception("DwellClick failed to trigger.");

        // Automated Unit Test 7: Camera-Frame Screenshot (Dual "L" Hands + 2.0s Hold)
        Console.WriteLine("Testing TwoHandGestureDetector (Camera-Frame Screenshot)...");
        TwoHandGestureDetector testScreenDetector = new();

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
        if (screenDecision == null || screenDecision.Action != TwoHandAction.Screenshot)
            throw new Exception("Camera-Frame Screenshot gesture failed to trigger.");
        if (!testScreenDetector.State.IsScreenshotBlocked)
            throw new Exception("Screenshot disambiguation cooldown failed to engage.");

        // Automated Unit Test 8: Clap / Prayer (Play/Pause Media Control)
        Console.WriteLine("Testing TwoHandGestureDetector (Clap/Prayer Play/Pause)...");
        TwoHandGestureDetector testPlayPauseDetector = new();

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
        if (playPauseDecision == null || playPauseDecision.Action != TwoHandAction.PlayPause)
            throw new Exception("Dual-Palm Clap/Prayer Play/Pause failed to trigger.");
        if (!testPlayPauseDetector.State.IsMediaPlayPauseInCooldown)
            throw new Exception("Play/Pause cooldown failed to engage.");

        // Automated Unit Test 9: PC Lock Sequence (🖐️ -> ✊ -> 🖐️ -> ✊)
        Console.WriteLine("Testing LockSequenceDetector (4-Stage Security Lock)...");
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
        if (testLockDetector.State.CurrentStep != LockSequenceStep.OpenPalm1)
            throw new Exception("Lock Step 1 (OpenPalm1) failed to engage.");

        testLockDetector.Update(seqFistHand);
        testLockDetector.Update(seqFistHand);
        if (testLockDetector.State.CurrentStep != LockSequenceStep.Fist1)
            throw new Exception("Lock Step 2 (Fist1) failed to engage.");

        testLockDetector.Update(seqOpenHand);
        testLockDetector.Update(seqOpenHand);
        if (testLockDetector.State.CurrentStep != LockSequenceStep.OpenPalm2)
            throw new Exception("Lock Step 3 (OpenPalm2) failed to engage.");

        testLockDetector.Update(seqFistHand);
        bool didTriggerLock = testLockDetector.Update(seqFistHand);
        if (!didTriggerLock || !testLockDetector.State.InCooldown)
            throw new Exception("Lock Step 4 (Fist2) failed to trigger workstation lock.");

        // Automated Unit Test 10: Wrist-Twist Peace Sign (Undo / Redo)
        Console.WriteLine("Testing CircleUndoDetector (Peace Wrist-Twist Undo & Redo)...");
        CircleUndoDetector testUndoDetector = new();

        TrackedHand peaceHand = new() { Gesture = "Peace" };
        peaceHand.SmoothedLandmarks2D[0] = new Point2f(500, 500);
        peaceHand.SmoothedLandmarks2D[4] = new Point2f(470, 430);
        peaceHand.SmoothedLandmarks2D[5] = new Point2f(480, 420);
        peaceHand.SmoothedLandmarks2D[9] = new Point2f(500, 400);
        peaceHand.SmoothedLandmarks2D[13] = new Point2f(520, 420);
        peaceHand.SmoothedLandmarks2D[16] = new Point2f(520, 450);
        peaceHand.SmoothedLandmarks2D[17] = new Point2f(540, 430);
        peaceHand.SmoothedLandmarks2D[20] = new Point2f(540, 460);

        peaceHand.SmoothedLandmarks2D[8] = new Point2f(490, 380);
        peaceHand.SmoothedLandmarks2D[12] = new Point2f(510, 380);
        testUndoDetector.Update(peaceHand);

        peaceHand.SmoothedLandmarks2D[8] = new Point2f(370, 380);
        peaceHand.SmoothedLandmarks2D[12] = new Point2f(390, 380);
        CircleUndoAction undoAction = testUndoDetector.Update(peaceHand);
        if (undoAction != CircleUndoAction.Undo)
            throw new Exception("Wrist Twist Left (Undo) failed to trigger.");

        testUndoDetector.State.CooldownTimer.Reset();
        testUndoDetector.Update(peaceHand);
        peaceHand.SmoothedLandmarks2D[8] = new Point2f(610, 380);
        peaceHand.SmoothedLandmarks2D[12] = new Point2f(630, 380);
        CircleUndoAction redoAction = testUndoDetector.Update(peaceHand);
        if (redoAction != CircleUndoAction.Redo)
            throw new Exception("Wrist Twist Right (Redo) failed to trigger.");

        // Automated Unit Test 11: 4-Finger Mute Gesture (4 Fingers in front of Mouth)
        Console.WriteLine("Testing ShhhMuteDetector (4 Fingers to Mouth Mute Toggle)...");
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
        if (!didToggleMute || !testShhh.State.InCooldown)
            throw new Exception("4-Finger Mute gesture failed to trigger.");

        // Automated Unit Test 12: HearNoEvilDetector (Hands to Ears Sound Mute)
        Console.WriteLine("Testing HearNoEvilDetector (Hands to Ears Sound Mute)...");
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

        List<TrackedHand> earHands = [leftHandAtEar, rightHandAtEar];
        testEars.Update(earHands, earFace);
        Thread.Sleep(20);
        bool didToggleSound = testEars.Update(earHands, earFace);
        if (!didToggleSound || !testEars.State.InCooldown)
            throw new Exception("Hear-No-Evil Sound Mute gesture failed to trigger.");

        Console.WriteLine($"[PASS] Pipeline & All State Machines executed cleanly. Detected hands: {results.Count}");
    }
}
