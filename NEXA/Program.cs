using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NEXA;
using NEXA.Hand;
using NEXA.Object;
using OpenCvSharp;

Console.WriteLine("==================================================");
Console.WriteLine("  N.E.X.A. - MediaPipe Hand Tracking (ONNX + OpenCV)");
Console.WriteLine("==================================================");

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
using var tracker = new HandTracker(palmModelPath, landmarkModelPath);
var renderer = new HandMeshRenderer();
var virtualObject = new VirtualObjectController();
var mouseController = new MouseController();
var scrollController = new ScrollController();

if (args.Length > 0 && args[0] == "--test")
{
    Console.WriteLine("Running in automated test mode (--test)...");
    using var testFrame = new Mat(720, 1280, MatType.CV_8UC3, new Scalar(30, 30, 30));
    var results = tracker.ProcessFrame(testFrame);
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

Console.WriteLine("Opening Camera (Index 0)...");
using var capture = new VideoCapture(0, VideoCaptureAPIs.ANY);

if (!capture.IsOpened())
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("[ERROR] Could not open webcam (index 0). Please check your camera connection.");
    Console.ResetColor();
    return;
}

// Request optimal camera settings
capture.Set(VideoCaptureProperties.FrameWidth, 1280);
capture.Set(VideoCaptureProperties.FrameHeight, 720);
capture.Set(VideoCaptureProperties.Fps, 30);

const string windowName = "NEXA - MediaPipe Hand Tracking [ONNX]";
using var window = new Window(windowName, WindowFlags.AutoSize);
using var frame = new Mat();

Console.WriteLine("\nControls:");
Console.WriteLine("  [ESC] / [Q] : Beenden");
Console.WriteLine("  [C]         : Maus-Steuerung Ein-/Ausschalten");
Console.WriteLine("  [W]         : Wisch-Scrollen Ein-/Ausschalten");
Console.WriteLine("  [S]         : Glättung Umschalten (Smooth vs Frame-Direct)");
Console.WriteLine("  [J]         : Gelenkpunkte (Joints) Umschalten");
Console.WriteLine("  [B]         : Bounding-Box & Gesten-Tag Umschalten");
Console.WriteLine("  [R]         : Virtuelles Objekt zurücksetzen (Pos & Zoom)");
Console.WriteLine("  [H]         : HUD-Overlay Umschalten\n");

int frameCount = 0;
double currentFps = 0.0;
var fpsStopwatch = Stopwatch.StartNew();
var frameStopwatch = new Stopwatch();
bool showHud = true;

while (true)
{
    frameStopwatch.Restart();

    if (!capture.Read(frame) || frame.Empty())
    {
        Cv2.WaitKey(10);
        continue;
    }

    // Mirror image for intuitive selfie view
    Cv2.Flip(frame, frame, FlipMode.Y);

    // 1. Process Hand Tracking
    var trackedHands = tracker.ProcessFrame(frame);
    var primaryHand = trackedHands.FirstOrDefault();

    // 2. Process Mouse Movement & Dwell Click
    mouseController.Update(primaryHand, frame.Width, frame.Height);

    // 3. Process Vertical Swipe Scrolling & Physics Momentum
    scrollController.UpdateMomentum();
    scrollController.LastPointerActiveTime = mouseController.LastPointerActiveTime;
    scrollController.Update(primaryHand);

    // 4. Process Relative Grab & Zoom on Virtual Object
    virtualObject.Update(primaryHand, frame.Width, frame.Height);

    // 5. Render Hand Skeleton Bones
    renderer.Render(frame, trackedHands);

    // 6. Render Mouse Click Feedback & Dwell Ring
    mouseController.RenderFeedback(frame, primaryHand);

    // 7. Render Swipe Scroll Feedback Arrows
    scrollController.RenderFeedback(frame);

    // 8. Render Virtual Test Target (Grab & Zoom Object)
    virtualObject.Render(frame);

    // 9. FPS calculation
    frameCount++;
    if (fpsStopwatch.ElapsedMilliseconds >= 500)
    {
        currentFps = frameCount * 1000.0 / fpsStopwatch.ElapsedMilliseconds;
        frameCount = 0;
        fpsStopwatch.Restart();
    }

    // 10. Render HUD
    if (showHud)
    {
        DrawHud(frame, currentFps, trackedHands.Count, tracker.SmoothingEnabled, virtualObject, mouseController, scrollController);
    }

    // 11. Display Frame
    window.ShowImage(frame);

    // 12. Handle User Input
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

static void DrawHud(Mat frame, double fps, int handsCount, bool smoothed, VirtualObjectController objCtrl, MouseController mouseCtrl, ScrollController scrollCtrl)
{
    var hudRect = new Rect(10, 10, 370, 110);
    using var overlay = frame.Clone();
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
    var mouseColor = mouseCtrl.Enabled ? new Scalar(0, 255, 120) : new Scalar(160, 160, 160);

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