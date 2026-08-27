using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.ML.OnnxRuntime;
using NEXA.Adapters.Output;
using NEXA.Common;
using NEXA.Domain.Click;
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

int webcamIndex = 0; // Index 0 = Webcam. Change if external camera is used.

// ====================================================================================================
// N.E.X.A. - Neural EXtended Augmented-Reality Gesture Controller (MediaPipe ONNX + OpenCV + Win32)
// Main Application Entry Point and Video Pipeline Loop
// ====================================================================================================

Console.WriteLine("==============================");
Console.WriteLine("  N.E.X.A. - Hand Tracking");
Console.WriteLine("==============================");

// Resolve ONNX model file paths
string palmModelPath = Path.Combine(AppContext.BaseDirectory, "models", "palm_detection.onnx");
string landmarkModelPath = Path.Combine(AppContext.BaseDirectory, "models", "handpose_estimation.onnx");

if (!File.Exists(palmModelPath) || !File.Exists(landmarkModelPath))
{
    // Fallback relative paths
    palmModelPath = "models/palm_detection.onnx";
    landmarkModelPath = "models/handpose_estimation.onnx";
}

if (!File.Exists(palmModelPath) || !File.Exists(landmarkModelPath))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[ERROR] ONNX model files not found! Expected:\n  {palmModelPath}\n  {landmarkModelPath}");
    Console.ResetColor();
    return;
}

Console.WriteLine("Loading MediaPipe ONNX Models...");

// 1. Initialize Pipeline & Output Adapters
using HandTracker tracker = new(palmModelPath, landmarkModelPath);
using FaceTracker faceTracker = new();
Win32InputSink inputSink = new();
Win32AudioSink audioSink = new();
Win32ScreenshotSink screenshotSink = new();
HandMeshRenderer renderer = new();
FaceMeshRenderer faceMeshRenderer = new();
VirtualObjectController virtualObject = new();
MouseController mouseController = new(inputSink);
ScrollController scrollController = new(inputSink);
WindowGrabController windowGrabController = new(inputSink);
TwoHandGestureController twoHandController = new(inputSink, screenshotSink);
MonitorThrowController monitorThrowController = new(inputSink);
VolumeController volumeController = new(audioSink);
LockSequenceController lockController = new(inputSink);
CircleUndoController circleUndoController = new(inputSink);
ShhhMuteController shhhMuteController = new(audioSink);

// Wire 3-second post-fist window trigger
windowGrabController.OnFistReleased += () => twoHandController.Detector.NotifyFistReleased();

// 2. Automated Non-Interactive Test Mode (--test).
if (args.Length > 0 && args[0] == "--test")
{
    Console.WriteLine("Running in automated test mode (--test)...");
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

    // Check gating: initially window should not be active
    if (testTwoHand.State.IsWindowActive) throw new Exception("2-Hand window should be inactive before fist release.");

    // Trigger fist release
    testTwoHand.NotifyFistReleased();
    if (!testTwoHand.State.IsWindowActive) throw new Exception("2-Hand window should be active after fist release.");

    // Simulate 2 hands for Maximize (Touch -> Apart)
    TrackedHand h1 = new();
    TrackedHand h2 = new();
    h1.SmoothedLandmarks2D[8] = new Point2f(600, 300); // Index 1
    h2.SmoothedLandmarks2D[8] = new Point2f(615, 300); // Index 2 (Touch distance = 15px)
    h1.SmoothedLandmarks2D[0] = new Point2f(600, 400); h1.SmoothedLandmarks2D[9] = new Point2f(600, 350);
    h2.SmoothedLandmarks2D[0] = new Point2f(615, 400); h2.SmoothedLandmarks2D[9] = new Point2f(615, 350);

    List<TrackedHand> twoHandsList = new() { h1, h2 };

    // Frame 1 & 2: Touch initiation
    testTwoHand.Update(twoHandsList, inputSink);
    testTwoHand.Update(twoHandsList, inputSink);
    if (!testTwoHand.State.IsTouchActive) throw new Exception("Touch anchor should be active.");

    // Frame 3: Move apart horizontally
    h1.SmoothedLandmarks2D[8] = new Point2f(500, 300);
    h2.SmoothedLandmarks2D[8] = new Point2f(720, 300); // Distance = 220px (>45% expansion)
    TwoHandGestureDecision? maxDecision = testTwoHand.Update(twoHandsList, inputSink);
    if (maxDecision == null || maxDecision.Action != TwoHandAction.Maximize) throw new Exception("Maximize gesture failed to trigger.");

    // Verify cooldown
    if (!testTwoHand.State.InCooldown) throw new Exception("Cooldown should be active after trigger.");

    // Automated Unit Test 3: WindowResizeDetector Simulation
    Console.WriteLine("Testing WindowResizeDetector (Continuous Pinch Resizing)...");
    WindowResizeDetector testResize = new();
    TrackedHand zoomHand = new() { Gesture = "L" };
    zoomHand.SmoothedLandmarks2D[0] = new Point2f(500, 500);
    zoomHand.SmoothedLandmarks2D[9] = new Point2f(500, 400); // Palm size = 100
    zoomHand.SmoothedLandmarks2D[4] = new Point2f(470, 400); // Thumb
    zoomHand.SmoothedLandmarks2D[8] = new Point2f(530, 400); // Index (Initial distance = 60)

    // Frame 1: Establishes baseline ratio
    (bool shouldResize1, int winW1, int winH1) = testResize.Update(zoomHand, 800, 600, 1920, 1080);
    if (!testResize.State.IsActive) throw new Exception("WindowResizeDetector should be active.");

    // Frame 2: Spread fingers wider to 120px (2.0x scale)
    zoomHand.SmoothedLandmarks2D[4] = new Point2f(440, 400);
    zoomHand.SmoothedLandmarks2D[8] = new Point2f(560, 400);
    (bool shouldResize2, int winW2, int winH2) = testResize.Update(zoomHand, 800, 600, 1920, 1080);
    if (!shouldResize2 || winW2 <= 800) throw new Exception("WindowResizeDetector failed to scale window up.");

    // Automated Unit Test 4: 8-Zone Snap-to-Side & Corner Simulation
    Console.WriteLine("Testing 8-Zone Snap Docking (Corner & Half Split)...");
    WindowGrabDetector testSnapDetector = new(1920, 1080);
    testSnapDetector.State.IsGrabbed = true;
    testSnapDetector.State.TargetHwnd = new IntPtr(999);
    testSnapDetector.State.InitialWindowBounds = new Rect(400, 300, 960, 540); // 50%x50%
    testSnapDetector.State.PreSnapBounds = new Rect(400, 300, 960, 540);
    testSnapDetector.State.InitialHandScreenX = 500;
    testSnapDetector.State.InitialHandScreenY = 400;

    TrackedHand edgeHand = new() { Gesture = "Fist" };
    // Map to top-left corner (normX = 0, normY = 0)
    edgeHand.SmoothedLandmarks2D[9] = new Point2f(1280 * 0.15f, 720 * 0.15f);

    testSnapDetector.Update(edgeHand, 1280, 720, inputSink);
    if (testSnapDetector.State.ActiveSnap != WindowSnapType.TopLeftCorner) throw new Exception("Snap Top-Left Corner failed to trigger.");
    if (testSnapDetector.State.SnapBounds.Width != 1920 / 2 || testSnapDetector.State.SnapBounds.Height != 1080 / 2) throw new Exception("Snap Top-Left dimensions incorrect.");

    // Wait for 300ms latch lock to expire, then drag inward to un-dock (screen X = ~550px > 307px)
    Thread.Sleep(310);
    edgeHand.SmoothedLandmarks2D[9] = new Point2f(1280 * 0.45f, 720 * 0.45f);
    testSnapDetector.Update(edgeHand, 1280, 720, inputSink);
    if (testSnapDetector.State.ActiveSnap != WindowSnapType.None) throw new Exception("Un-docking failed when pulling hand away from corner.");

    // Automated Unit Test 5: Monitor Throw Edge-On Recognition & Kinematics
    Console.WriteLine("Testing MonitorThrowDetector (Edge-On Swipe)...");
    MonitorThrowDetector testThrowDetector = new();
    TrackedHand bladeHand = new() { Gesture = "Open Palm" };
    bladeHand.SmoothedLandmarks2D[0] = new Point2f(500, 500);
    bladeHand.SmoothedLandmarks2D[9] = new Point2f(500, 400); // Palm size = 100
    bladeHand.SmoothedLandmarks2D[5] = new Point2f(515, 410); // Index MCP
    bladeHand.SmoothedLandmarks2D[17] = new Point2f(535, 410); // Pinky MCP (Knuckle dist = 20 <= 45% palm size -> Edge-on!)

    // Enqueue samples moving rapidly rightwards
    testThrowDetector.Update(bladeHand, inputSink);
    Thread.Sleep(30);
    bladeHand.SmoothedLandmarks2D[9] = new Point2f(540, 400);
    testThrowDetector.Update(bladeHand, inputSink);
    Thread.Sleep(30);
    bladeHand.SmoothedLandmarks2D[9] = new Point2f(590, 400); // Delta X = +90px in <100ms
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
    lHand1.SmoothedLandmarks2D[0] = new Point2f(400, 500);  // Wrist
    lHand1.SmoothedLandmarks2D[2] = new Point2f(360, 470);  // Thumb MCP
    lHand1.SmoothedLandmarks2D[4] = new Point2f(320, 470);  // Thumb Tip 1
    lHand1.SmoothedLandmarks2D[5] = new Point2f(400, 430);  // Index MCP
    lHand1.SmoothedLandmarks2D[8] = new Point2f(400, 360);  // Index Tip 1
    lHand1.SmoothedLandmarks2D[9] = new Point2f(400, 430);
    lHand1.SmoothedLandmarks2D[12] = new Point2f(400, 470);
    lHand1.SmoothedLandmarks2D[13] = new Point2f(420, 440);
    lHand1.SmoothedLandmarks2D[16] = new Point2f(420, 480);
    lHand1.SmoothedLandmarks2D[17] = new Point2f(440, 450);
    lHand1.SmoothedLandmarks2D[20] = new Point2f(440, 490);

    TrackedHand lHand2 = new() { Gesture = "L" };
    lHand2.SmoothedLandmarks2D[0] = new Point2f(600, 500);  // Wrist
    lHand2.SmoothedLandmarks2D[2] = new Point2f(560, 470);  // Thumb MCP
    lHand2.SmoothedLandmarks2D[4] = new Point2f(330, 470);  // Thumb Tip 2 (Near Thumb 1, dist=10px)
    lHand2.SmoothedLandmarks2D[5] = new Point2f(600, 430);  // Index MCP
    lHand2.SmoothedLandmarks2D[8] = new Point2f(410, 360);  // Index Tip 2 (Near Index 1, dist=10px)
    lHand2.SmoothedLandmarks2D[9] = new Point2f(600, 430);
    lHand2.SmoothedLandmarks2D[12] = new Point2f(600, 470);
    lHand2.SmoothedLandmarks2D[13] = new Point2f(620, 440);
    lHand2.SmoothedLandmarks2D[16] = new Point2f(620, 480);
    lHand2.SmoothedLandmarks2D[17] = new Point2f(640, 450);
    lHand2.SmoothedLandmarks2D[20] = new Point2f(640, 490);

    testScreenDetector.State.RequiredScreenshotHoldSeconds = 0.01;
    List<TrackedHand> dualLHands = new() { lHand1, lHand2 };

    // Frame 1: Establishes framing & starts hold
    testScreenDetector.Update(dualLHands, inputSink);
    Thread.Sleep(20);
    // Frame 2: Confirms hold -> triggers Screenshot
    TwoHandGestureDecision? screenDecision = testScreenDetector.Update(dualLHands, inputSink);
    if (screenDecision == null || screenDecision.Action != TwoHandAction.Screenshot)
        throw new Exception("Camera-Frame Screenshot gesture failed to trigger.");
    if (!testScreenDetector.State.IsScreenshotBlocked)
        throw new Exception("Screenshot disambiguation cooldown failed to engage.");

    // Automated Unit Test 8: Clap / Prayer (Play/Pause Media Control)
    Console.WriteLine("Testing TwoHandGestureDetector (Clap/Prayer Play/Pause)...");
    TwoHandGestureDetector testPlayPauseDetector = new();

    TrackedHand palmHand1 = new() { Gesture = "Open Palm" };
    palmHand1.SmoothedLandmarks2D[0] = new Point2f(500, 500); // Wrist 1
    palmHand1.SmoothedLandmarks2D[9] = new Point2f(500, 400); // Palm 1
    palmHand1.SmoothedLandmarks2D[4] = new Point2f(470, 430);
    palmHand1.SmoothedLandmarks2D[8] = new Point2f(490, 320);
    palmHand1.SmoothedLandmarks2D[12] = new Point2f(510, 310);
    palmHand1.SmoothedLandmarks2D[16] = new Point2f(530, 325);
    palmHand1.SmoothedLandmarks2D[20] = new Point2f(545, 345);

    TrackedHand palmHand2 = new() { Gesture = "Open Palm" };
    palmHand2.SmoothedLandmarks2D[0] = new Point2f(515, 500); // Wrist 2 (dist=15px)
    palmHand2.SmoothedLandmarks2D[9] = new Point2f(515, 400); // Palm 2 (dist=15px)
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

    // Step 1: Open Palm 1
    testLockDetector.Update(seqOpenHand);
    testLockDetector.Update(seqOpenHand);
    if (testLockDetector.State.CurrentStep != LockSequenceStep.OpenPalm1)
        throw new Exception("Lock Step 1 (OpenPalm1) failed to engage.");

    // Step 2: Fist 1
    testLockDetector.Update(seqFistHand);
    testLockDetector.Update(seqFistHand);
    if (testLockDetector.State.CurrentStep != LockSequenceStep.Fist1)
        throw new Exception("Lock Step 2 (Fist1) failed to engage.");

    // Step 3: Open Palm 2
    testLockDetector.Update(seqOpenHand);
    testLockDetector.Update(seqOpenHand);
    if (testLockDetector.State.CurrentStep != LockSequenceStep.OpenPalm2)
        throw new Exception("Lock Step 3 (OpenPalm2) failed to engage.");

    // Step 4: Fist 2 -> Trigger Lock!
    testLockDetector.Update(seqFistHand);
    bool didTriggerLock = testLockDetector.Update(seqFistHand);
    if (!didTriggerLock || !testLockDetector.State.InCooldown)
        throw new Exception("Lock Step 4 (Fist2) failed to trigger workstation lock.");

    // Automated Unit Test 10: Wrist-Twist Peace Sign (Undo / Redo)
    Console.WriteLine("Testing CircleUndoDetector (Peace Wrist-Twist Undo & Redo)...");
    CircleUndoDetector testUndoDetector = new();

    TrackedHand peaceHand = new() { Gesture = "Peace" };
    peaceHand.SmoothedLandmarks2D[0] = new Point2f(500, 500); // Wrist
    peaceHand.SmoothedLandmarks2D[4] = new Point2f(470, 430); // Thumb curled
    peaceHand.SmoothedLandmarks2D[5] = new Point2f(480, 420);
    peaceHand.SmoothedLandmarks2D[9] = new Point2f(500, 400); // Palm
    peaceHand.SmoothedLandmarks2D[13] = new Point2f(520, 420);
    peaceHand.SmoothedLandmarks2D[16] = new Point2f(520, 450); // Ring curled
    peaceHand.SmoothedLandmarks2D[17] = new Point2f(540, 430);
    peaceHand.SmoothedLandmarks2D[20] = new Point2f(540, 460); // Pinky curled

    // 1. Establish baseline (upright vector: 0 deg delta)
    peaceHand.SmoothedLandmarks2D[8] = new Point2f(490, 380);
    peaceHand.SmoothedLandmarks2D[12] = new Point2f(510, 380); // Tips center = (500, 380), angle = -90 deg
    testUndoDetector.Update(peaceHand);

    // 2. Twist Left / Counter-Clockwise (Tips center = (380, 380), dx=-120, dy=-120, angle = -135 deg -> delta = -45 deg <= -42 deg)
    peaceHand.SmoothedLandmarks2D[8] = new Point2f(370, 380);
    peaceHand.SmoothedLandmarks2D[12] = new Point2f(390, 380);
    CircleUndoAction undoAction = testUndoDetector.Update(peaceHand);
    if (undoAction != CircleUndoAction.Undo)
        throw new Exception("Wrist Twist Left (Undo) failed to trigger.");

    // 3. Reset cooldown and test Twist Right / Clockwise (Tips center = (620, 380), dx=+120, dy=-120, angle = -45 deg -> delta = +45 deg >= +42 deg)
    testUndoDetector.State.CooldownTimer.Reset();
    testUndoDetector.Update(peaceHand); // re-establish baseline
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
    fourFingerHand.SmoothedLandmarks2D[0] = new Point2f(600, 560); // Wrist
    fourFingerHand.SmoothedLandmarks2D[2] = new Point2f(570, 520); // Thumb MCP
    fourFingerHand.SmoothedLandmarks2D[4] = new Point2f(580, 500); // Thumb Tip (Tucked, dist=30)
    fourFingerHand.SmoothedLandmarks2D[5] = new Point2f(590, 480);
    fourFingerHand.SmoothedLandmarks2D[8] = new Point2f(590, 400); // Index Tip
    fourFingerHand.SmoothedLandmarks2D[9] = new Point2f(600, 475);
    fourFingerHand.SmoothedLandmarks2D[12] = new Point2f(600, 395); // Middle Tip
    fourFingerHand.SmoothedLandmarks2D[13] = new Point2f(610, 480);
    fourFingerHand.SmoothedLandmarks2D[16] = new Point2f(610, 400); // Ring Tip
    fourFingerHand.SmoothedLandmarks2D[17] = new Point2f(620, 490);
    fourFingerHand.SmoothedLandmarks2D[20] = new Point2f(620, 410); // Pinky Tip

    testShhh.Update(fourFingerHand, simulatedFace);
    Thread.Sleep(20);
    bool didToggleMute = testShhh.Update(fourFingerHand, simulatedFace);
    if (!didToggleMute || !testShhh.State.InCooldown)
        throw new Exception("4-Finger Mute gesture failed to trigger.");

    Console.WriteLine($"[PASS] Pipeline & All State Machines executed cleanly. Detected hands: {results.Count}");
    return;
}

// 3. Open Video Capture (Webcam Index 0)
Console.WriteLine("Opening Camera (Index 0)..."); // Index 0 = Webcam. Change if external camera is used.
using VideoCapture capture = new(webcamIndex, VideoCaptureAPIs.ANY);

if (!capture.IsOpened())
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("[ERROR] Could not open webcam (index 0). Please check your camera connection.");
    Console.ResetColor();
    return;
}

// Configure optimal camera streaming settings
capture.Set(VideoCaptureProperties.FrameWidth, 1280);
capture.Set(VideoCaptureProperties.FrameHeight, 720);
capture.Set(VideoCaptureProperties.Fps, 30);

const string windowName = "NEXA - MediaPipe Hand Tracking [ONNX]";
using Window window = new(windowName, WindowFlags.AutoSize);
using Mat frame = new();

Console.WriteLine("\nControls:");
Console.WriteLine("  [ESC] / [Q] : Exit application");
Console.WriteLine("  [C]         : Toggle Mouse Navigation & Dwell-Click");
Console.WriteLine("  [W]         : Toggle Swipe Scrolling");
Console.WriteLine("  [G]         : Toggle Real Window Grabbing & Pinch-Resizing");
Console.WriteLine("  [T]         : Toggle 2-Hand Gestures (Maximize/Minimize/Screenshot/PlayPause)");
Console.WriteLine("  [M]         : Toggle Multi-Monitor Throw (Blade Swipe)");
Console.WriteLine("  [V]         : Toggle Volume Control (L-Gesture Rotary Dial)");
Console.WriteLine("  [L]         : Toggle PC Lock Gesture (Open-Fist-Open-Fist)");
Console.WriteLine("  [U]         : Toggle Undo/Redo Gesture (Peace Wrist-Twist)");
Console.WriteLine("  [X]         : Toggle Shhh Mute Gesture (Finger to Mouth)");
Console.WriteLine("  [S]         : Toggle OneEuroFilter Smoothing");
Console.WriteLine("  [J]         : Toggle Skeleton Joint Nodes");
Console.WriteLine("  [B]         : Toggle Bounding Box & HUD Tag");
Console.WriteLine("  [R]         : Reset Virtual Object (Pos & Zoom)");
Console.WriteLine("  [H]         : Toggle Telemetry HUD Overlay\n");
Console.WriteLine("  [F]         : Toggle Face Tracking");

int frameCount = 0;
double currentFps = 0.0;
Stopwatch fpsStopwatch = Stopwatch.StartNew();
Stopwatch frameStopwatch = new();
bool showHud = true;

// ====================================================================================================
// Real-Time Frame Processing Loop
// ====================================================================================================
while (true)
{
    frameStopwatch.Restart();

    // Read latest camera frame
    if (!capture.Read(frame) || frame.Empty())
    {
        Cv2.WaitKey(10);
        continue;
    }

    // Mirror image for intuitive selfie interaction
    Cv2.Flip(frame, frame, FlipMode.Y);

    // 1. Process Multi-Stage ML Hand Tracking & Filtering
    List<TrackedHand> trackedHands = tracker.ProcessFrame(frame);
    TrackedHand? primaryHand = trackedHands.FirstOrDefault();

    // 2. Process Face & Mouth Tracking
    TrackedFace? primaryFace = faceTracker.ProcessFrame(frame);

    // 3. Process Mouse Movement & Dwell Click
    mouseController.Update(primaryHand, frame.Width, frame.Height);

    // 4. Process Vertical Swipe Scrolling & Physics Momentum
    scrollController.UpdateMomentum();
    scrollController.LastPointerActiveTime = mouseController.LastPointerActiveTime;
    scrollController.Update(primaryHand);

    // 5. Process Real Windows OS Window Grabbing, Moving & Two-Hand Pinch Resizing
    windowGrabController.Update(trackedHands, frame.Width, frame.Height);

    // 6. Process Two-Hand Gestures (Maximize / Minimize / Camera-Frame Screenshot / PlayPause)
    twoHandController.Update(trackedHands, frame.Width, frame.Height);

    // 7. Process Multi-Monitor Window Throw (Edge-On Blade Swipe)
    monitorThrowController.Update(primaryHand);

    // 8. Process System Master Volume Control (L-Gesture Rotary Dial)
    volumeController.Update(primaryHand);

    // 9. Process 4-Stage Security PC Lock Sequence (🖐️ -> ✊ -> 🖐️ -> ✊)
    lockController.Update(primaryHand);

    // 10. Process Peace-Sign Wrist-Twist Undo (Ctrl+Z) & Redo (Ctrl+Y)
    circleUndoController.Update(primaryHand);

    // 11. Process "Shhh" (🤫) Audio Mute Toggle
    shhhMuteController.Update(primaryHand, primaryFace);

    // 12. Process Relative Grab & Zoom on Virtual Object (when real window grab is idle)
    virtualObject.Update(primaryHand, frame.Width, frame.Height);

    // 13. Render Hand Skeleton Bones Overlay
    // 13. Render 68-Point Facial Landmarks & Contour Mesh
    faceMeshRenderer.Render(frame, primaryFace);

    // 14. Render Hand Skeleton Bones Overlay
    renderer.Render(frame, trackedHands);

    // 15. Render Mouse Click Feedback & Dwell Ring
    mouseController.RenderFeedback(frame, primaryHand);

    // 16. Render Swipe Scroll Feedback Arrows
    scrollController.RenderFeedback(frame);

    // 17. Render Real Window Grab Feedback, Scaled Corner Brackets & Pinch Caliper
    windowGrabController.RenderFeedback(frame);

    // 18. Render Two-Hand Gesture Banner, Viewfinder Box, White Flash & Action Animations
    twoHandController.RenderFeedback(frame, trackedHands);

    // 19. Render Multi-Monitor Blade Pose & Holographic Transfer Arrows
    monitorThrowController.RenderFeedback(frame, primaryHand);

    // 20. Render Holographic Rotary Volume Dial & Live Audio Gauge
    volumeController.RenderFeedback(frame);

    // 21. Render 4-Stage Security PC Lock Sequence Badges & Alert
    lockController.RenderFeedback(frame, primaryHand);

    // 22. Render Circular Trajectory Spiral Trail & Revolution Gauge
    circleUndoController.RenderFeedback(frame, primaryHand);

    // 23. Render "Shhh" Mouth Reticle & Mute Animation
    shhhMuteController.RenderFeedback(frame, primaryFace, primaryHand);

    // 24. Render Virtual Test Target Object
    virtualObject.Render(frame);

    // 25. Real-time FPS Calculation
    frameCount++;
    if (fpsStopwatch.ElapsedMilliseconds >= 500)
    {
        currentFps = frameCount * 1000.0 / fpsStopwatch.ElapsedMilliseconds;
        frameCount = 0;
        fpsStopwatch.Restart();
    }

    // 26. Render Telemetry HUD
    if (showHud)
        DrawHud(frame, currentFps, trackedHands.Count, tracker.SmoothingEnabled, virtualObject, mouseController, scrollController, windowGrabController, twoHandController, monitorThrowController, volumeController, lockController, circleUndoController, shhhMuteController);
    
    // 27. Display Frame in OpenCV Window
    window.ShowImage(frame);

    // 28. Handle Keyboard Hotkeys
    int key = Cv2.WaitKey(1);
    if (key == 27 || key == 'q' || key == 'Q') // ESC or Q
    {
        break;
    }
    else if (key == 'c' || key == 'C')
    {
        mouseController.Enabled = !mouseController.Enabled;
    }
    else if (key == 'w' || key == 'W')
    {
        scrollController.Enabled = !scrollController.Enabled;
    }
    else if (key == 'g' || key == 'G')
    {
        windowGrabController.Enabled = !windowGrabController.Enabled;
    }
    else if (key == 't' || key == 'T')
    {
        twoHandController.Enabled = !twoHandController.Enabled;
    }
    else if (key == 'm' || key == 'M')
    {
        monitorThrowController.Enabled = !monitorThrowController.Enabled;
    }
    else if (key == 'v' || key == 'V')
    {
        volumeController.Enabled = !volumeController.Enabled;
    }
    else if (key == 'l' || key == 'L')
    {
        lockController.Enabled = !lockController.Enabled;
    }
    else if (key == 'u' || key == 'U')
    {
        circleUndoController.Enabled = !circleUndoController.Enabled;
    }
    else if (key == 'x' || key == 'X')
    {
        shhhMuteController.Enabled = !shhhMuteController.Enabled;
    }
    else if (key == 'f' || key == 'F')
    {
        faceMeshRenderer.ShowMeshWidget = !faceMeshRenderer.ShowMeshWidget;
        faceMeshRenderer.ShowHeadBoundingBox = !faceMeshRenderer.ShowHeadBoundingBox;
    }
    else if (key == 's' || key == 'S')
    {
        tracker.SmoothingEnabled = !tracker.SmoothingEnabled;
    }
    else if (key == 'j' || key == 'J')
    {
        renderer.ShowJoints = !renderer.ShowJoints;
    }
    else if (key == 'b' || key == 'B')
    {
        renderer.ShowBoundingBox = !renderer.ShowBoundingBox;
    }
    else if (key == 'r' || key == 'R')
    {
        virtualObject.Reset(frame.Width, frame.Height);
    }
    else if (key == 'h' || key == 'H')
    {
        showHud = !showHud;
    }
}

Console.WriteLine("Shutting down NEXA Hand Tracking...");

/// <summary>
/// Draws the semi-transparent telemetry HUD card with live FPS, filter states, and controller status indicators.
/// </summary>
static void DrawHud(Mat frame, double fps, int handsCount, bool smoothed, VirtualObjectController objCtrl, MouseController mouseCtrl, ScrollController scrollCtrl, WindowGrabController grabCtrl, TwoHandGestureController twoHandCtrl, MonitorThrowController throwCtrl, VolumeController volCtrl, LockSequenceController lockCtrl, CircleUndoController undoCtrl, ShhhMuteController muteCtrl)
{
    Rect hudRect = new(10, 10, 390, 238);
    using Mat overlay = frame.Clone();
    Cv2.Rectangle(overlay, hudRect, new Scalar(10, 10, 15), -1);
    Cv2.AddWeighted(overlay, 0.7, frame, 0.3, 0, frame);
    Cv2.Rectangle(frame, hudRect, new Scalar(0, 220, 255), 1);

    Cv2.PutText(frame, "NEXA HAND MOUSE & WINDOWS (ONNX)", new Point(20, 28),
        HersheyFonts.HersheySimplex, 0.48, new Scalar(0, 255, 255), 1, LineTypes.AntiAlias);

    Cv2.PutText(frame, $"FPS: {fps:F1} | Hands: {handsCount} | Filter: {(smoothed ? "ON" : "OFF")}", new Point(20, 48),
        HersheyFonts.HersheySimplex, 0.36, new Scalar(255, 255, 255), 1, LineTypes.AntiAlias);

    string mouseStatus = mouseCtrl.Enabled
        ? (mouseCtrl.DwellState.IsHovering && mouseCtrl.DwellState.HoverProgress > 0.05
            ? $"Verweilklick: {(int)(mouseCtrl.DwellState.HoverProgress * 100)}%"
            : "Aktiv (Zeigen)")
        : "AUS (Taste C)";
    Scalar mouseColor = mouseCtrl.Enabled ? new Scalar(0, 255, 120) : new Scalar(160, 160, 160);

    Cv2.PutText(frame, $"Maus (C): {mouseStatus}", new Point(20, 66),
        HersheyFonts.HersheySimplex, 0.36, mouseColor, 1, LineTypes.AntiAlias);

    string scrollStatus;
    Scalar scrollColor;

    if (!scrollCtrl.Enabled)
    {
        scrollStatus = "AUS (Taste W)";
        scrollColor = new Scalar(160, 160, 160);
    }
    else if (!scrollCtrl.IsWindowActive)
    {
        scrollStatus = "Gesperrt (Erst Zeigen)";
        scrollColor = new Scalar(120, 120, 120);
    }
    else if (scrollCtrl.State.WaitingForRest)
    {
        scrollStatus = $"Cooldown ({scrollCtrl.RemainingWindowSeconds:F1}s)";
        scrollColor = new Scalar(0, 180, 255);
    }
    else
    {
        scrollStatus = $"Bereit ({scrollCtrl.RemainingWindowSeconds:F1}s)";
        scrollColor = new Scalar(0, 255, 120);
    }

    Cv2.PutText(frame, $"Scroll (W): {scrollStatus}", new Point(20, 84),
        HersheyFonts.HersheySimplex, 0.36, scrollColor, 1, LineTypes.AntiAlias);

    string winGrabStatus;
    Scalar winGrabColor;
    if (!grabCtrl.Enabled)
    {
        winGrabStatus = "AUS (Taste G)";
        winGrabColor = new Scalar(160, 160, 160);
    }
    else if (grabCtrl.State.IsSnapped)
    {
        winGrabStatus = $"Docked ({grabCtrl.State.ActiveSnap})";
        winGrabColor = new Scalar(255, 160, 0);
    }
    else if (grabCtrl.ResizeState.IsActive)
    {
        winGrabStatus = $"Resize: {grabCtrl.ResizeState.CurrentWidth}x{grabCtrl.ResizeState.CurrentHeight} ({grabCtrl.ResizeState.CurrentScale:F2}x)";
        winGrabColor = new Scalar(0, 220, 255);
    }
    else if (grabCtrl.State.IsGrabbed)
    {
        winGrabStatus = $"Gegriffen [{NEXA.Common.TextSanitizer.ToSafeAscii(grabCtrl.State.CachedWindowTitle)}]";
        winGrabColor = new Scalar(0, 100, 255);
    }
    else if (grabCtrl.State.HoldDurationSeconds > 0)
    {
        winGrabStatus = $"Halte Faust ({grabCtrl.State.HoldDurationSeconds:F1}s / {grabCtrl.State.RequiredHoldSeconds:F1}s)";
        winGrabColor = new Scalar(0, 165, 255);
    }
    else
    {
        winGrabStatus = "Bereit (Faust 2s)";
        winGrabColor = new Scalar(0, 255, 120);
    }

    Cv2.PutText(frame, NEXA.Common.TextSanitizer.ToSafeAscii($"Fenster (G): {winGrabStatus}"), new Point(20, 102),
        HersheyFonts.HersheySimplex, 0.36, winGrabColor, 1, LineTypes.AntiAlias);

    string twoHandStatus;
    Scalar twoHandColor;
    if (!twoHandCtrl.Enabled)
    {
        twoHandStatus = "AUS (Taste T)";
        twoHandColor = new Scalar(160, 160, 160);
    }
    else if ((DateTime.Now - twoHandCtrl.State.LastMediaPlayPauseTime).TotalMilliseconds < 1500)
    {
        twoHandStatus = "> || PLAY / PAUSE gesendet!";
        twoHandColor = new Scalar(0, 220, 255);
    }
    else if (twoHandCtrl.State.IsCameraFrameActive)
    {
        twoHandStatus = "Kamera-Rahmen (2s Zusammenhalten)";
        twoHandColor = new Scalar(0, 255, 120);
    }
    else if ((DateTime.Now - twoHandCtrl.State.LastScreenshotTime).TotalMilliseconds < 1500)
    {
        twoHandStatus = "Screenshot gespeichert & kopiert!";
        twoHandColor = new Scalar(255, 255, 255);
    }
    else if (twoHandCtrl.State.InCooldown)
    {
        twoHandStatus = $"Cooldown ({twoHandCtrl.State.LastAction})";
        twoHandColor = new Scalar(0, 180, 255);
    }
    else if (twoHandCtrl.State.IsWindowActive)
    {
        twoHandStatus = $"Fenster ({twoHandCtrl.State.RemainingWindowSeconds:F1}s)";
        twoHandColor = new Scalar(0, 255, 120);
    }
    else
    {
        twoHandStatus = "Bereit (Klatsch >|| / Doppel-L / Faust)";
        twoHandColor = new Scalar(120, 120, 120);
    }

    Cv2.PutText(frame, NEXA.Common.TextSanitizer.ToSafeAscii($"Zwei-Hand (T): {twoHandStatus}"), new Point(20, 120),
        HersheyFonts.HersheySimplex, 0.36, twoHandColor, 1, LineTypes.AntiAlias);

    string throwStatus = throwCtrl.Enabled
        ? (throwCtrl.State.InCooldown ? "Cooldown (800ms)" : (throwCtrl.State.IsEdgeOnPosture ? "Handkante erkannt!" : "Bereit (Handkante Wisch)"))
        : "AUS (Taste M)";
    Scalar throwColor = throwCtrl.Enabled
        ? (throwCtrl.State.IsEdgeOnPosture ? new Scalar(255, 100, 200) : new Scalar(0, 255, 120))
        : new Scalar(160, 160, 160);

    Cv2.PutText(frame, NEXA.Common.TextSanitizer.ToSafeAscii($"Monitor (M): {throwStatus}"), new Point(20, 138),
        HersheyFonts.HersheySimplex, 0.36, throwColor, 1, LineTypes.AntiAlias);

    string volStatus;
    Scalar volColor;
    if (!volCtrl.Enabled)
    {
        volStatus = "AUS (Taste V)";
        volColor = new Scalar(160, 160, 160);
    }
    else if (volCtrl.State.IsActive)
    {
        volStatus = $"Aktiv: {(int)(volCtrl.State.SmoothedVolume * 100)}% ({volCtrl.State.AngleDelta:+0;-0;0} deg)";
        volColor = new Scalar(0, 255, 120);
    }
    else
    {
        volStatus = "Bereit (L-Geste + Drehung)";
        volColor = new Scalar(0, 255, 120);
    }

    Cv2.PutText(frame, NEXA.Common.TextSanitizer.ToSafeAscii($"Lautstaerke (V): {volStatus}"), new Point(20, 156),
        HersheyFonts.HersheySimplex, 0.36, volColor, 1, LineTypes.AntiAlias);

    string lockStatus;
    Scalar lockColor;
    if (!lockCtrl.Enabled)
    {
        lockStatus = "AUS (Taste L)";
        lockColor = new Scalar(160, 160, 160);
    }
    else if (lockCtrl.State.CurrentStep != LockSequenceStep.Idle)
    {
        lockStatus = $"Sequenz: {(int)lockCtrl.State.CurrentStep}/4 ({lockCtrl.State.StepTimeoutSeconds - lockCtrl.State.StepTimer.Elapsed.TotalSeconds:F1}s)";
        lockColor = new Scalar(0, 220, 255);
    }
    else
    {
        lockStatus = "Bereit (Offen-Faust-Offen-Faust)";
        lockColor = new Scalar(0, 255, 120);
    }

    Cv2.PutText(frame, NEXA.Common.TextSanitizer.ToSafeAscii($"Sperren (L): {lockStatus}"), new Point(20, 174),
        HersheyFonts.HersheySimplex, 0.36, lockColor, 1, LineTypes.AntiAlias);

    string undoStatus;
    Scalar undoColor;
    if (!undoCtrl.Enabled)
    {
        undoStatus = "AUS (Taste U)";
        undoColor = new Scalar(160, 160, 160);
    }
    else if (undoCtrl.State.IsTracking && Math.Abs(undoCtrl.State.AngleDeltaDeg) > 5.0)
    {
        string dir = undoCtrl.State.AngleDeltaDeg < 0.0 ? "Undo <--" : "Redo -->";
        string sign = undoCtrl.State.AngleDeltaDeg >= 0 ? "+" : "";
        undoStatus = $"{dir} ({sign}{undoCtrl.State.AngleDeltaDeg:F0} deg / 42 deg)";
        undoColor = undoCtrl.State.AngleDeltaDeg < 0.0 ? new Scalar(0, 220, 255) : new Scalar(255, 160, 0);
    }
    else
    {
        undoStatus = "Bereit (Peace Handgelenk-Dreh)";
        undoColor = new Scalar(0, 255, 120);
    }

    Cv2.PutText(frame, NEXA.Common.TextSanitizer.ToSafeAscii($"Undo/Redo (U): {undoStatus}"), new Point(20, 192),
        HersheyFonts.HersheySimplex, 0.36, undoColor, 1, LineTypes.AntiAlias);

    string muteStatus;
    Scalar muteColor;
    if (!muteCtrl.Enabled)
    {
        muteStatus = "AUS (Taste X)";
        muteColor = new Scalar(160, 160, 160);
    }
    else if (muteCtrl.State.IsInProximity)
    {
        muteStatus = $"Muten: {(int)(muteCtrl.State.HoldProgress * 100)}%";
        muteColor = new Scalar(0, 0, 255);
    }
    else
    {
        muteStatus = muteCtrl.State.IsMuted ? "STUMM (4 Finger vor Mund)" : "Aktiv (4 Finger vor Mund 🤫)";
        muteColor = muteCtrl.State.IsMuted ? new Scalar(0, 0, 255) : new Scalar(0, 255, 120);
    }

    Cv2.PutText(frame, NEXA.Common.TextSanitizer.ToSafeAscii($"Mikro (X): {muteStatus}"), new Point(20, 210),
        HersheyFonts.HersheySimplex, 0.36, muteColor, 1, LineTypes.AntiAlias);

    string grabLabel = objCtrl.GrabState.Active ? "GRABBED" : (objCtrl.GrabState.HoldDurationSeconds > 0 ? $"HOLD {objCtrl.GrabState.HoldDurationSeconds:F1}s" : "Ready");
    string objStatus = $"Testobjekt: {grabLabel} | Zoom: {objCtrl.ZoomState.CurrentZoom:F2}x (R)";
    Cv2.PutText(frame, NEXA.Common.TextSanitizer.ToSafeAscii(objStatus), new Point(20, 228),
        HersheyFonts.HersheySimplex, 0.34, new Scalar(200, 200, 200), 1, LineTypes.AntiAlias);
}