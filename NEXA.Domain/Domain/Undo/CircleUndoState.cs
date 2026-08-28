using System;
using System.Diagnostics;
using OpenCvSharp;

namespace NEXA.Domain.Undo;

/// <summary>
/// State container tracking Peace-sign wrist twist angles, baseline orientations, active session timers, and cooldowns.
/// <para>
/// <b>What it is:</b> The state machine memory model for wrist-twist Undo/Redo detection.
/// </para>
/// </summary>
public class CircleUndoState
{
    /// <summary>
    /// Initial baseline angle in degrees when the Peace gesture was first formed.
    /// </summary>
    public double? InitialAngleDeg { get; internal set; } = null;

    /// <summary>
    /// Current instantaneous orientation angle in degrees.
    /// </summary>
    public double CurrentAngleDeg { get; internal set; } = 0.0;

    /// <summary>
    /// Current signed angular delta from the initial baseline in degrees (negative = left/CCW, positive = right/CW).
    /// </summary>
    public double AngleDeltaDeg { get; internal set; } = 0.0;

    /// <summary>
    /// Wrist position (Landmark 0) in 2D camera coordinates.
    /// </summary>
    public Point2f WristPos { get; internal set; }

    /// <summary>
    /// Midpoint between Index tip (8) and Middle tip (12) in 2D camera coordinates.
    /// </summary>
    public Point2f FingerTipsPos { get; internal set; }

    /// <summary>
    /// Indicates whether the Peace gesture is actively held.
    /// </summary>
    public bool IsTracking { get; internal set; } = false;

    /// <summary>
    /// Stopwatch tracking how long the current twist session has been active (3.0s window).
    /// </summary>
    public Stopwatch SessionTimer { get; } = new();

    /// <summary>
    /// Dedicated stopwatch enforcing a 1.0-second post-action cooldown.
    /// </summary>
    public Stopwatch CooldownTimer { get; } = new();

    /// <summary>
    /// Indicates whether the detector is currently in post-action cooldown.
    /// </summary>
    public bool InCooldown => CooldownTimer.IsRunning && CooldownTimer.Elapsed.TotalSeconds < 1.0;

    /// <summary>
    /// Indicates whether the wrist-twist undo/redo interaction is actively engaged.
    /// </summary>
    public bool IsActive => IsTracking || InCooldown;

    /// <summary>
    /// Action label ("UNDO" or "REDO") of the most recently dispatched action.
    /// </summary>
    public string LastAction { get; internal set; } = string.Empty;

    /// <summary>
    /// Timestamp of the last executed action for AR feedback animation.
    /// </summary>
    public DateTime LastActionTime { get; internal set; } = DateTime.MinValue;

    /// <summary>
    /// 2D coordinate where the action was executed.
    /// </summary>
    public Point2f LastActionCenter { get; internal set; }

    /// <summary>
    /// Resets active tracking state back to baseline.
    /// </summary>
    public void Reset()
    {
        InitialAngleDeg = null;
        AngleDeltaDeg = 0.0;
        IsTracking = false;
        SessionTimer.Reset();
    }
}
