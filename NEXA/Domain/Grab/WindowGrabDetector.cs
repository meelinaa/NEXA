using System;
using NEXA.Adapters.Output;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Grab;

/// <summary>
/// Domain-level analyzer evaluating continuous fist-hold gestures, delta window repositioning, initial 50%x50% scaling, and 8-zone Snap-to-Side layouts.
/// <para>
/// <b>What it is:</b> The gesture detector evaluating real Windows desktop window manipulation.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="number">
/// <item><description><b>Hold Detection &amp; Auto-Resize:</b> Holding a fist for 2.0s over a window engages dragging and automatically resizes the window to 50% width and 50% height (1/4 screen area) centered under the hand.</description></item>
/// <item><description><b>Delta Dragging:</b> Translates the window by tracking relative hand coordinate changes from the initial grab anchor.</description></item>
/// <item><description><b>8-Zone Spatial Snapping:</b> Supports Left/Right half splits (50%x100%), Top/Bottom half splits (100%x50%), and 4 Corner quadrants (50%x50%).</description></item>
/// <item><description><b>Latch Lock (300ms) &amp; Inward Un-docking:</b> Locks docked geometry for 300ms, requiring a 16% inward pull to un-dock cleanly without accidental drops.</description></item>
/// <item><description><b>Release Debouncing:</b> Enforces a 120ms buffer to absorb temporary tracking flutter without dropping the window.</description></item>
/// </list>
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
    /// Ratio of monitor dimensions (5.0%) defining the snap engagement zone for hand / window center.
    /// </summary>
    public const double SnapEdgeRatio = 0.05;

    /// <summary>
    /// Ratio of monitor dimensions (16.0%) defining the inward displacement required to un-dock.
    /// </summary>
    public const double UnsnapRatio = 0.16;

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
                if (hwnd != IntPtr.Zero && inputSink.GetWindowBounds(hwnd, out int _, out int _, out int _, out int _, out string title))
                {
                    State.IsGrabbed = true;
                    State.TargetHwnd = hwnd;
                    State.CachedWindowTitle = title;

                    // Automatically resize grabbed window to 50% width and 50% height (1/4 screen area) centered under hand
                    int grabW = ScreenWidth / 2;
                    int grabH = ScreenHeight / 2;
                    int maxAllowedX = Math.Max(0, ScreenWidth - grabW);
                    int maxAllowedY = Math.Max(0, ScreenHeight - grabH);
                    int grabX = Math.Clamp(currentHandX - grabW / 2, 0, maxAllowedX);
                    int grabY = Math.Clamp(currentHandY - 25, 0, maxAllowedY);

                    inputSink.SetWindowRect(hwnd, grabX, grabY, grabW, grabH);

                    State.InitialWindowBounds = new Rect(grabX, grabY, grabW, grabH);
                    State.PreSnapBounds = new Rect(grabX, grabY, grabW, grabH);
                    State.InitialHandScreenX = currentHandX;
                    State.InitialHandScreenY = currentHandY;
                    State.CurrentTargetX = grabX;
                    State.CurrentTargetY = grabY;
                    State.ActiveSnap = WindowSnapType.None;

                    _smoothedTargetX = grabX;
                    _smoothedTargetY = grabY;
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

            int winW = State.InitialWindowBounds.Width > 0 ? State.InitialWindowBounds.Width : ScreenWidth / 2;
            int winH = State.InitialWindowBounds.Height > 0 ? State.InitialWindowBounds.Height : ScreenHeight / 2;

            // Window center coordinates
            double windowCenterX = rawTargetX + winW / 2.0;
            double windowCenterY = rawTargetY + winH / 2.0;

            // Percentage-based dynamic margins
            int snapMarginX = (int)Math.Max(50.0, ScreenWidth * SnapEdgeRatio);
            int snapMarginY = (int)Math.Max(50.0, ScreenHeight * SnapEdgeRatio);
            int unsnapMarginX = (int)Math.Max(120.0, ScreenWidth * UnsnapRatio);
            int unsnapMarginY = (int)Math.Max(120.0, ScreenHeight * UnsnapRatio);

            bool isNearLeft = currentHandX <= snapMarginX || windowCenterX <= snapMarginX;
            bool isNearRight = currentHandX >= ScreenWidth - snapMarginX || windowCenterX >= ScreenWidth - snapMarginX;
            bool isNearTop = currentHandY <= snapMarginY || windowCenterY <= snapMarginY;
            bool isNearBottom = currentHandY >= ScreenHeight - snapMarginY || windowCenterY >= ScreenHeight - snapMarginY;

            // =========================================================================
            // 8-ZONE SNAP-TO-SIDE & CORNER DOCKING LOGIC
            // =========================================================================
            if (!State.IsSnapped)
            {
                // 1. Check 4 Corner Quadrants first (50% Width x 50% Height)
                if (isNearTop && isNearLeft)
                {
                    State.ActiveSnap = WindowSnapType.TopLeftCorner;
                    State.PreSnapBounds = State.InitialWindowBounds;
                    State.SnapBounds = new Rect(0, 0, ScreenWidth / 2, ScreenHeight / 2);
                    State.SnapLockTimer.Restart(); // Lock for 300ms
                }
                else if (isNearTop && isNearRight)
                {
                    State.ActiveSnap = WindowSnapType.TopRightCorner;
                    State.PreSnapBounds = State.InitialWindowBounds;
                    State.SnapBounds = new Rect(ScreenWidth / 2, 0, ScreenWidth / 2, ScreenHeight / 2);
                    State.SnapLockTimer.Restart(); // Lock for 300ms
                }
                else if (isNearBottom && isNearLeft)
                {
                    State.ActiveSnap = WindowSnapType.BottomLeftCorner;
                    State.PreSnapBounds = State.InitialWindowBounds;
                    State.SnapBounds = new Rect(0, ScreenHeight / 2, ScreenWidth / 2, ScreenHeight / 2);
                    State.SnapLockTimer.Restart(); // Lock for 300ms
                }
                else if (isNearBottom && isNearRight)
                {
                    State.ActiveSnap = WindowSnapType.BottomRightCorner;
                    State.PreSnapBounds = State.InitialWindowBounds;
                    State.SnapBounds = new Rect(ScreenWidth / 2, ScreenHeight / 2, ScreenWidth / 2, ScreenHeight / 2);
                    State.SnapLockTimer.Restart(); // Lock for 300ms
                }
                // 2. Check Vertical Halves (50% Width x 100% Height)
                else if (isNearLeft)
                {
                    State.ActiveSnap = WindowSnapType.LeftHalf;
                    State.PreSnapBounds = State.InitialWindowBounds;
                    State.SnapBounds = new Rect(0, 0, ScreenWidth / 2, ScreenHeight);
                    State.SnapLockTimer.Restart(); // Lock for 300ms
                }
                else if (isNearRight)
                {
                    State.ActiveSnap = WindowSnapType.RightHalf;
                    State.PreSnapBounds = State.InitialWindowBounds;
                    State.SnapBounds = new Rect(ScreenWidth / 2, 0, ScreenWidth / 2, ScreenHeight);
                    State.SnapLockTimer.Restart(); // Lock for 300ms
                }
                // 3. Check Horizontal Halves (100% Width x 50% Height)
                else if (isNearTop)
                {
                    State.ActiveSnap = WindowSnapType.TopHalf;
                    State.PreSnapBounds = State.InitialWindowBounds;
                    State.SnapBounds = new Rect(0, 0, ScreenWidth, ScreenHeight / 2);
                    State.SnapLockTimer.Restart(); // Lock for 300ms
                }
                else if (isNearBottom)
                {
                    State.ActiveSnap = WindowSnapType.BottomHalf;
                    State.PreSnapBounds = State.InitialWindowBounds;
                    State.SnapBounds = new Rect(0, ScreenHeight / 2, ScreenWidth, ScreenHeight / 2);
                    State.SnapLockTimer.Restart(); // Lock for 300ms
                }
            }
            else
            {
                // Enforce 300ms latch lock: during lock duration, keep window firmly docked
                if (State.SnapLockTimer.Elapsed >= WindowGrabState.SnapLockDuration)
                {
                    // Window is docked and past lock: check if user pulls hand away by unsnapMargin
                    bool shouldUnsnap = false;
                    switch (State.ActiveSnap)
                    {
                        case WindowSnapType.LeftHalf:
                            shouldUnsnap = currentHandX > unsnapMarginX;
                            break;
                        case WindowSnapType.RightHalf:
                            shouldUnsnap = currentHandX < ScreenWidth - unsnapMarginX;
                            break;
                        case WindowSnapType.TopHalf:
                            shouldUnsnap = currentHandY > unsnapMarginY;
                            break;
                        case WindowSnapType.BottomHalf:
                            shouldUnsnap = currentHandY < ScreenHeight - unsnapMarginY;
                            break;
                        case WindowSnapType.TopLeftCorner:
                            shouldUnsnap = currentHandX > unsnapMarginX || currentHandY > unsnapMarginY;
                            break;
                        case WindowSnapType.TopRightCorner:
                            shouldUnsnap = currentHandX < ScreenWidth - unsnapMarginX || currentHandY > unsnapMarginY;
                            break;
                        case WindowSnapType.BottomLeftCorner:
                            shouldUnsnap = currentHandX > unsnapMarginX || currentHandY < ScreenHeight - unsnapMarginY;
                            break;
                        case WindowSnapType.BottomRightCorner:
                            shouldUnsnap = currentHandX < ScreenWidth - unsnapMarginX || currentHandY < ScreenHeight - unsnapMarginY;
                            break;
                    }

                    if (shouldUnsnap)
                    {
                        State.ActiveSnap = WindowSnapType.None;
                        State.SnapLockTimer.Reset();

                        // Re-anchor window to hand location using pre-snap 50%x50% dimensions safely without ArgumentException
                        int restoredW = State.PreSnapBounds.Width > 0 ? State.PreSnapBounds.Width : ScreenWidth / 2;
                        int restoredH = State.PreSnapBounds.Height > 0 ? State.PreSnapBounds.Height : ScreenHeight / 2;

                        int maxAllowedX = Math.Max(0, ScreenWidth - restoredW);
                        int maxAllowedY = Math.Max(0, ScreenHeight - restoredH);

                        int restoredX = Math.Clamp(currentHandX - restoredW / 2, 0, maxAllowedX);
                        int restoredY = Math.Clamp(currentHandY - 25, 0, maxAllowedY);

                        State.InitialWindowBounds = new Rect(restoredX, restoredY, restoredW, restoredH);
                        State.InitialHandScreenX = currentHandX;
                        State.InitialHandScreenY = currentHandY;

                        _smoothedTargetX = restoredX;
                        _smoothedTargetY = restoredY;
                    }
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
        State.SnapLockTimer.Reset();
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
