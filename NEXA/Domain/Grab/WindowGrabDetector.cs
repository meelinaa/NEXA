using System;
using NEXA.Adapters.Output;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Grab;

/// <summary>
/// Domain-level detector for window grab initiation, delta dragging, and release management.
/// <para>
/// <b>What it is:</b> A state machine engine managing real-time window manipulation gestures.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="number">
/// <item><description><b>Coordinate Mapping:</b> Maps normalized camera space to monitor pixels (<see cref="MapToScreen"/>) and inverse-projects desktop bounds back into camera viewport space (<see cref="MapFromScreen"/>).</description></item>
/// <item><description><b>Hold Engagement (2.0s):</b> Requires holding a steady fist gesture for 2.0s over a target window to latch onto it.</description></item>
/// <item><description><b>Single-Call Caching:</b> Queries window title, bounds, and initial hand coordinates strictly once upon grab engagement.</description></item>
/// <item><description><b>Deadzone & Adaptive Smoothing:</b> Filters out camera jitter with a 3.0px stillness deadzone and dynamic exponential smoothing.</description></item>
/// <item><description><b>Time-Based Release Debounce (120ms):</b> Prevents accidental window dropouts during momentary optical tracking noise.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Why it is used:</b> Encapsulates window manipulation logic cleanly away from OpenCV video capture and Win32 interop.
/// </para>
/// <para>
/// <b>Consequence:</b> Generates rock-steady, smooth, jitter-free window relocation commands.
/// </para>
/// </summary>
public class WindowGrabDetector
{
    /// <summary>
    /// Gets the internal state machine tracking hold times, bounds, and debounce timers.
    /// </summary>
    public WindowGrabState State { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether window grabbing is active.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The physical pixel width of the primary monitor.
    /// </summary>
    public int ScreenWidth { get; }

    /// <summary>
    /// The physical pixel height of the primary monitor.
    /// </summary>
    public int ScreenHeight { get; }

    /// <summary>
    /// Continuous smoothed horizontal window target coordinate accumulator.
    /// </summary>
    private double _smoothedTargetX = 0;

    /// <summary>
    /// Continuous smoothed vertical window target coordinate accumulator.
    /// </summary>
    private double _smoothedTargetY = 0;

    /// <summary>
    /// Flag indicating whether the smoothing accumulator has been initialized with the starting window position.
    /// </summary>
    private bool _hasInitializedSmoothing = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowGrabDetector"/> class with specified desktop display dimensions.
    /// </summary>
    /// <param name="screenWidth">Monitor width in pixels.</param>
    /// <param name="screenHeight">Monitor height in pixels.</param>
    public WindowGrabDetector(int screenWidth, int screenHeight)
    {
        ScreenWidth = screenWidth;
        ScreenHeight = screenHeight;
    }

    /// <summary>
    /// Evaluates hand tracking data for the current frame to update window grab engagement or translation deltas.
    /// </summary>
    /// <param name="hand">The tracked hand instance.</param>
    /// <param name="frameWidth">Width of the camera frame in pixels.</param>
    /// <param name="frameHeight">Height of the camera frame in pixels.</param>
    /// <param name="inputSink">The output adapter to query window handles and geometry.</param>
    /// <returns>A tuple containing (isGrabbed, targetHwnd, targetX, targetY) for window movement.</returns>
    public (bool isGrabbed, IntPtr hwnd, int targetX, int targetY) Update(TrackedHand? hand, int frameWidth, int frameHeight, IInputSink inputSink)
    {
        if (!Enabled)
        {
            Reset();
            return (false, IntPtr.Zero, 0, 0);
        }

        bool isFist = hand != null && hand.Gesture == "Fist";

        // Handle case where hand is lost or gesture changes
        if (!isFist)
        {
            if (State.IsGrabbed)
            {
                // Start release debounce timer if not already running
                if (!State.ReleaseTimer.IsRunning)
                {
                    State.ReleaseTimer.Restart();
                }

                // If debounce period exceeded, release the window
                if (State.ReleaseTimer.Elapsed >= WindowGrabState.ReleaseTolerance)
                {
                    Reset();
                    return (false, IntPtr.Zero, 0, 0);
                }

                // Maintain dragging during temporary debounce interval
                return (true, State.TargetHwnd, State.CurrentTargetX, State.CurrentTargetY);
            }

            // Not grabbed and not making a fist -> reset hold timer
            ResetHold();
            return (false, IntPtr.Zero, 0, 0);
        }

        // Hand is actively making a fist
        State.ReleaseTimer.Reset();
        Point2f palmCenter = hand!.SmoothedLandmarks2D[9]; // Palm center (Middle finger MCP)
        State.LastPalmCenter = palmCenter;

        (double screenX, double screenY) = MapToScreen(palmCenter.X, palmCenter.Y, frameWidth, frameHeight);
        int currentHandX = (int)Math.Round(screenX);
        int currentHandY = (int)Math.Round(screenY);

        if (!State.IsGrabbed)
        {
            if (!State.HoldTimer.IsRunning)
            {
                State.HoldTimer.Restart();
            }

            State.HoldDurationSeconds = State.HoldTimer.Elapsed.TotalSeconds;

            // Check if 2.0s hold threshold is reached to initiate grab
            if (State.HoldDurationSeconds >= State.RequiredHoldSeconds)
            {
                IntPtr hwnd = inputSink.GetWindowAt(currentHandX, currentHandY);
                if (hwnd != IntPtr.Zero && inputSink.GetWindowBounds(hwnd, out int winX, out int winY, out int winW, out int winH, out string title))
                {
                    State.IsGrabbed = true;
                    State.TargetHwnd = hwnd;
                    State.CachedWindowTitle = title;
                    State.InitialWindowBounds = new Rect(winX, winY, winW, winH);
                    State.InitialHandScreenX = currentHandX;
                    State.InitialHandScreenY = currentHandY;
                    State.CurrentTargetX = winX;
                    State.CurrentTargetY = winY;

                    _smoothedTargetX = winX;
                    _smoothedTargetY = winY;
                    _hasInitializedSmoothing = true;
                }
                else
                {
                    // No valid window under hand -> reset hold timer
                    ResetHold();
                }
            }
        }
        else
        {
            // Actively dragging: compute delta relative to initial hand screen position
            int deltaX = currentHandX - State.InitialHandScreenX;
            int deltaY = currentHandY - State.InitialHandScreenY;

            double rawTargetX = State.InitialWindowBounds.X + deltaX;
            double rawTargetY = State.InitialWindowBounds.Y + deltaY;

            // Dynamic exponential smoothing and deadzone filtering for buttery-smooth movement
            if (!_hasInitializedSmoothing)
            {
                _smoothedTargetX = rawTargetX;
                _smoothedTargetY = rawTargetY;
                _hasInitializedSmoothing = true;
            }
            else
            {
                double diffX = rawTargetX - _smoothedTargetX;
                double diffY = rawTargetY - _smoothedTargetY;
                double dist = Math.Sqrt(diffX * diffX + diffY * diffY);

                // Deadzone: ignore micro-tremors smaller than 3.0 pixels
                if (dist > 3.0)
                {
                    // Dynamic alpha: 0.20 (heavy smoothing during slow positioning) up to 0.80 (rapid sweeping)
                    double alpha = Math.Clamp(0.20 + (dist / 180.0) * 0.55, 0.20, 0.80);
                    _smoothedTargetX += diffX * alpha;
                    _smoothedTargetY += diffY * alpha;
                }
            }

            State.CurrentTargetX = (int)Math.Round(_smoothedTargetX);
            State.CurrentTargetY = (int)Math.Round(_smoothedTargetY);

            return (true, State.TargetHwnd, State.CurrentTargetX, State.CurrentTargetY);
        }

        return (false, IntPtr.Zero, 0, 0);
    }

    /// <summary>
    /// Resets the hold timer when the fist gesture is interrupted before grab threshold is reached.
    /// </summary>
    private void ResetHold()
    {
        State.HoldTimer.Reset();
        State.HoldDurationSeconds = 0.0;
    }

    /// <summary>
    /// Fully resets all grab, drag, and smoothing states.
    /// </summary>
    public void Reset()
    {
        State.IsGrabbed = false;
        State.TargetHwnd = IntPtr.Zero;
        State.CachedWindowTitle = string.Empty;
        State.InitialWindowBounds = new Rect();
        State.InitialHandScreenX = 0;
        State.InitialHandScreenY = 0;
        State.CurrentTargetX = 0;
        State.CurrentTargetY = 0;
        State.HoldTimer.Reset();
        State.HoldDurationSeconds = 0.0;
        State.ReleaseTimer.Reset();
        _hasInitializedSmoothing = false;
    }

    /// <summary>
    /// Maps 2D camera coordinates into desktop screen coordinates with 15% comfort margins.
    /// </summary>
    public (double screenX, double screenY) MapToScreen(float x, float y, int frameWidth, int frameHeight)
    {
        float marginX = frameWidth * 0.15f;
        float marginY = frameHeight * 0.15f;

        float normX = Math.Clamp((x - marginX) / (frameWidth - 2 * marginX), 0.0f, 1.0f);
        float normY = Math.Clamp((y - marginY) / (frameHeight - 2 * marginY), 0.0f, 1.0f);

        double screenX = normX * ScreenWidth;
        double screenY = normY * ScreenHeight;

        return (screenX, screenY);
    }

    /// <summary>
    /// Performs the exact mathematical inverse of <see cref="MapToScreen"/> to map desktop screen coordinates back into camera pixel space.
    /// </summary>
    public (float camX, float camY) MapFromScreen(int screenX, int screenY, int frameWidth, int frameHeight)
    {
        float normX = (float)screenX / ScreenWidth;
        float normY = (float)screenY / ScreenHeight;

        float marginX = frameWidth * 0.15f;
        float marginY = frameHeight * 0.15f;

        float camX = marginX + normX * (frameWidth - 2 * marginX);
        float camY = marginY + normY * (frameHeight - 2 * marginY);

        return (camX, camY);
    }
}
