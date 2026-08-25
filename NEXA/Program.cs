using System;
using System.Diagnostics;
using System.IO;
using NEXA;
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

if (args.Length > 0 && args[0] == "--test")
{
    Console.WriteLine("Running in automated test mode (--test)...");
    using var testFrame = new Mat(720, 1280, MatType.CV_8UC3, new Scalar(30, 30, 30));
    var results = tracker.ProcessFrame(testFrame);
    renderer.Render(testFrame, results);
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
Console.WriteLine("  [S]         : Glättung Umschalten (Direct Frame vs OneEuroFilter)");
Console.WriteLine("  [J]         : Gelenkpunkte (Joints) Umschalten");
Console.WriteLine("  [B]         : Bounding-Box & Gesten-Tag Umschalten");
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

    // 2. Render Hand Skeleton Bones
    renderer.Render(frame, trackedHands);

    // 3. FPS calculation
    frameCount++;
    if (fpsStopwatch.ElapsedMilliseconds >= 500)
    {
        currentFps = frameCount * 1000.0 / fpsStopwatch.ElapsedMilliseconds;
        frameCount = 0;
        fpsStopwatch.Restart();
    }

    // 4. Render HUD
    if (showHud)
    {
        DrawHud(frame, currentFps, trackedHands.Count, tracker.SmoothingEnabled, renderer.ShowJoints);
    }

    // 5. Display Frame
    window.ShowImage(frame);

    // 6. Handle User Input
    int key = Cv2.WaitKey(1);
    if (key == 27 || key == 'q' || key == 'Q') // ESC or Q
    {
        break;
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
    else if (key == 'h' || key == 'H')
    {
        showHud = !showHud;
    }
}

Console.WriteLine("Shutting down NEXA Hand Tracking...");

static void DrawHud(Mat frame, double fps, int handsCount, bool smoothed, bool showJoints)
{
    var hudRect = new Rect(10, 10, 310, 75);
    using var overlay = frame.Clone();
    Cv2.Rectangle(overlay, hudRect, new Scalar(10, 10, 15), -1);
    Cv2.AddWeighted(overlay, 0.7, frame, 0.3, 0, frame);
    Cv2.Rectangle(frame, hudRect, new Scalar(0, 220, 255), 1);

    Cv2.PutText(frame, "NEXA HAND SKELETON (ONNX)", new Point(20, 30),
        HersheyFonts.HersheySimplex, 0.55, new Scalar(0, 255, 255), 1, LineTypes.AntiAlias);

    Cv2.PutText(frame, $"FPS: {fps:F1} | Hands: {handsCount}", new Point(20, 50),
        HersheyFonts.HersheySimplex, 0.45, new Scalar(255, 255, 255), 1, LineTypes.AntiAlias);

    Cv2.PutText(frame, $"Mode: {(smoothed ? "Smooth" : "Frame-Direct (S)")} | Joints: (J)", new Point(20, 70),
        HersheyFonts.HersheySimplex, 0.42, new Scalar(200, 200, 200), 1, LineTypes.AntiAlias);
}