using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
using NEXA.UI;
using OpenCvSharp;

namespace NEXA.Application;

/// <summary>
/// Main application execution engine managing the real-time camera capture loop, multi-modal pipeline updates, and UI rendering passes.
/// <para>
/// <b>What it is:</b> The core application coordinator driving the real-time OpenCV AR loop.
/// </para>
/// </summary>
public class NexaEngine
{
    private readonly HandTracker _tracker;
    private readonly FaceTracker _faceTracker;
    private readonly IInputSink _inputSink;
    private readonly IAudioSink _audioSink;
    private readonly IScreenshotSink _screenshotSink;
    private readonly HandMeshRenderer _handRenderer;
    private readonly FaceMeshRenderer _faceRenderer;
    private readonly VirtualObjectController _virtualObject;
    private readonly MouseController _mouseController;
    private readonly ScrollController _scrollController;
    private readonly WindowGrabController _windowGrabController;
    private readonly TwoHandGestureController _twoHandController;
    private readonly MonitorThrowController _monitorThrowController;
    private readonly VolumeController _volumeController;
    private readonly LockSequenceController _lockController;
    private readonly CircleUndoController _circleUndoController;
    private readonly ShhhMuteController _shhhMuteController;
    private readonly HearNoEvilController _hearNoEvilController;
    private readonly HudRenderer _hudRenderer;
    private readonly KeyboardCommandHandler _commandHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="NexaEngine"/> class.
    /// </summary>
    public NexaEngine(
        HandTracker tracker,
        FaceTracker faceTracker,
        IInputSink inputSink,
        IAudioSink audioSink,
        IScreenshotSink screenshotSink,
        HandMeshRenderer handRenderer,
        FaceMeshRenderer faceRenderer,
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
        HearNoEvilController hearNoEvilController,
        HudRenderer hudRenderer,
        KeyboardCommandHandler commandHandler)
    {
        _tracker = tracker;
        _faceTracker = faceTracker;
        _inputSink = inputSink;
        _audioSink = audioSink;
        _screenshotSink = screenshotSink;
        _handRenderer = handRenderer;
        _faceRenderer = faceRenderer;
        _virtualObject = virtualObject;
        _mouseController = mouseController;
        _scrollController = scrollController;
        _windowGrabController = windowGrabController;
        _twoHandController = twoHandController;
        _monitorThrowController = monitorThrowController;
        _volumeController = volumeController;
        _lockController = lockController;
        _circleUndoController = circleUndoController;
        _shhhMuteController = shhhMuteController;
        _hearNoEvilController = hearNoEvilController;
        _hudRenderer = hudRenderer;
        _commandHandler = commandHandler;
    }

    /// <summary>
    /// Opens the camera stream and runs the interactive frame processing loop.
    /// </summary>
    /// <param name="webcamIndex">Index of the camera device.</param>
    public void Run(int webcamIndex = 0)
    {
        Console.WriteLine($"Opening Camera (Index {webcamIndex})...");
        using VideoCapture capture = new(webcamIndex, VideoCaptureAPIs.ANY);

        if (!capture.IsOpened())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] Could not open webcam (index {webcamIndex}). Please check your camera connection.");
            Console.ResetColor();
            return;
        }

        capture.Set(VideoCaptureProperties.FrameWidth, 1280);
        capture.Set(VideoCaptureProperties.FrameHeight, 720);
        capture.Set(VideoCaptureProperties.Fps, 30);

        const string windowName = "NEXA - MediaPipe Hand Tracking [ONNX]";
        using Window window = new(windowName, WindowFlags.AutoSize);
        using Mat frame = new();

        _commandHandler.PrintControls();

        int frameCount = 0;
        double currentFps = 0.0;
        Stopwatch fpsStopwatch = Stopwatch.StartNew();
        bool showHud = true;

        while (true)
        {
            if (!capture.Read(frame) || frame.Empty())
            {
                Cv2.WaitKey(10);
                continue;
            }

            // Mirror camera frame horizontally for natural selfie-view AR interaction
            Cv2.Flip(frame, frame, FlipMode.Y);

            // 1. Vision Inference (Hands & Face)
            List<TrackedHand> trackedHands = _tracker.ProcessFrame(frame);
            TrackedHand? primaryHand = trackedHands.FirstOrDefault();
            TrackedFace? primaryFace = _faceTracker.ProcessFrame(frame);

            // 2. Domain Controllers Update Pass
            _mouseController.Update(primaryHand, frame.Width, frame.Height);
            _scrollController.UpdateMomentum();
            _scrollController.Update(primaryHand);
            _windowGrabController.Update(trackedHands, frame.Width, frame.Height);
            _twoHandController.Update(trackedHands, frame.Width, frame.Height);
            _monitorThrowController.Update(primaryHand);
            _volumeController.Update(primaryHand);
            _lockController.Update(primaryHand);
            _circleUndoController.Update(primaryHand);
            _shhhMuteController.Update(primaryHand, primaryFace);
            _hearNoEvilController.Update(trackedHands, primaryFace);
            _virtualObject.Update(primaryHand, frame.Width, frame.Height);

            // 3. Visual Render Passes
            _faceRenderer.Render(frame, primaryFace);
            _handRenderer.Render(frame, trackedHands);
            _mouseController.RenderFeedback(frame, primaryHand);
            _scrollController.RenderFeedback(frame);
            _windowGrabController.RenderFeedback(frame);
            _twoHandController.RenderFeedback(frame, trackedHands);
            _monitorThrowController.RenderFeedback(frame, primaryHand);
            _volumeController.RenderFeedback(frame);
            _lockController.RenderFeedback(frame, primaryHand);
            _circleUndoController.RenderFeedback(frame, primaryHand);
            _shhhMuteController.RenderFeedback(frame, primaryFace, primaryHand);
            _hearNoEvilController.RenderFeedback(frame, primaryFace, trackedHands);
            _virtualObject.Render(frame);

            // 4. Performance & Telemetry HUD
            frameCount++;
            if (fpsStopwatch.ElapsedMilliseconds >= 500)
            {
                currentFps = frameCount * 1000.0 / fpsStopwatch.ElapsedMilliseconds;
                frameCount = 0;
                fpsStopwatch.Restart();
            }

            if (showHud)
            {
                _hudRenderer.Render(
                    frame,
                    currentFps,
                    trackedHands.Count,
                    _tracker.SmoothingEnabled,
                    _virtualObject,
                    _mouseController,
                    _scrollController,
                    _windowGrabController,
                    _twoHandController,
                    _monitorThrowController,
                    _volumeController,
                    _lockController,
                    _circleUndoController,
                    _shhhMuteController,
                    _hearNoEvilController);
            }

            window.ShowImage(frame);

            // 5. Keyboard Handling
            int key = Cv2.WaitKey(1);
            if (key != -1)
            {
                bool continueRunning = _commandHandler.ProcessKey(
                    key,
                    frame.Width,
                    frame.Height,
                    _tracker,
                    _handRenderer,
                    _faceRenderer,
                    _virtualObject,
                    _mouseController,
                    _scrollController,
                    _windowGrabController,
                    _twoHandController,
                    _monitorThrowController,
                    _volumeController,
                    _lockController,
                    _circleUndoController,
                    _shhhMuteController,
                    _hearNoEvilController,
                    ref showHud);

                if (!continueRunning)
                {
                    break;
                }
            }
        }

        Console.WriteLine("Shutting down NEXA Hand Tracking...");
    }
}
