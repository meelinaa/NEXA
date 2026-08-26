using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace NEXA.Domain.TwoHand;

/// <summary>
/// State container tracking temporal windows, fingertip touch anchors, synchronous downward motions, and cooldowns for two-hand gestures.
/// <para>
/// <b>What it is:</b> The state machine memory model for <see cref="TwoHandGestureDetector"/>.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description>Maintains a 3.0-second active window following a fist-grab release.</description></item>
/// <item><description>Tracks index fingertip touch duration and initial anchor distances for Maximize gestures.</description></item>
/// <item><description>Maintains a 300ms sliding queue of dual-hand coordinates to evaluate synchronous downward velocity for Minimize gestures.</description></item>
/// <item><description>Enforces a 750ms refractory cooldown following any triggered action.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Why it is used:</b> Encapsulates multi-frame gesture tracking metrics and gating rules in a clean domain structure.
/// </para>
/// </summary>
public class TwoHandGestureState
{
    /// <summary>
    /// Duration of the allowed interaction window (3.0 seconds) following a fist-grab release.
    /// </summary>
    public static readonly TimeSpan ActiveWindowDuration = TimeSpan.FromSeconds(3.0);

    /// <summary>
    /// Minimum cooldown duration (750 milliseconds) immediately following an executed gesture.
    /// </summary>
    public static readonly TimeSpan CooldownDuration = TimeSpan.FromMilliseconds(750);

    /// <summary>
    /// Timestamp of when the fist-grab gesture was most recently released.
    /// </summary>
    public DateTime LastFistReleaseTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Timestamp of when a two-hand gesture action was most recently dispatched.
    /// </summary>
    public DateTime LastTriggerTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Indicates whether the 3.0-second post-fist interaction window is currently active.
    /// </summary>
    public bool IsWindowActive => (DateTime.Now - LastFistReleaseTime) <= ActiveWindowDuration;

    /// <summary>
    /// Indicates whether the detector is currently within the 750ms post-action cooldown period.
    /// </summary>
    public bool InCooldown => (DateTime.Now - LastTriggerTime) <= CooldownDuration;

    /// <summary>
    /// Remaining active window duration in seconds.
    /// </summary>
    public double RemainingWindowSeconds => Math.Max(0.0, ActiveWindowDuration.TotalSeconds - (DateTime.Now - LastFistReleaseTime).TotalSeconds);

    // --- Maximize Touch Tracking ---

    /// <summary>
    /// Gets or sets a value indicating whether both index fingertips are actively touching.
    /// </summary>
    public bool IsTouchActive { get; set; } = false;

    /// <summary>
    /// Number of consecutive frames the index fingertips have been touching.
    /// </summary>
    public int ConsecutiveTouchFrames { get; set; } = 0;

    /// <summary>
    /// Timestamp when index fingertip touch was established.
    /// </summary>
    public DateTime TouchStartTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Horizontal distance between index fingertips captured at the touch anchor moment.
    /// </summary>
    public double TouchAnchorDistance { get; set; } = 0.0;

    /// <summary>
    /// 2D coordinate of the first hand's index fingertip at touch time.
    /// </summary>
    public Point2f TouchPoint1 { get; set; }

    /// <summary>
    /// 2D coordinate of the second hand's index fingertip at touch time.
    /// </summary>
    public Point2f TouchPoint2 { get; set; }

    // --- Minimize Synchronous Downward Tracking ---

    /// <summary>
    /// Sliding window queue of timestamped dual-hand palm positions for downward velocity analysis.
    /// </summary>
    public Queue<(Point2f Hand1, Point2f Hand2, DateTime Time)> DownwardHistory { get; } = new();

    // --- Visual Feedback & Telemetry ---

    /// <summary>
    /// Action label ("MAXIMIZE" or "MINIMIZE") of the most recently executed gesture.
    /// </summary>
    public string LastAction { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of the last executed action for floating AR animation rendering.
    /// </summary>
    public DateTime LastFeedbackTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// 2D center point between both hands where visual feedback animation originates.
    /// </summary>
    public Point2f LastFeedbackCenter { get; set; }
}
