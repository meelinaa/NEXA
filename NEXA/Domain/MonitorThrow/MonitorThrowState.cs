using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace NEXA.Domain.MonitorThrow;

/// <summary>
/// State container tracking edge-on hand orientation, horizontal swipe kinematics, and cooldown timers for multi-monitor window throws.
/// <para>
/// <b>What it is:</b> The state model managing multi-monitor gesture recognition.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description>Maintains an 800ms refractory cooldown to prevent repeated transfers.</description></item>
/// <item><description>Maintains a sliding 250ms history queue of palm coordinates for velocity estimation.</description></item>
/// <item><description>Tracks edge-on orientation metrics (knuckle 5-17 distance ratio).</description></item>
/// <item><description>Holds telemetry and feedback timestamps for AR holographic animations.</description></item>
/// </list>
/// </para>
/// </summary>
public class MonitorThrowState
{
    /// <summary>
    /// Minimum refractory cooldown duration (800 milliseconds) immediately following an executed monitor transfer.
    /// </summary>
    public static readonly TimeSpan CooldownDuration = TimeSpan.FromMilliseconds(800);

    /// <summary>
    /// Timestamp of when the most recent monitor throw gesture was dispatched.
    /// </summary>
    public DateTime LastSwipeTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Indicates whether the detector is currently in the post-throw cooldown period.
    /// </summary>
    public bool InCooldown => (DateTime.Now - LastSwipeTime) <= CooldownDuration;

    /// <summary>
    /// Gets or sets a value indicating whether the tracked hand is currently held in an edge-on ("Knife Hand") posture.
    /// </summary>
    public bool IsEdgeOnPosture { get; set; } = false;

    /// <summary>
    /// The ratio between knuckle distance (Index MCP [5] to Pinky MCP [17]) and palm size (Wrist [0] to Middle MCP [9]).
    /// </summary>
    public double KnuckleCompressionRatio { get; set; } = 1.0;

    /// <summary>
    /// Sliding window queue of timestamped palm positions for horizontal velocity regression.
    /// </summary>
    public Queue<(double X, double Y, DateTime Time)> History { get; } = new();

    /// <summary>
    /// Direction string ("LEFT" or "RIGHT") of the most recently executed monitor throw.
    /// </summary>
    public string LastDirection { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of the last executed monitor throw for floating holographic arrow rendering.
    /// </summary>
    public DateTime LastFeedbackTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// 2D image coordinates of the palm center where the throw animation originates.
    /// </summary>
    public Point2f LastSwipeCenter { get; set; }
}
