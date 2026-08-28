using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using NEXA.Abstractions;
using NEXA.Adapters.Capture;
using NEXA.Adapters.Output;
using NEXA.Common;
using NEXA.Face;
using NEXA.Hand;
using NEXA.UI;
using OpenCvSharp;

namespace NEXA.Application;

/// <summary>
/// Main application execution engine managing the real-time camera capture loop, multi-modal pipeline updates, and UI rendering passes.
/// <para>
/// <b>What it is:</b> The core application coordinator driving the real-time OpenCV AR loop via multi-threaded Producer-Consumer architecture.
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
    private readonly NexaControllerBundle _controllers;
    private readonly HudRenderer _hudRenderer;
    private readonly KeyboardCommandHandler _commandHandler;
    private readonly IFrameSource _frameSource;
    private readonly IDisplaySink _displaySink;
    private readonly IKeyboardEventSource _keyboardSource;
    private readonly IVisionPipeline _visionPipeline;
    private readonly IGestureArbitrator _gestureArbitrator;

    /// <summary>
    /// Initializes a new instance of the <see cref="NexaEngine"/> class.
    /// </summary>
    /// <param name="tracker">Hand tracking pipeline (palm detection + landmark estimation).</param>
    /// <param name="faceTracker">Face detection and landmark pipeline.</param>
    /// <param name="inputSink">Win32 mouse/keyboard/window interop adapter.</param>
    /// <param name="audioSink">CoreAudio volume and mute adapter.</param>
    /// <param name="screenshotSink">GDI screen capture adapter.</param>
    /// <param name="handRenderer">Hand skeleton AR overlay renderer.</param>
    /// <param name="faceRenderer">Face mesh AR overlay renderer.</param>
    /// <param name="controllers">Aggregate bundle of all gesture-domain controllers.</param>
    /// <param name="hudRenderer">Telemetry HUD overlay renderer.</param>
    /// <param name="commandHandler">Keyboard hotkey dispatcher.</param>
    /// <param name="frameSource">Optional camera frame source (defaults to OpenCV webcam).</param>
    /// <param name="displaySink">Optional display output (defaults to OpenCV window).</param>
    /// <param name="keyboardSource">Optional keyboard event source (defaults to OpenCV WaitKey).</param>
    /// <param name="visionPipeline">Optional asynchronous multi-model vision pipeline.</param>
    /// <param name="gestureArbitrator">Optional gesture arbitrator enforcing mutual exclusion.</param>
    public NexaEngine(
        HandTracker tracker,
        FaceTracker faceTracker,
        IInputSink inputSink,
        IAudioSink audioSink,
        IScreenshotSink screenshotSink,
        HandMeshRenderer handRenderer,
        FaceMeshRenderer faceRenderer,
        NexaControllerBundle controllers,
        HudRenderer hudRenderer,
        KeyboardCommandHandler commandHandler,
        IFrameSource? frameSource = null,
        IDisplaySink? displaySink = null,
        IKeyboardEventSource? keyboardSource = null,
        IVisionPipeline? visionPipeline = null,
        IGestureArbitrator? gestureArbitrator = null)
    {
        _tracker = tracker;
        _faceTracker = faceTracker;
        _inputSink = inputSink;
        _audioSink = audioSink;
        _screenshotSink = screenshotSink;
        _handRenderer = handRenderer;
        _faceRenderer = faceRenderer;
        _controllers = controllers;
        _hudRenderer = hudRenderer;
        _commandHandler = commandHandler;
        _frameSource = frameSource ?? new OpenCvFrameSource();
        _displaySink = displaySink ?? new OpenCvDisplaySink();
        _keyboardSource = keyboardSource ?? new OpenCvKeyboardEventSource();
        _visionPipeline = visionPipeline ?? new AsyncVisionPipeline(tracker, faceTracker);
        _gestureArbitrator = gestureArbitrator ?? new NEXA.Domain.Common.GestureArbitrator();
    }

    /// <summary>
    /// Opens the camera stream and runs the interactive frame processing loop synchronously.
    /// </summary>
    /// <param name="webcamIndex">Index of the camera device.</param>
    public void Run(int webcamIndex = 0)
    {
        Console.WriteLine($"Opening Camera (Index {webcamIndex})...");

        if (!_frameSource.Open(webcamIndex))
        {
            if (_frameSource is SwitchableFrameSource switchable)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[INFO] Local USB webcam (index {webcamIndex}) not detected or unavailable.");
                Console.WriteLine("[INFO] Falling back to Wireless Smartphone Camera Receiver (HTTP/WebRTC)...");
                Console.ResetColor();

                if (!switchable.SwitchMode(CameraSourceMode.SmartphoneStream))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[ERROR] Could not start smartphone camera receiver.");
                    Console.ResetColor();
                    return;
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Could not open webcam (index {webcamIndex}). Please check your camera connection.");
                Console.ResetColor();
                return;
            }
        }

        _frameSource.Set(VideoCaptureProperties.FrameWidth, 1280);
        _frameSource.Set(VideoCaptureProperties.FrameHeight, 720);
        _frameSource.Set(VideoCaptureProperties.Fps, 30);

        // Pre-warm ONNX inference sessions & DirectML GPU shaders to eliminate first-frame hitch
        _visionPipeline.WarmUpAsync().GetAwaiter().GetResult();

        using Mat frame = new();

        _commandHandler.PrintControls();

        int frameCount = 0;
        double currentFps = 0.0;
        Stopwatch fpsStopwatch = Stopwatch.StartNew();
        bool showHud = true;

        while (true)
        {
            if (!_frameSource.Read(frame) || frame.Empty())
            {
                int idleKey = _keyboardSource.WaitKey(10);
                if (idleKey != -1)
                {
                    bool continueRunning = _commandHandler.ProcessKey(
                        idleKey,
                        1280,
                        720,
                        _tracker,
                        _handRenderer,
                        _faceRenderer,
                        _controllers,
                        ref showHud);

                    if (!continueRunning)
                    {
                        break;
                    }
                }
                continue;
            }

            // Mirror camera frame horizontally for natural selfie-view AR interaction
            Cv2.Flip(frame, frame, FlipMode.Y);

            // 1. Vision Inference (Hands & Face)
            List<TrackedHand> trackedHands = _tracker.ProcessFrame(frame);
            TrackedFace? primaryFace = _faceTracker.ProcessFrame(frame);
            FrameContext context = new(frame, trackedHands, primaryFace, _gestureArbitrator);

            // 2. Domain Processors Update Pass (Decoupled via IFrameProcessor)
            foreach (IFrameProcessor processor in _controllers.Processors)
            {
                processor.Process(context);
            }

            // 3. Visual Render Passes
            _faceRenderer.Render(frame, primaryFace);
            _handRenderer.Render(frame, trackedHands);

            foreach (IFrameProcessor processor in _controllers.Processors)
            {
                processor.Render(context);
            }

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
                _hudRenderer.Render(frame, currentFps, trackedHands.Count, _tracker.SmoothingEnabled, _controllers);
            }

            _displaySink.ShowImage(frame);

            // 5. Keyboard Handling
            int key = _keyboardSource.WaitKey(1);
            if (key != -1)
            {
                bool continueRunning = _commandHandler.ProcessKey(
                    key,
                    frame.Width,
                    frame.Height,
                    _tracker,
                    _handRenderer,
                    _faceRenderer,
                    _controllers,
                    ref showHud);

                if (!continueRunning)
                {
                    break;
                }
            }
        }

        Console.WriteLine("Shutting down NEXA Hand Tracking...");
    }

    /// <summary>
    /// Executes the multi-threaded asynchronous Producer-Consumer engine loop decoupling camera ingestion from ONNX vision inference via <see cref="Channel{T}"/> and zero-allocation <see cref="MatRingBuffer"/>.
    /// </summary>
    /// <param name="webcamIndex">Index of the camera device.</param>
    /// <param name="cancellationToken">Cancellation token for graceful termination.</param>
    public async Task RunAsync(int webcamIndex = 0, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Opening Camera Async (Index {webcamIndex})...");

        if (!_frameSource.Open(webcamIndex))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] Could not open webcam (index {webcamIndex}). Please check your camera connection.");
            Console.ResetColor();
            return;
        }

        _frameSource.Set(VideoCaptureProperties.FrameWidth, 1280);
        _frameSource.Set(VideoCaptureProperties.FrameHeight, 720);
        _frameSource.Set(VideoCaptureProperties.Fps, 60);

        _commandHandler.PrintControls();

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Pre-warm ONNX inference sessions & DirectML GPU shaders in parallel to eliminate first-frame hitch
        await _visionPipeline.WarmUpAsync(cts.Token).ConfigureAwait(false);

        // Zero-allocation reusable native Mat ring buffer
        using MatRingBuffer ringBuffer = new(capacity: 4);

        // Bounded Channel with Capacity 1 and DropOldest guarantees Zero-Latency:
        // The capture thread constantly pushes fresh frames, discarding stale unconsumed ones.
        Channel<Mat> frameChannel = Channel.CreateBounded<Mat>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        });

        // 1. Producer Task: Camera Capture Thread running at max frame rate (e.g. 60 fps)
        Task producerTask = Task.Run(() =>
        {
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    Mat rawFrame = ringBuffer.Rent();
                    if (!_frameSource.Read(rawFrame) || rawFrame.Empty())
                    {
                        ringBuffer.Return(rawFrame);
                        Thread.Sleep(5);
                        continue;
                    }

                    // Horizontal flip for mirror view in-place
                    Cv2.Flip(rawFrame, rawFrame, FlipMode.Y);

                    if (!frameChannel.Writer.TryWrite(rawFrame))
                    {
                        // Channel dropped unconsumed/full item; recycle frame back to pool
                        ringBuffer.Return(rawFrame);
                    }
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                frameChannel.Writer.Complete();
            }
        }, cts.Token);

        // 2. Consumer Task: AI Inferenz & UI Rendering Pipeline
        int frameCount = 0;
        double currentFps = 0.0;
        Stopwatch fpsStopwatch = Stopwatch.StartNew();
        bool showHud = true;

        try
        {
            ChannelReader<Mat> reader = frameChannel.Reader;

            while (await reader.WaitToReadAsync(cts.Token).ConfigureAwait(false))
            {
                while (reader.TryRead(out Mat? frame))
                {
                    if (frame == null || frame.Empty())
                    {
                        ringBuffer.Return(frame);
                        continue;
                    }

                    try
                    {
                        // 1. Parallel Asynchronous Vision Inference (Hands + Face)
                        (List<TrackedHand> trackedHands, TrackedFace? primaryFace) =
                            await _visionPipeline.ProcessAsync(frame, cts.Token).ConfigureAwait(false);

                        FrameContext context = new(frame, trackedHands, primaryFace, _gestureArbitrator);

                        // 2. Domain Processors Update Pass
                        foreach (IFrameProcessor processor in _controllers.Processors)
                        {
                            processor.Process(context);
                        }

                        // 3. Visual Render Passes
                        _faceRenderer.Render(frame, primaryFace);
                        _handRenderer.Render(frame, trackedHands);

                        foreach (IFrameProcessor processor in _controllers.Processors)
                        {
                            processor.Render(context);
                        }

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
                            _hudRenderer.Render(frame, currentFps, trackedHands.Count, _tracker.SmoothingEnabled, _controllers);
                        }

                        _displaySink.ShowImage(frame);

                        // 5. Keyboard Handling
                        int key = _keyboardSource.WaitKey(1);
                        if (key != -1)
                        {
                            bool continueRunning = _commandHandler.ProcessKey(
                                key,
                                frame.Width,
                                frame.Height,
                                _tracker,
                                _handRenderer,
                                _faceRenderer,
                                _controllers,
                                ref showHud);

                            if (!continueRunning)
                            {
                                cts.Cancel();
                                break;
                            }
                        }
                    }
                    finally
                    {
                        // Return processed frame back to ring buffer for zero-alloc recycling
                        ringBuffer.Return(frame);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            cts.Cancel();

            // Recycle any remaining frames in channel
            while (frameChannel.Reader.TryRead(out Mat? leftover))
            {
                ringBuffer.Return(leftover);
            }

            await producerTask.ConfigureAwait(false);
        }

        Console.WriteLine("Shutting down NEXA Hand Tracking (Async)...");
    }

    /// <summary>
    /// Explicitly pre-warms the vision pipeline and compiles DirectML shaders in advance.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public Task WarmUpAsync(CancellationToken cancellationToken = default)
    {
        return _visionPipeline.WarmUpAsync(cancellationToken);
    }
}
