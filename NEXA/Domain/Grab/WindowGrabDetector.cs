using System;
using NEXA.Adapters.Output;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Grab;

/// <summary>
/// Domain-level analyzer evaluating continuous fist-hold gestures, delta window repositioning, and Windows-style Snap-to-Side edge docking.
/// <para>
/// <b>What it is:</b> The gesture detector evaluating real Windows desktop window manipulation.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="number">
/// <item><description><b>Hold Detection:</b> Requires holding a clenched fist for 2.0 seconds directly over an OS window to engage dragging.</description></item>
/// <item><description><b>Delta Dragging:</b> Translates the window by tracking relative hand coordinate changes from the initial grab anchor.</description></item>
/// <item><description><b>Edge Snap Docking:</b> Automatically docks the window to Left Half, Right Half, or Top Maximize when dragged near screen borders (&lt;=35px).</description></item>
/// <item><description><b>Seamless Un-docking:</b> Allows the user to continue dragging away from the docked edge (&gt;65px), seamlessly restoring original geometry.</description></item>
/// <item><description><b>Release Debouncing:</b> Enforces a 120ms buffer to absorb temporary tracking flutter without dropping the window.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Why it is used:</b> Provides native-feeling desktop window dragging and docking without accidental triggers or rigid lock-in.
/// </para>
/// </summary>
public class WindowGrabDetector
{
    /// <summary>
    /// Gets the internal state container tracking hold durations, drag deltas, and snap alignments.
    /// </summary>
    public WindowGrabState State { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether window grab detection is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Desktop primary monitor horizontal resolution in pixels.
    /// </summary>
    public int ScreenWidth { get; }

    /// <summary>
    /// Desktop primary monitor vertical resolution in pixels.
    /// </summary>
    public int ScreenHeight { get; }

    /// <summary>
    /// Pixel distance from monitor borders required to trigger edge snap docking.
    /// </summary>
    public const int SnapEdgeMargin = 35;

    /// <summary>
    /// Pixel distance required to pull away from a docked border before un-snapping back to free dragging.
    /// </summary>
    public const int UnsnapMargin = 65;

    /// <summary>
    /// Internal smoothed horizontal coordinate accumulator.
    /// </summary>
    private double _smoothedTargetX = 0;

    /// <summary>
    /// Internal smoothed vertical coordinate accumulator.
    /// </summary>
    private double _smoothedTargetY = 0;

    /// <summary>
    /// Flag indicating whether the smoothing accumulator has been initialized with the initial window position.
    /// </summary>
    private bool _hasInitializedSmoothing = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowGrabDetector"/> class with display resolution.
    /// </summary>
    /// <param name="screenWidth">Monitor display width in pixels.</param>
    /// <param name="screenHeight">Monitor display height in pixels.</param>
    public WindowGrabDetector(int screenWidth, int screenHeight)
    {
        ScreenWidth = screenWidth > 0 ? screenWidth : 1920;
        ScreenHeight = screenHeight > 0 ? screenHeight : 1080;
    }

    /// <summary>
    /// Evaluates hand tracking data for the current frame, updates grab/drag state, and detects edge snapping.
    /// </summary>
    /// <param name="hand">The tracked hand instance.</param>
    /// <param name="frameWidth">Camera frame width in pixels.</param>
    /// <param name="frameHeight">Camera frame height in pixels.</param>
    /// <param name="inputSink">The output adapter used to query native OS window handles and geometry.</param>
    /// <returns>A tuple containing (isGrabbed, targetHwnd, targetX, targetY).</returns>
    public (bool isGrabbed, IntPtr targetHwnd, int targetX, int targetY) Update(
        TrackedHand? hand,
        int frameWidth,
        int frameHeight,
        IInputSink inputSink)
    {
        if (!Enabled)
        {
            Reset();
            return (false, IntPtr.Zero, 0, 0);
        }

        string currentGesture = hand?.Gesture ?? string.Empty;
        bool isFist = currentGesture == "Fist";

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
                    State.PreSnapBounds = new Rect(winX, winY, winW, winH);
                    State.InitialHandScreenX = currentHandX;
                    State.InitialHandScreenY = currentHandY;
                    State.CurrentTargetX = winX;
                    State.CurrentTargetY = winY;
                    State.ActiveSnap = WindowSnapType.None;

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

            // =========================================================================
            // SNAP-TO-SIDE EDGE DOCKING LOGIC
            // =========================================================================
            if (!State.IsSnapped)
            {
                // Check if hand is dragged near desktop screen borders
                if (currentHandX <= SnapEdgeMargin)
                {
                    State.ActiveSnap = WindowSnapType.LeftHalf;
                    State.PreSnapBounds = State.InitialWindowBounds;
                    State.SnapBounds = new Rect(0, 0, ScreenWidth / 2, ScreenHeight);
                }
                else if (currentHandX >= ScreenWidth - SnapEdgeMargin)
                {
                    State.ActiveSnap = WindowSnapType.RightHalf;
                    State.PreSnapBounds = State.InitialWindowBounds;
                    State.SnapBounds = new Rect(ScreenWidth / 2, 0, ScreenWidth / 2, ScreenHeight);
                }
                else if (currentHandY <= SnapEdgeMargin)
                {
                    State.ActiveSnap = WindowSnapType.TopMaximize;
                    State.PreSnapBounds = State.InitialWindowBounds;
                    State.SnapBounds = new Rect(0, 0, ScreenWidth, ScreenHeight);
                }
            }
            else
            {
                // Window is currently docked: check if user pulls hand away to seamlessly un-snap
                bool shouldUnsnap = false;
                if (State.ActiveSnap == WindowSnapType.LeftHalf && currentHandX > UnsnapMargin)
                {
                    shouldUnsnap = true;
                }
                else if (State.ActiveSnap == WindowSnapType.RightHalf && currentHandX < ScreenWidth - UnsnapMargin)
                {
                    shouldUnsnap = true;
                }
                else if (State.ActiveSnap == WindowSnapType.TopMaximize && currentHandY > UnsnapMargin)
                {
                    shouldUnsnap = true;
                }

                if (shouldUnsnap)
                {
                    State.ActiveSnap = WindowSnapType.None;
                    // Re-anchor window to hand location using pre-snap dimensions without jumps
                    int restoredW = State.PreSnapBounds.Width;
                    int restoredH = State.PreSnapBounds.Height;
                    int restoredX = Math.Clamp(currentHandX - restoredW / 2, 0, ScreenWidth - restoredW);
                    int restoredY = Math.Clamp(currentHandY - 25, 0, ScreenHeight - restoredH);

                    State.InitialWindowBounds = new Rect(restoredX, restoredY, restoredW, restoredH);
                    State.InitialHandScreenX = currentHandX;
                    State.InitialHandScreenY = currentHandY;

                    _smoothedTargetX = restoredX;
                    _smoothedTargetY = restoredY;
                }
            }

            if (State.IsSnapped)
            {
                State.CurrentTargetX = State.SnapBounds.X;
                State.CurrentTargetY = State.SnapBounds.Y;
                return (true, State.TargetHwnd, State.CurrentTargetX, State.CurrentTargetY);
            }

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
    /// Fully resets all grab, drag, smoothing, and snap docking states.
    /// </summary>
    public void Reset()
    {
        State.IsGrabbed = false;
        State.TargetHwnd = IntPtr.Zero;
        State.CachedWindowTitle = string.Empty;
        State.InitialWindowBounds = new Rect();
        State.PreSnapBounds = new Rect();
        State.SnapBounds = new Rect();
        State.ActiveSnap = WindowSnapType.None;
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
