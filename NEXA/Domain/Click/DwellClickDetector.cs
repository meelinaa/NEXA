using System;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Click;

/// <summary>
/// Domain-level mouse pointer mapping and dwell-click detector.
/// <para>
/// <b>What it is:</b> A hardware-agnostic calculator that translates fingertip camera coordinates to desktop pixel space and detects stationary dwell clicks.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="number">
/// <item><description><b>Normalized Screen Mapping:</b> Applies a 15% outer frame margin and scales normalized camera coordinates to actual screen resolution.</description></item>
/// <item><description><b>Dynamic Smoothing &amp; Deadzone:</b> Filters micro-jitter using a 2.5px stillness deadzone and speed-adaptive exponential interpolation.</description></item>
/// <item><description><b>Dwell Timer &amp; Radial Charge:</b> Tracks whether the cursor remains within a 28px anchor radius for 0.85 seconds to trigger a click.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Why it is used:</b> Eliminates hand tremor and provides a reliable, gesture-free mouse click mechanism without relying on Win32 API calls directly.
/// </para>
/// <para>
/// <b>Consequence:</b> Emits smoothed cursor coordinates and click trigger booleans for consumption by <see cref="MouseController"/>.
/// </para>
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DwellClickDetector"/> class with specified desktop display dimensions.
/// </remarks>
/// <param name="screenWidth">Monitor width in pixels (e.g. 1920, 2560, 3840).</param>
/// <param name="screenHeight">Monitor height in pixels (e.g. 1080, 1440, 2160).</param>
public class DwellClickDetector(int screenWidth, int screenHeight)
{
    /// <summary>
    /// The internal dwell state model tracking hover progress and timer metrics.
    /// </summary>
    public DwellClickState DwellState { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether pointer tracking and dwell clicking are active.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The physical pixel width of the user's primary monitor display.
    /// </summary>
    public int ScreenWidth { get; } = screenWidth;

    /// <summary>
    /// The physical pixel height of the user's primary monitor display.
    /// </summary>
    public int ScreenHeight { get; } = screenHeight;

    /// <summary>
    /// Timestamp of when the user was last actively in the "Pointing" gesture.
    /// </summary>
    public DateTime LastPointerActiveTime { get; private set; } = DateTime.MinValue;

    /// <summary>
    /// 2D camera coordinates of the index fingertip when the most recent click was triggered.
    /// </summary>
    public Point2f LastClickPosition { get; private set; }

    /// <summary>
    /// Internal smoothed horizontal screen coordinate accumulator.
    /// </summary>
    private double _smoothedScreenX = 0;

    /// <summary>
    /// Internal smoothed vertical screen coordinate accumulator.
    /// </summary>
    private double _smoothedScreenY = 0;

    /// <summary>
    /// Flag indicating whether the smoothing accumulator has been initialized with the first frame's target position.
    /// </summary>
    private bool _hasInitializedPos = false;

    /// <summary>
    /// Processes hand tracking data for the current frame to update cursor smoothing and evaluate dwell clicking.
    /// </summary>
    /// <param name="hand">The tracked hand instance.</param>
    /// <param name="frameWidth">Width of the camera frame in pixels.</param>
    /// <param name="frameHeight">Height of the camera frame in pixels.</param>
    /// <returns>A tuple containing optional target (moveX, moveY) screen coordinates and a boolean indicating whether a click event occurred.</returns>
    public (int? moveX, int? moveY, bool shouldClick) Update(TrackedHand? hand, int frameWidth, int frameHeight)
    {
        if (!Enabled || hand == null)
        {
            ResetHover();
            return (null, null, false);
        }

        string currentGesture = hand.Gesture;
        var indexTip = hand.SmoothedLandmarks2D[8]; // Landmark 8 = Index fingertip

        // Pointer navigation is strictly active only during the "Pointing" gesture
        if (currentGesture != "Pointing")
        {
            ResetHover();
            return (null, null, false);
        }

        LastPointerActiveTime = DateTime.Now;
        var (targetX, targetY) = MapToScreen(indexTip.X, indexTip.Y, frameWidth, frameHeight);

        // 1. Dynamic smoothing with stillness deadzone to eliminate webcam tremor
        if (!_hasInitializedPos)
        {
            _smoothedScreenX = targetX;
            _smoothedScreenY = targetY;
            _hasInitializedPos = true;
        }
        else
        {
            double dx = targetX - _smoothedScreenX;
            double dy = targetY - _smoothedScreenY;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            // Deadzone: Ignore micro-drifts smaller than 2.5 pixels to keep cursor rock-steady
            if (dist > 2.5)
            {
                // Dynamic alpha: 0.18 (heavy smoothing during slow aiming) up to 0.85 (direct tracking during fast sweeps)
                double alpha = Math.Clamp(0.18 + (dist / 150.0) * 0.55, 0.18, 0.85);
                _smoothedScreenX += dx * alpha;
                _smoothedScreenY += dy * alpha;
            }
        }

        int finalScreenX = (int)Math.Round(_smoothedScreenX);
        int finalScreenY = (int)Math.Round(_smoothedScreenY);

        // 2. Evaluate stationary dwell hover state
        bool shouldClick = UpdateDwellClick(finalScreenX, finalScreenY, indexTip);

        return (finalScreenX, finalScreenY, shouldClick);
    }

    /// <summary>
    /// Updates the dwell hover state machine and returns <c>true</c> when the required dwell duration is achieved.
    /// </summary>
    private bool UpdateDwellClick(int screenX, int screenY, Point2f indexTip)
    {
        if (DwellState.InCooldown)
        {
            ResetHover();
            return false;
        }

        Point2f currentScreenPt = new(screenX, screenY);

        if (!DwellState.IsHovering)
        {
            DwellState.IsHovering = true;
            DwellState.AnchorScreenPos = currentScreenPt;
            DwellState.DwellTimer.Restart();
            DwellState.HoverProgress = 0.0;
            return false;
        }

        // Compute distance from established anchor center
        double distFromAnchor = Math.Sqrt(
            Math.Pow(currentScreenPt.X - DwellState.AnchorScreenPos.X, 2) +
            Math.Pow(currentScreenPt.Y - DwellState.AnchorScreenPos.Y, 2)
        );

        // Check if hand remains within allowable dwell radius
        if (distFromAnchor <= DwellState.DwellRadiusPx)
        {
            double elapsed = DwellState.DwellTimer.Elapsed.TotalSeconds;
            DwellState.HoverProgress = Math.Clamp(elapsed / DwellState.RequiredDwellSeconds, 0.0, 1.0);

            // Trigger click event upon reaching 100% progress
            if (DwellState.HoverProgress >= 1.0)
            {
                LastClickPosition = indexTip;
                DwellState.LastClickTime = DateTime.Now;
                ResetHover();
                return true;
            }
        }
        else
        {
            // Reset anchor when drifting outside tolerance circle
            DwellState.AnchorScreenPos = currentScreenPt;
            DwellState.DwellTimer.Restart();
            DwellState.HoverProgress = 0.0;
        }

        return false;
    }

    /// <summary>
    /// Resets active hover progress and stops the timer.
    /// </summary>
    private void ResetHover()
    {
        DwellState.IsHovering = false;
        DwellState.HoverProgress = 0.0;
        DwellState.DwellTimer.Reset();
    }

    /// <summary>
    /// Maps 2D camera coordinates into desktop screen coordinates with comfort margins.
    /// </summary>
    /// <param name="x">Raw X coordinate in camera image space.</param>
    /// <param name="y">Raw Y coordinate in camera image space.</param>
    /// <param name="frameWidth">Camera frame width.</param>
    /// <param name="frameHeight">Camera frame height.</param>
    /// <returns>The scaled (screenX, screenY) pixel coordinates on the user's monitor.</returns>
    public (double screenX, double screenY) MapToScreen(float x, float y, int frameWidth, int frameHeight)
    {
        // 15% comfort padding prevents user from having to reach to the absolute physical edge of the camera view
        float marginX = frameWidth * 0.15f;
        float marginY = frameHeight * 0.15f;

        float normX = Math.Clamp((x - marginX) / (frameWidth - 2 * marginX), 0.0f, 1.0f);
        float normY = Math.Clamp((y - marginY) / (frameHeight - 2 * marginY), 0.0f, 1.0f);

        double screenX = normX * ScreenWidth;
        double screenY = normY * ScreenHeight;

        return (screenX, screenY);
    }
}
