using System;
using OpenCvSharp;

namespace NEXA.Domain.Grab;

/// <summary>
/// Domain engine evaluating 8-zone desktop edge and corner snap docking, latch lock durations, and inward un-docking displacements.
/// <para>
/// <b>What it is:</b> Mathematical spatial tiling and window docking calculator.
/// </para>
/// </summary>
public class WindowSnapEngine
{
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
    /// Initializes a new instance of the <see cref="WindowSnapEngine"/> class.
    /// </summary>
    /// <param name="screenWidth">Monitor display width in pixels.</param>
    /// <param name="screenHeight">Monitor display height in pixels.</param>
    public WindowSnapEngine(int screenWidth, int screenHeight)
    {
        ScreenWidth = screenWidth > 0 ? screenWidth : 1920;
        ScreenHeight = screenHeight > 0 ? screenHeight : 1080;
    }

    /// <summary>
    /// Evaluates current hand position and window bounds to engage or release 8-zone snap docking layouts.
    /// </summary>
    /// <param name="state">The window grab state container.</param>
    /// <param name="currentHandX">Current hand desktop X coordinate.</param>
    /// <param name="currentHandY">Current hand desktop Y coordinate.</param>
    /// <param name="rawTargetX">Raw computed window X coordinate before smoothing.</param>
    /// <param name="rawTargetY">Raw computed window Y coordinate before smoothing.</param>
    /// <param name="winW">Window width in pixels.</param>
    /// <param name="winH">Window height in pixels.</param>
    /// <param name="shouldReanchor">Outputs true if un-docking occurred and window needs re-anchoring.</param>
    /// <param name="reanchoredX">Outputs re-anchored X coordinate.</param>
    /// <param name="reanchoredY">Outputs re-anchored Y coordinate.</param>
    public void ProcessSnapping(
        WindowGrabState state,
        int currentHandX,
        int currentHandY,
        double rawTargetX,
        double rawTargetY,
        int winW,
        int winH,
        out bool shouldReanchor,
        out int reanchoredX,
        out int reanchoredY)
    {
        shouldReanchor = false;
        reanchoredX = 0;
        reanchoredY = 0;

        double windowCenterX = rawTargetX + winW / 2.0;
        double windowCenterY = rawTargetY + winH / 2.0;

        int snapMarginX = (int)Math.Max(50.0, ScreenWidth * SnapEdgeRatio);
        int snapMarginY = (int)Math.Max(50.0, ScreenHeight * SnapEdgeRatio);
        int unsnapMarginX = (int)Math.Max(120.0, ScreenWidth * UnsnapRatio);
        int unsnapMarginY = (int)Math.Max(120.0, ScreenHeight * UnsnapRatio);

        bool isNearLeft = currentHandX <= snapMarginX || windowCenterX <= snapMarginX;
        bool isNearRight = currentHandX >= ScreenWidth - snapMarginX || windowCenterX >= ScreenWidth - snapMarginX;
        bool isNearTop = currentHandY <= snapMarginY || windowCenterY <= snapMarginY;
        bool isNearBottom = currentHandY >= ScreenHeight - snapMarginY || windowCenterY >= ScreenHeight - snapMarginY;

        if (!state.IsSnapped)
        {
            // 1. Check 4 Corner Quadrants (50% Width x 50% Height)
            if (isNearTop && isNearLeft)
            {
                state.ActiveSnap = WindowSnapType.TopLeftCorner;
                state.PreSnapBounds = state.InitialWindowBounds;
                state.SnapBounds = new Rect(0, 0, ScreenWidth / 2, ScreenHeight / 2);
                state.SnapLockTimer.Restart();
            }
            else if (isNearTop && isNearRight)
            {
                state.ActiveSnap = WindowSnapType.TopRightCorner;
                state.PreSnapBounds = state.InitialWindowBounds;
                state.SnapBounds = new Rect(ScreenWidth / 2, 0, ScreenWidth / 2, ScreenHeight / 2);
                state.SnapLockTimer.Restart();
            }
            else if (isNearBottom && isNearLeft)
            {
                state.ActiveSnap = WindowSnapType.BottomLeftCorner;
                state.PreSnapBounds = state.InitialWindowBounds;
                state.SnapBounds = new Rect(0, ScreenHeight / 2, ScreenWidth / 2, ScreenHeight / 2);
                state.SnapLockTimer.Restart();
            }
            else if (isNearBottom && isNearRight)
            {
                state.ActiveSnap = WindowSnapType.BottomRightCorner;
                state.PreSnapBounds = state.InitialWindowBounds;
                state.SnapBounds = new Rect(ScreenWidth / 2, ScreenHeight / 2, ScreenWidth / 2, ScreenHeight / 2);
                state.SnapLockTimer.Restart();
            }
            // 2. Check Vertical Halves (50% Width x 100% Height)
            else if (isNearLeft)
            {
                state.ActiveSnap = WindowSnapType.LeftHalf;
                state.PreSnapBounds = state.InitialWindowBounds;
                state.SnapBounds = new Rect(0, 0, ScreenWidth / 2, ScreenHeight);
                state.SnapLockTimer.Restart();
            }
            else if (isNearRight)
            {
                state.ActiveSnap = WindowSnapType.RightHalf;
                state.PreSnapBounds = state.InitialWindowBounds;
                state.SnapBounds = new Rect(ScreenWidth / 2, 0, ScreenWidth / 2, ScreenHeight);
                state.SnapLockTimer.Restart();
            }
            // 3. Check Horizontal Halves (100% Width x 50% Height)
            else if (isNearTop)
            {
                state.ActiveSnap = WindowSnapType.TopHalf;
                state.PreSnapBounds = state.InitialWindowBounds;
                state.SnapBounds = new Rect(0, 0, ScreenWidth, ScreenHeight / 2);
                state.SnapLockTimer.Restart();
            }
            else if (isNearBottom)
            {
                state.ActiveSnap = WindowSnapType.BottomHalf;
                state.PreSnapBounds = state.InitialWindowBounds;
                state.SnapBounds = new Rect(0, ScreenHeight / 2, ScreenWidth, ScreenHeight / 2);
                state.SnapLockTimer.Restart();
            }
        }
        else
        {
            // Enforce 300ms latch lock: during lock duration, keep window firmly docked
            if (state.SnapLockTimer.Elapsed >= WindowGrabState.SnapLockDuration)
            {
                bool shouldUnsnap = false;
                switch (state.ActiveSnap)
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
                    state.ActiveSnap = WindowSnapType.None;
                    state.SnapLockTimer.Reset();

                    int restoredW = state.PreSnapBounds.Width > 0 ? state.PreSnapBounds.Width : ScreenWidth / 2;
                    int restoredH = state.PreSnapBounds.Height > 0 ? state.PreSnapBounds.Height : ScreenHeight / 2;

                    int maxAllowedX = Math.Max(0, ScreenWidth - restoredW);
                    int maxAllowedY = Math.Max(0, ScreenHeight - restoredH);

                    int restoredX = Math.Clamp(currentHandX - restoredW / 2, 0, maxAllowedX);
                    int restoredY = Math.Clamp(currentHandY - 25, 0, maxAllowedY);

                    state.InitialWindowBounds = new Rect(restoredX, restoredY, restoredW, restoredH);
                    state.InitialHandScreenX = currentHandX;
                    state.InitialHandScreenY = currentHandY;

                    shouldReanchor = true;
                    reanchoredX = restoredX;
                    reanchoredY = restoredY;
                }
            }
        }
    }
}
