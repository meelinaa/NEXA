using NEXA.Adapters.Output;
using NEXA.Domain.Click;
using NEXA.Domain.Scroll;
using NEXA.Hand;
using NEXA.Object;
using OpenCvSharp;
using System.Diagnostics;

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

// 2. Automated Non-Interactive Test Mode (--test). Its for automatically running the application without any user interaction.
// This is used to test the application without any user interaction.
// It is also used to test the application on different cameras.
// It is also used to test the application on different models.
if (args.Length > 0 && args[0] == "--test")
{
    Console.WriteLine("Running in automated test mode (--test)...");
    using var testFrame = new Mat(720, 1280, MatType.CV_8UC3, new Scalar(30, 30, 30));
    List<TrackedHand> results = tracker.ProcessFrame(testFrame);
    renderer.Render(testFrame, results);
    virtualObject.Update(results.FirstOrDefault(), testFrame.Width, testFrame.Height);
    virtualObject.Render(testFrame);
    mouseController.Update(results.FirstOrDefault(), testFrame.Width, testFrame.Height);
    mouseController.RenderFeedback(testFrame, results.FirstOrDefault());
    scrollController.UpdateMomentum();
    scrollController.Update(results.FirstOrDefault());
    scrollController.RenderFeedback(testFrame);
    Console.WriteLine($"[PASS] Pipeline executed cleanly. Detected hands: {results.Count}");
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

    // 4. Process Relative Grab & Zoom on Virtual Object
    virtualObject.Update(primaryHand, frame.Width, frame.Height);

    // 5. Render Hand Skeleton Bones Overlay
    renderer.Render(frame, trackedHands);

    // 6. Render Mouse Click Feedback & Dwell Ring
    mouseController.RenderFeedback(frame, primaryHand);

    // 7. Render Swipe Scroll Feedback Arrows
    scrollController.RenderFeedback(frame);

    // 8. Render Virtual Test Target Object
    virtualObject.Render(frame);

    // 9. Real-time FPS Calculation
    frameCount++;
    if (fpsStopwatch.ElapsedMilliseconds >= 500)
    {
        currentFps = frameCount * 1000.0 / fpsStopwatch.ElapsedMilliseconds;
        frameCount = 0;
        fpsStopwatch.Restart();
    }

    // 10. Render Telemetry HUD
    if (showHud)
        DrawHud(frame, currentFps, trackedHands.Count, tracker.SmoothingEnabled, virtualObject, mouseController, scrollController);
    
    // 11. Display Frame in OpenCV Window
    window.ShowImage(frame);

    // 12. Handle Keyboard Hotkeys
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
static void DrawHud(Mat frame, double fps, int handsCount, bool smoothed, VirtualObjectController objCtrl, MouseController mouseCtrl, ScrollController scrollCtrl)
{
    Rect hudRect = new(10, 10, 370, 110);
    using Mat overlay = frame.Clone();
    Cv2.Rectangle(overlay, hudRect, new Scalar(10, 10, 15), -1);
    Cv2.AddWeighted(overlay, 0.7, frame, 0.3, 0, frame);
    Cv2.Rectangle(frame, hudRect, new Scalar(0, 220, 255), 1);

    Cv2.PutText(frame, "NEXA HAND MOUSE & SCROLL (ONNX)", new Point(20, 28),
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

    string grabLabel = objCtrl.GrabState.Active ? "GRABBED" : (objCtrl.GrabState.HoldDurationSeconds > 0 ? $"HOLD {objCtrl.GrabState.HoldDurationSeconds:F1}s" : "Ready");
    string objStatus = $"Faust: {grabLabel} | Zoom: {objCtrl.ZoomState.CurrentZoom:F2}x (R)";
    Cv2.PutText(frame, objStatus, new Point(20, 102),
        HersheyFonts.HersheySimplex, 0.35, new Scalar(200, 200, 200), 1, LineTypes.AntiAlias);
}