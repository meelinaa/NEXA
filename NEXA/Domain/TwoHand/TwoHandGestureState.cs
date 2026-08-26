using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenCvSharp;

namespace NEXA.Domain.TwoHand;

/// <summary>
/// State container tracking temporal windows, fingertip touch anchors, synchronous downward motions, camera-framing viewfinder rectangles, and screenshot hold durations.
/// <para>
/// <b>What it is:</b> The state machine memory model for <see cref="TwoHandGestureDetector"/>.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description>Maintains a 3.0-second active window following a fist-grab release for window actions.</description></item>
/// <item><description>Tracks live camera-frame bounding boxes spanned by dual "L" hands.</description></item>
/// <item><description>Measures continuous double-touch hold duration (2.0s required) before triggering a screenshot.</description></item>
/// <item><description>Enforces a 2.0-second directed cooldown blocking Maximize and consecutive screenshots.</description></item>
/// <item><description>Enforces a 750ms refractory cooldown following any executed gesture.</description></item>
/// </list>
/// </para>
/// </summary>
public class TwoHandGestureState
{
    /// <summary>
    /// Duration of the allowed interaction window (3.0 seconds) following a fist-grab release for Maximize/Minimize gestures.
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

    // --- Camera Frame Screenshot Tracking ---

    /// <summary>
    /// Gets or sets a value indicating whether both hands are simultaneously forming an "L" posture.
    /// </summary>
    public bool IsCameraFrameActive { get; set; } = false;

    /// <summary>
    /// The live 2D camera coordinates bounding rectangle spanned by all 4 extended fingertips (Thumb 1, Index 1, Thumb 2, Index 2).
    /// </summary>
    public Rect2f LiveCameraFrameRect { get; set; }

    /// <summary>
    /// Dedicated stopwatch tracking continuous double-touch hold duration before triggering a screenshot.
    /// </summary>
    public Stopwatch ScreenshotHoldTimer { get; } = new();

    /// <summary>
    /// The required double-touch hold duration in seconds (2.0s) before executing a screenshot.
    /// </summary>
    public double RequiredScreenshotHoldSeconds { get; set; } = 2.0;

    /// <summary>
    /// Elapsed double-touch hold duration in seconds.
    /// </summary>
    public double ScreenshotHoldDurationSeconds { get; set; } = 0.0;

    /// <summary>
    /// Normalized hold progress from 0.0 to 1.0.
    /// </summary>
    public double ScreenshotHoldProgress { get; set; } = 0.0;

    /// <summary>
    /// Dedicated stopwatch enforcing a 2.0-second cooldown blocking Maximize and consecutive screenshots immediately following a trigger.
    /// </summary>
    public Stopwatch ScreenshotBlockTimer { get; } = new();

    /// <summary>
    /// Indicates whether screenshot actions are currently suppressed by a recent screenshot trigger.
    /// </summary>
    public bool IsScreenshotBlocked => ScreenshotBlockTimer.IsRunning && ScreenshotBlockTimer.Elapsed.TotalSeconds < 2.0;

    /// <summary>
    /// Timestamp of the most recently captured screenshot for AR flash rendering.
    /// </summary>
    public DateTime LastScreenshotTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// 2D camera coordinates rectangle of the most recently captured screenshot.
    /// </summary>
    public Rect2f LastCapturedFrameRect { get; set; }

    /// <summary>
    /// Absolute file system path of the most recently saved screenshot file.
    /// </summary>
    public string LastSavedFilePath { get; set; } = string.Empty;

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
    /// Action label ("MAXIMIZE", "MINIMIZE", or "SCREENSHOT") of the most recently executed gesture.
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
