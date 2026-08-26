using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using NEXA.Adapters.Output;
using NEXA.Domain.Click;
using NEXA.Domain.Grab;
using NEXA.Domain.Scroll;
using NEXA.Domain.TwoHand;
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
Win32InputSink inputSink = new();
HandMeshRenderer renderer = new();
VirtualObjectController virtualObject = new();
MouseController mouseController = new(inputSink);
ScrollController scrollController = new(inputSink);
WindowGrabController windowGrabController = new(inputSink);
TwoHandGestureController twoHandController = new(inputSink);

// Wire 3-second post-fist window trigger
windowGrabController.OnFistReleased += () => twoHandController.Detector.NotifyFistReleased();

// 2. Automated Non-Interactive Test Mode (--test).
if (args.Length > 0 && args[0] == "--test")
{
    Console.WriteLine("Running in automated test mode (--test)...");
    using Mat testFrame = new(720, 1280, MatType.CV_8UC3, new Scalar(30, 30, 30));
    List<TrackedHand> results = tracker.ProcessFrame(testFrame);
    renderer.Render(testFrame, results);
    virtualObject.Update(results.FirstOrDefault(), testFrame.Width, testFrame.Height);
    virtualObject.Render(testFrame);
    mouseController.Update(results.FirstOrDefault(), testFrame.Width, testFrame.Height);
    mouseController.RenderFeedback(testFrame, results.FirstOrDefault());
    scrollController.UpdateMomentum();
    scrollController.Update(results.FirstOrDefault());
    scrollController.RenderFeedback(testFrame);
    windowGrabController.Update(results, testFrame.Width, testFrame.Height);
    windowGrabController.RenderFeedback(testFrame);
    twoHandController.Update(results);
    twoHandController.RenderFeedback(testFrame, results);

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

    // Automated Unit Test 4: Snap-to-Side & Un-snap Simulation
    Console.WriteLine("Testing Snap-to-Side Edge Docking & Seamless Un-snap...");
    WindowGrabDetector testSnapDetector = new(1920, 1080);
    testSnapDetector.State.IsGrabbed = true;
    testSnapDetector.State.TargetHwnd = new IntPtr(999);
    testSnapDetector.State.InitialWindowBounds = new Rect(400, 300, 800, 600);
    testSnapDetector.State.PreSnapBounds = new Rect(400, 300, 800, 600);
    testSnapDetector.State.InitialHandScreenX = 500;
    testSnapDetector.State.InitialHandScreenY = 400;

    TrackedHand edgeHand = new() { Gesture = "Fist" };
    // Map to screen X = 0 (left edge <= 35)
    edgeHand.SmoothedLandmarks2D[9] = new Point2f(1280 * 0.15f, 720 * 0.5f);

    testSnapDetector.Update(edgeHand, 1280, 720, inputSink);
    if (testSnapDetector.State.ActiveSnap != WindowSnapType.LeftHalf) throw new Exception("Snap Left failed to trigger.");
    if (testSnapDetector.State.SnapBounds.Width != 1920 / 2) throw new Exception("Snap Left width incorrect.");

    // Drag away from edge to un-dock (screen X = ~384px > 65px)
    edgeHand.SmoothedLandmarks2D[9] = new Point2f(1280 * 0.35f, 720 * 0.5f);
    testSnapDetector.Update(edgeHand, 1280, 720, inputSink);
    if (testSnapDetector.State.ActiveSnap != WindowSnapType.None) throw new Exception("Un-docking failed when pulling hand away from edge.");

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
Console.WriteLine("  [T]         : Toggle 2-Hand Gestures (Maximize/Minimize)");
Console.WriteLine("  [S]         : Toggle OneEuroFilter Smoothing");
Console.WriteLine("  [J]         : Toggle Skeleton Joint Nodes");
Console.WriteLine("  [B]         : Toggle Bounding Box & HUD Tag");
Console.WriteLine("  [R]         : Reset Virtual Object (Pos & Zoom)");
Console.WriteLine("  [H]         : Toggle Telemetry HUD Overlay\n");

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

    // 2. Process Mouse Movement & Dwell Click
    mouseController.Update(primaryHand, frame.Width, frame.Height);

    // 3. Process Vertical Swipe Scrolling & Physics Momentum
    scrollController.UpdateMomentum();
    scrollController.LastPointerActiveTime = mouseController.LastPointerActiveTime;
    scrollController.Update(primaryHand);

    // 4. Process Real Windows OS Window Grabbing, Moving & Two-Hand Pinch Resizing
    windowGrabController.Update(trackedHands, frame.Width, frame.Height);

    // 5. Process Two-Hand Window Gestures (Maximize / Minimize in 3s Window)
    twoHandController.Update(trackedHands);

    // 6. Process Relative Grab & Zoom on Virtual Object (when real window grab is idle)
    virtualObject.Update(primaryHand, frame.Width, frame.Height);

    // 7. Render Hand Skeleton Bones Overlay
    renderer.Render(frame, trackedHands);

    // 8. Render Mouse Click Feedback & Dwell Ring
    mouseController.RenderFeedback(frame, primaryHand);

    // 9. Render Swipe Scroll Feedback Arrows
    scrollController.RenderFeedback(frame);

    // 10. Render Real Window Grab Feedback, Scaled Corner Brackets & Pinch Caliper
    windowGrabController.RenderFeedback(frame);

    // 11. Render Two-Hand Gesture Banner, Touch Link Line & Action Arrows
    twoHandController.RenderFeedback(frame, trackedHands);

    // 12. Render Virtual Test Target Object
    virtualObject.Render(frame);

    // 13. Real-time FPS Calculation
    frameCount++;
    if (fpsStopwatch.ElapsedMilliseconds >= 500)
    {
        currentFps = frameCount * 1000.0 / fpsStopwatch.ElapsedMilliseconds;
        frameCount = 0;
        fpsStopwatch.Restart();
    }

    // 14. Render Telemetry HUD
    if (showHud)
        DrawHud(frame, currentFps, trackedHands.Count, tracker.SmoothingEnabled, virtualObject, mouseController, scrollController, windowGrabController, twoHandController);
    
    // 15. Display Frame in OpenCV Window
    window.ShowImage(frame);

    // 16. Handle Keyboard Hotkeys
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
static void DrawHud(Mat frame, double fps, int handsCount, bool smoothed, VirtualObjectController objCtrl, MouseController mouseCtrl, ScrollController scrollCtrl, WindowGrabController grabCtrl, TwoHandGestureController twoHandCtrl)
{
    Rect hudRect = new(10, 10, 390, 148);
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
    else if (grabCtrl.ResizeState.IsActive)
    {
        winGrabStatus = $"Resize: {grabCtrl.ResizeState.CurrentWidth}x{grabCtrl.ResizeState.CurrentHeight} ({grabCtrl.ResizeState.CurrentScale:F2}x)";
        winGrabColor = new Scalar(0, 220, 255);
    }
    else if (grabCtrl.State.IsGrabbed)
    {
        winGrabStatus = $"Gegriffen [{grabCtrl.State.CachedWindowTitle}]";
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

    Cv2.PutText(frame, $"Fenster (G): {winGrabStatus}", new Point(20, 102),
        HersheyFonts.HersheySimplex, 0.36, winGrabColor, 1, LineTypes.AntiAlias);

    string twoHandStatus;
    Scalar twoHandColor;
    if (!twoHandCtrl.Enabled)
    {
        twoHandStatus = "AUS (Taste T)";
        twoHandColor = new Scalar(160, 160, 160);
    }
    else if (twoHandCtrl.State.InCooldown)
    {
        twoHandStatus = $"Cooldown ({twoHandCtrl.State.LastAction})";
        twoHandColor = new Scalar(0, 180, 255);
    }
    else if (twoHandCtrl.State.IsWindowActive)
    {
        twoHandStatus = $"Bereit ({twoHandCtrl.State.RemainingWindowSeconds:F1}s)";
        twoHandColor = new Scalar(0, 255, 120);
    }
    else
    {
        twoHandStatus = "Gesperrt (Erst Faust loslassen)";
        twoHandColor = new Scalar(120, 120, 120);
    }

    Cv2.PutText(frame, $"Zwei-Hand (T): {twoHandStatus}", new Point(20, 120),
        HersheyFonts.HersheySimplex, 0.36, twoHandColor, 1, LineTypes.AntiAlias);

    string grabLabel = objCtrl.GrabState.Active ? "GRABBED" : (objCtrl.GrabState.HoldDurationSeconds > 0 ? $"HOLD {objCtrl.GrabState.HoldDurationSeconds:F1}s" : "Ready");
    string objStatus = $"Testobjekt: {grabLabel} | Zoom: {objCtrl.ZoomState.CurrentZoom:F2}x (R)";
    Cv2.PutText(frame, objStatus, new Point(20, 138),
        HersheyFonts.HersheySimplex, 0.34, new Scalar(200, 200, 200), 1, LineTypes.AntiAlias);
}