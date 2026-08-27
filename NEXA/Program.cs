using System;
using System.IO;
using NEXA.Adapters.Output;
using NEXA.Application;
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
using NEXA.Testing;
using NEXA.UI;

// ====================================================================================================
// N.E.X.A. - Neural EXtended Augmented-Reality Gesture Controller (MediaPipe ONNX + OpenCV + Win32)
// Main Application Bootstrap & Entry Point
// ====================================================================================================

int webcamIndex = 0; // Index 0 = Webcam. Change if external camera is used.

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("==============================");
Console.WriteLine("  N.E.X.A. - Hand Tracking    ");
Console.WriteLine("==============================");
Console.ResetColor();

string palmModelPath = Path.Combine(AppContext.BaseDirectory, "models", "palm_detection.onnx");
string landmarkModelPath = Path.Combine(AppContext.BaseDirectory, "models", "handpose_estimation.onnx");

if (!File.Exists(palmModelPath) || !File.Exists(landmarkModelPath))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[ERROR] Models not found in: {Path.Combine(AppContext.BaseDirectory, "models")}");
    Console.ResetColor();
    return;
}

Console.WriteLine("Loading MediaPipe ONNX Models...");

// 1. Initialize Pipeline Adapters & Controllers
using HandTracker tracker = new(palmModelPath, landmarkModelPath);
using FaceTracker faceTracker = new();
Win32InputSink inputSink = new();
Win32AudioSink audioSink = new();
Win32ScreenshotSink screenshotSink = new();
HandMeshRenderer handRenderer = new();
FaceMeshRenderer faceRenderer = new();
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
HearNoEvilController hearNoEvilController = new(audioSink);
HudRenderer hudRenderer = new();
KeyboardCommandHandler commandHandler = new();

// Wire 3-second post-fist window trigger for two-hand maximize/minimize
windowGrabController.OnFistReleased += () => twoHandController.Detector.NotifyFistReleased();

// 2. Dispatch: Headless Automated Test Mode (--test) vs Interactive Engine
if (args.Length > 0 && args[0] == "--test")
{
    SelfTestRunner.Run(
        tracker,
        faceTracker,
        inputSink,
        audioSink,
        screenshotSink,
        handRenderer,
        faceRenderer,
        virtualObject,
        mouseController,
        scrollController,
        windowGrabController,
        twoHandController,
        monitorThrowController,
        volumeController,
        lockController,
        circleUndoController,
        shhhMuteController,
        hearNoEvilController);
    return;
}

NexaEngine engine = new(
    tracker,
    faceTracker,
    inputSink,
    audioSink,
    screenshotSink,
    handRenderer,
    faceRenderer,
    virtualObject,
    mouseController,
    scrollController,
    windowGrabController,
    twoHandController,
    monitorThrowController,
    volumeController,
    lockController,
    circleUndoController,
    shhhMuteController,
    hearNoEvilController,
    hudRenderer,
    commandHandler);

engine.Run(webcamIndex);