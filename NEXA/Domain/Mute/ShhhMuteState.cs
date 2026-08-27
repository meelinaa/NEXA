using System;
using System.Diagnostics;
using OpenCvSharp;

namespace NEXA.Domain.Mute;

/// <summary>
/// State container tracking the "Shhh" mute gesture hold timers, proximity distances, and cooldown timestamps.
/// <para>
/// <b>What it is:</b> State machine model for <see cref="ShhhMuteDetector"/>.
/// </para>
/// </summary>
public class ShhhMuteState
{
    /// <summary>
    /// Indicates whether the Index finger is currently inside the mouth target zone in the correct upright posture.
    /// </summary>
    public bool IsInProximity { get; set; } = false;

    /// <summary>
    /// High-precision stopwatch tracking how long the finger has been continuously held in front of the mouth.
    /// </summary>
    public Stopwatch HoldTimer { get; } = new();

    /// <summary>
    /// Required hold duration in seconds (0.40s) to toggle mute.
    /// </summary>
    public double RequiredHoldSeconds { get; set; } = 0.40;

    /// <summary>
    /// Current normalized hold progress from 0.0 (entered zone) to 1.0 (triggered).
    /// </summary>
    public double HoldProgress => Math.Clamp(HoldTimer.Elapsed.TotalSeconds / RequiredHoldSeconds, 0.0, 1.0);

    /// <summary>
    /// Current distance in camera pixels between Index fingertip and mouth center.
    /// </summary>
    public double CurrentDistanceToMouth { get; set; } = double.MaxValue;

    /// <summary>
    /// Dedicated stopwatch enforcing a 1.5-second post-mute toggle cooldown.
    /// </summary>
    public Stopwatch CooldownTimer { get; } = new();

    /// <summary>
    /// Indicates whether the detector is in refractory cooldown.
    /// </summary>
    public bool InCooldown => CooldownTimer.IsRunning && CooldownTimer.Elapsed.TotalSeconds < 1.5;

    /// <summary>
    /// Timestamp of the last mute toggle event.
    /// </summary>
    public DateTime LastToggleTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Current system mute state (<c>true</c> = Muted, <c>false</c> = Unmuted).
    /// </summary>
    public bool IsMuted { get; set; } = false;

    /// <summary>
    /// 2D coordinates of the mouth center during the most recent frame.
    /// </summary>
    public Point2f LastMouthCenter { get; set; }

    /// <summary>
    /// Resets the proximity hold timer.
    /// </summary>
    public void Reset()
    {
        IsInProximity = false;
        HoldTimer.Reset();
    }
}
