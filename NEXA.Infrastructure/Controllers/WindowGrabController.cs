using System;
using System.Collections.Generic;
using System.Linq;
using NEXA.Abstractions;
using NEXA.Adapters.Output;
using NEXA.Common;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Grab;

/// <summary>
/// Application controller orchestrating real OS window grabbing, delta relocation, Snap-to-Side docking, and camera viewport feedback rendering.
/// <para>
/// <b>What it is:</b> The controller responsible for moving, resizing, and docking physical Windows desktop applications via hand gestures.
/// </para>
/// </summary>
public class WindowGrabController : IHudStatusProvider, IFrameProcessor
{
    private readonly IInputSink _inputSink;
    private readonly int _screenWidth;
    private readonly int _screenHeight;
    private readonly WindowResizeCoordinator _resizeCoordinator;
    private readonly WindowGrabRenderer _renderer;

    private IntPtr _lastForegroundHwnd = IntPtr.Zero;
    private bool _wasGrabbedLastFrame = false;

    /// <summary>
    /// Event fired immediately when a fist-grab gesture is released.
    /// </summary>
    public event Action? OnFistReleased;

    /// <summary>
    /// The core domain detector handling hold timing, coordinate mapping, delta dragging, and edge snapping.
    /// </summary>
    public WindowGrabDetector Detector { get; }

    /// <summary>
    /// The secondary domain detector handling continuous two-hand pinch aperture scaling.
    /// </summary>
    public WindowResizeDetector ResizeDetector => _resizeCoordinator.Detector;

    /// <summary>
    /// Gets or sets a value indicating whether window grabbing and resizing are enabled.
    /// </summary>
    public bool Enabled
    {
        get => Detector.Enabled;
        set => Detector.Enabled = value;
    }

    /// <summary>
    /// Gets the internal window grab state machine.
    /// </summary>
    public WindowGrabState State => Detector.State;

    /// <summary>
    /// Gets the internal window resize state machine.
    /// </summary>
    public WindowResizeState ResizeState => _resizeCoordinator.State;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowGrabController"/> class.
    /// </summary>
    /// <param name="inputSink">The output adapter for OS inputs (defaults to <see cref="Win32InputSink"/> if null).</param>
    /// <param name="detector">Optional custom detector instance.</param>
    /// <param name="resizeCoordinator">Optional custom resize coordinator.</param>
    /// <param name="renderer">Optional custom renderer instance.</param>
    public WindowGrabController(
        IInputSink? inputSink = null,
        WindowGrabDetector? detector = null,
        WindowResizeCoordinator? resizeCoordinator = null,
        WindowGrabRenderer? renderer = null)
    {
        _inputSink = inputSink ?? new Win32InputSink();
        (_screenWidth, _screenHeight) = _inputSink.GetScreenResolution();
        Detector = detector ?? new WindowGrabDetector(_screenWidth, _screenHeight);
        _resizeCoordinator = resizeCoordinator ?? new WindowResizeCoordinator(_screenWidth, _screenHeight);
        _renderer = renderer ?? new WindowGrabRenderer();
    }

    /// <summary>
    /// Single-hand overload: Evaluates hand tracking data for moving the grabbed desktop window.
    /// </summary>
    public void Update(TrackedHand? hand, int frameWidth, int frameHeight)
    {
        List<TrackedHand> list = hand != null ? [hand] : [];
        Update(list, frameWidth, frameHeight);
    }

    /// <summary>
    /// Multi-hand evaluation: Moves grabbed window with primary fist hand, executes edge snapping, and resizes window with secondary pinch hand.
    /// </summary>
    /// <param name="hands">The list of active tracked hands.</param>
    /// <param name="frameWidth">Width of the camera frame in pixels.</param>
    /// <param name="frameHeight">Height of the camera frame in pixels.</param>
    public void Update(List<TrackedHand>? hands, int frameWidth, int frameHeight)
    {
        TrackedHand? primaryHand = null;
        TrackedHand? secondaryHand = null;

        if (hands != null && hands.Count > 0)
        {
            if (State.IsGrabbed)
            {
                primaryHand = hands.OrderBy(h => Math.Pow(h.SmoothedLandmarks2D[9].X - State.LastPalmCenter.X, 2) +
                                                 Math.Pow(h.SmoothedLandmarks2D[9].Y - State.LastPalmCenter.Y, 2)).FirstOrDefault();
                secondaryHand = hands.FirstOrDefault(h => h != primaryHand);
            }
            else
            {
                primaryHand = hands.FirstOrDefault(h => h.Gesture == "Fist") ?? hands[0];
                secondaryHand = hands.FirstOrDefault(h => h != primaryHand);
            }
        }

        (bool isGrabbed, IntPtr hwnd, int targetX, int targetY) = Detector.Update(primaryHand, frameWidth, frameHeight, _inputSink);

        if (isGrabbed && hwnd != IntPtr.Zero)
        {
            if (_lastForegroundHwnd != hwnd)
            {
                _inputSink.BringWindowToForeground(hwnd);
                _lastForegroundHwnd = hwnd;
                _inputSink.LastFocusedHwnd = hwnd;
                _inputSink.LastFocusedTitle = State.CachedWindowTitle;
            }

            // 1. Process secondary hand pinch-zoom resizing
            _resizeCoordinator.ProcessResize(State, secondaryHand, ref targetX, ref targetY);

            // 2. Dispatch window position and dimensions to the operating system
            if (State.IsSnapped)
            {
                _inputSink.SetWindowRect(hwnd, State.SnapBounds.X, State.SnapBounds.Y, State.SnapBounds.Width, State.SnapBounds.Height);
            }
            else
            {
                int currentW = State.InitialWindowBounds.Width > 0 ? State.InitialWindowBounds.Width : _screenWidth / 2;
                int currentH = State.InitialWindowBounds.Height > 0 ? State.InitialWindowBounds.Height : _screenHeight / 2;

                _inputSink.SetWindowRect(hwnd, targetX, targetY, currentW, currentH);
            }

            _wasGrabbedLastFrame = true;
        }
        else
        {
            _resizeCoordinator.Reset();

            if (_wasGrabbedLastFrame)
            {
                OnFistReleased?.Invoke();
                _wasGrabbedLastFrame = false;
            }
            _lastForegroundHwnd = IntPtr.Zero;
        }
    }

    public void Process(FrameContext context)
    {
        if (context.Arbitrator != null && !context.Arbitrator.CanExecute("WindowGrab"))
        {
            if (_wasGrabbedLastFrame)
            {
                _resizeCoordinator.Reset();
                OnFistReleased?.Invoke();
                _wasGrabbedLastFrame = false;
                _lastForegroundHwnd = IntPtr.Zero;
            }
            return;
        }

        Update(context.TrackedHands, context.FrameWidth, context.FrameHeight);

        if (State.IsGrabbed || ResizeState.IsActive)
        {
            context.Arbitrator?.TryAcquire("WindowGrab");
        }
        else
        {
            context.Arbitrator?.Release("WindowGrab");
        }
    }

    /// <summary>
    /// Renders augmented-reality visual feedback (hold countdown ring, scaled corner brackets, snap preview zones, and pinch caliper) onto the camera frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    public void RenderFeedback(Mat frame)
    {
        _renderer.Render(frame, State, ResizeState, Detector);
    }

    /// <inheritdoc/>
    public void Render(FrameContext context)
    {
        RenderFeedback(context.Frame);
    }

    /// <inheritdoc/>
    public string GetStatusText()
    {
        string winGrabStatus;
        if (!Enabled)
            winGrabStatus = "AUS (Taste G)";
        else if (State.IsSnapped)
            winGrabStatus = $"Docked ({State.ActiveSnap})";
        else if (ResizeState.IsActive)
            winGrabStatus = $"Resize: {ResizeState.CurrentWidth}x{ResizeState.CurrentHeight} ({ResizeState.CurrentScale:F2}x)";
        else if (State.IsGrabbed)
            winGrabStatus = $"Gegriffen [{TextSanitizer.ToSafeAscii(State.CachedWindowTitle)}]";
        else if (State.HoldDurationSeconds > 0)
            winGrabStatus = $"Halte Faust ({State.HoldDurationSeconds:F1}s / {State.RequiredHoldSeconds:F1}s)";
        else
            winGrabStatus = "Bereit (Faust 2s)";

        return TextSanitizer.ToSafeAscii($"Fenster (G): {winGrabStatus}");
    }

    /// <inheritdoc/>
    public Scalar GetStatusColor()
    {
        if (!Enabled) return new Scalar(160, 160, 160);
        if (State.IsSnapped) return new Scalar(255, 160, 0);
        if (ResizeState.IsActive) return new Scalar(0, 220, 255);
        if (State.IsGrabbed) return new Scalar(0, 100, 255);
        if (State.HoldDurationSeconds > 0) return new Scalar(0, 165, 255);
        return new Scalar(0, 255, 120);
    }
}

