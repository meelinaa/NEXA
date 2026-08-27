using System;
using NEXA.Adapters.Output;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Grab;

/// <summary>
/// Domain-level analyzer evaluating continuous fist-hold gestures, delta window repositioning, initial 50%x50% scaling, and coordinating 8-zone Snap docking.
/// <para>
/// <b>What it is:</b> The high-level coordinator detecting and managing real Windows desktop window manipulation.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="number">
/// <item><description><b>Hold Detection &amp; Auto-Resize:</b> Holding a fist for 2.0s over a window engages dragging and automatically resizes the window to 50% width and 50% height (1/4 screen area) centered under the hand.</description></item>
/// <item><description><b>Delta Dragging:</b> Translates the window by tracking relative hand coordinate changes from the initial grab anchor.</description></item>
/// <item><description><b>Delegated Snap &amp; Motion Processing:</b> Delegates 8-zone docking to <see cref="WindowSnapEngine"/>, coordinate conversion to <see cref="WindowCoordinateMapper"/>, and jitter smoothing to <see cref="WindowGrabSmoother"/>.</description></item>
/// <item><description><b>Release Debouncing:</b> Enforces a 120ms buffer to absorb temporary tracking flutter without dropping the window.</description></item>
/// </list>
/// </para>
/// </summary>
public class WindowGrabDetector
{
    private readonly WindowCoordinateMapper _coordinateMapper;
    private readonly WindowSnapEngine _snapEngine;
    private readonly WindowGrabSmoother _smoother;

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
    public int ScreenWidth => _coordinateMapper.ScreenWidth;

    /// <summary>
    /// Desktop primary monitor vertical resolution in pixels.
    /// </summary>
    public int ScreenHeight => _coordinateMapper.ScreenHeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowGrabDetector"/> class.
    /// </summary>
    /// <param name="screenWidth">Monitor display width in pixels.</param>
    /// <param name="screenHeight">Monitor display height in pixels.</param>
    /// <param name="coordinateMapper">Optional custom coordinate mapper.</param>
    /// <param name="snapEngine">Optional custom snap engine.</param>
    /// <param name="smoother">Optional custom smoother.</param>
    public WindowGrabDetector(
        int screenWidth,
        int screenHeight,
        WindowCoordinateMapper? coordinateMapper = null,
        WindowSnapEngine? snapEngine = null,
        WindowGrabSmoother? smoother = null)
    {
        _coordinateMapper = coordinateMapper ?? new WindowCoordinateMapper(screenWidth, screenHeight);
        _snapEngine = snapEngine ?? new WindowSnapEngine(screenWidth, screenHeight);
        _smoother = smoother ?? new WindowGrabSmoother();
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
                if (!State.ReleaseTimer.IsRunning)
                {
                    State.ReleaseTimer.Restart();
                }

                if (State.ReleaseTimer.Elapsed >= WindowGrabState.ReleaseTolerance)
                {
                    Reset();
                    return (false, IntPtr.Zero, 0, 0);
                }

                return (true, State.TargetHwnd, State.CurrentTargetX, State.CurrentTargetY);
            }

            ResetHold();
            return (false, IntPtr.Zero, 0, 0);
        }

        // Hand is actively making a fist
        State.ReleaseTimer.Reset();
        Point2f palmCenter = hand!.SmoothedLandmarks2D[9];
        State.LastPalmCenter = palmCenter;

        (double screenX, double screenY) = _coordinateMapper.MapToScreen(palmCenter.X, palmCenter.Y, frameWidth, frameHeight);
        int currentHandX = (int)Math.Round(screenX);
        int currentHandY = (int)Math.Round(screenY);

        if (!State.IsGrabbed)
        {
            if (!State.HoldTimer.IsRunning)
            {
                State.HoldTimer.Restart();
            }

            State.HoldDurationSeconds = State.HoldTimer.Elapsed.TotalSeconds;

            if (State.HoldDurationSeconds >= State.RequiredHoldSeconds)
            {
                IntPtr hwnd = inputSink.GetWindowAt(currentHandX, currentHandY);
                if (hwnd != IntPtr.Zero && inputSink.GetWindowBounds(hwnd, out int _, out int _, out int _, out int _, out string title))
                {
                    State.IsGrabbed = true;
                    State.TargetHwnd = hwnd;
                    State.CachedWindowTitle = title;

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

                    _smoother.SetPosition(grabX, grabY);
                }
                else
                {
                    ResetHold();
                }
            }
        }
        else
        {
            int deltaX = currentHandX - State.InitialHandScreenX;
            int deltaY = currentHandY - State.InitialHandScreenY;

            double rawTargetX = State.InitialWindowBounds.X + deltaX;
            double rawTargetY = State.InitialWindowBounds.Y + deltaY;

            int winW = State.InitialWindowBounds.Width > 0 ? State.InitialWindowBounds.Width : ScreenWidth / 2;
            int winH = State.InitialWindowBounds.Height > 0 ? State.InitialWindowBounds.Height : ScreenHeight / 2;

            _snapEngine.ProcessSnapping(
                State,
                currentHandX,
                currentHandY,
                rawTargetX,
                rawTargetY,
                winW,
                winH,
                out bool shouldReanchor,
                out int reanchoredX,
                out int reanchoredY);

            if (shouldReanchor)
            {
                _smoother.SetPosition(reanchoredX, reanchoredY);
            }

            if (State.IsSnapped)
            {
                State.CurrentTargetX = State.SnapBounds.X;
                State.CurrentTargetY = State.SnapBounds.Y;
                return (true, State.TargetHwnd, State.CurrentTargetX, State.CurrentTargetY);
            }

            (int smoothedX, int smoothedY) = _smoother.Smooth(rawTargetX, rawTargetY);
            State.CurrentTargetX = smoothedX;
            State.CurrentTargetY = smoothedY;

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
        _smoother.Reset();
    }

    /// <summary>
    /// Maps 2D camera coordinates into desktop screen coordinates with 15% comfort margins.
    /// </summary>
    public (double screenX, double screenY) MapToScreen(float x, float y, int frameWidth, int frameHeight)
    {
        return _coordinateMapper.MapToScreen(x, y, frameWidth, frameHeight);
    }

    /// <summary>
    /// Performs the exact mathematical inverse of <see cref="MapToScreen"/> to map desktop screen coordinates back into camera pixel space.
    /// </summary>
    public (float camX, float camY) MapFromScreen(int screenX, int screenY, int frameWidth, int frameHeight)
    {
        return _coordinateMapper.MapFromScreen(screenX, screenY, frameWidth, frameHeight);
    }
}
