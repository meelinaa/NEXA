using System;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Grab;

/// <summary>
/// Domain coordinator managing dual-hand pinch-zoom continuous resizing calculations, center-anchored positioning, and monitor boundary clamping.
/// <para>
/// <b>What it is:</b> Secondary-hand window scaling coordinator.
/// </para>
/// </summary>
public class WindowResizeCoordinator
{
    private readonly WindowResizeDetector _resizeDetector;
    private readonly int _screenWidth;
    private readonly int _screenHeight;

    /// <summary>
    /// Gets the internal window resize detector.
    /// </summary>
    public WindowResizeDetector Detector => _resizeDetector;

    /// <summary>
    /// Gets the internal window resize state machine.
    /// </summary>
    public WindowResizeState State => _resizeDetector.State;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowResizeCoordinator"/> class.
    /// </summary>
    /// <param name="screenWidth">Monitor display width in pixels.</param>
    /// <param name="screenHeight">Monitor display height in pixels.</param>
    /// <param name="resizeDetector">Optional custom resize detector.</param>
    public WindowResizeCoordinator(
        int screenWidth,
        int screenHeight,
        WindowResizeDetector? resizeDetector = null)
    {
        _screenWidth = screenWidth > 0 ? screenWidth : 1920;
        _screenHeight = screenHeight > 0 ? screenHeight : 1080;
        _resizeDetector = resizeDetector ?? new WindowResizeDetector();
    }

    /// <summary>
    /// Evaluates secondary hand pinch gestures to continuously scale and reposition grabbed windows around their center point.
    /// </summary>
    /// <param name="grabState">The window grab state container.</param>
    /// <param name="secondaryHand">The tracked secondary hand performing the pinch gesture.</param>
    /// <param name="targetX">Reference to target desktop X coordinate.</param>
    /// <param name="targetY">Reference to target desktop Y coordinate.</param>
    public void ProcessResize(
        WindowGrabState grabState,
        TrackedHand? secondaryHand,
        ref int targetX,
        ref int targetY)
    {
        if (!grabState.IsSnapped && secondaryHand != null)
        {
            int baseW = grabState.InitialWindowBounds.Width > 0 ? grabState.InitialWindowBounds.Width : _screenWidth / 2;
            int baseH = grabState.InitialWindowBounds.Height > 0 ? grabState.InitialWindowBounds.Height : _screenHeight / 2;

            (bool shouldResize, int newWidth, int newHeight) = _resizeDetector.Update(
                secondaryHand,
                baseW,
                baseH,
                _screenWidth,
                _screenHeight);

            if (shouldResize && newWidth > 0 && newHeight > 0)
            {
                double centerX = targetX + baseW / 2.0;
                double centerY = targetY + baseH / 2.0;

                double newTargetX = centerX - newWidth / 2.0;
                double newTargetY = centerY - newHeight / 2.0;

                int maxBoundX = Math.Max(0, _screenWidth - newWidth);
                int maxBoundY = Math.Max(0, _screenHeight - newHeight);

                int clampedX = Math.Clamp((int)Math.Round(newTargetX), 0, maxBoundX);
                int clampedY = Math.Clamp((int)Math.Round(newTargetY), 0, maxBoundY);

                targetX = clampedX;
                targetY = clampedY;

                grabState.InitialWindowBounds = new Rect(clampedX, clampedY, newWidth, newHeight);
                grabState.PreSnapBounds = new Rect(clampedX, clampedY, newWidth, newHeight);
                grabState.CurrentTargetX = clampedX;
                grabState.CurrentTargetY = clampedY;
            }
        }
        else
        {
            _resizeDetector.Reset();
        }
    }

    /// <summary>
    /// Resets the resize detector.
    /// </summary>
    public void Reset()
    {
        _resizeDetector.Reset();
    }
}
