using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenCvSharp;

namespace NEXA.Domain.TwoHand;

/// <summary>
/// State container tracking temporal windows, fingertip touch anchors, synchronous downward motions, camera-framing viewfinder rectangles, screenshot hold durations, and media play/pause cooldowns.
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
/// <item><description>Enforces a 1.5-second cooldown for dual-palm Play/Pause media toggles.</description></item>
/// <item><description>Enforces a 750ms refractory cooldown following any executed window gesture.</description></item>
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
    public DateTime LastFistReleaseTime { get; internal set; } = DateTime.MinValue;

    /// <summary>
    /// Timestamp of when a two-hand gesture action was most recently dispatched.
    /// </summary>
    public DateTime LastTriggerTime { get; internal set; } = DateTime.MinValue;

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

    // --- Media Play/Pause Clap Tracking ---

    /// <summary>
    /// Dedicated stopwatch enforcing a 1.5-second cooldown between consecutive Play/Pause clap triggers.
    /// </summary>
    public Stopwatch MediaPlayPauseCooldownTimer { get; } = new();

    /// <summary>
    /// Indicates whether media play/pause triggers are currently suppressed by cooldown.
    /// </summary>
    public bool IsMediaPlayPauseInCooldown => MediaPlayPauseCooldownTimer.IsRunning && MediaPlayPauseCooldownTimer.Elapsed.TotalSeconds < 1.5;

    /// <summary>
    /// Number of consecutive frames that dual open palms have been touching in clap/prayer posture.
    /// </summary>
    public int ConsecutiveClapFrames { get; internal set; } = 0;

    /// <summary>
    /// Timestamp of the most recent Play/Pause trigger for AR animation feedback.
    /// </summary>
    public DateTime LastMediaPlayPauseTime { get; internal set; } = DateTime.MinValue;

    /// <summary>
    /// 2D camera coordinates center point where the Play/Pause pulse animation originates.
    /// </summary>
    public Point2f LastMediaFeedbackCenter { get; internal set; }

    // --- Camera Frame Screenshot Tracking ---

    /// <summary>
    /// Gets or sets a value indicating whether both hands are simultaneously forming an "L" posture.
    /// </summary>
    public bool IsCameraFrameActive { get; internal set; } = false;

    /// <summary>
    /// The live 2D camera coordinates bounding rectangle spanned by all 4 extended fingertips (Thumb 1, Index 1, Thumb 2, Index 2).
    /// </summary>
    public Rect2f LiveCameraFrameRect { get; internal set; }

    /// <summary>
    /// Dedicated stopwatch tracking continuous double-touch hold duration before triggering a screenshot.
    /// </summary>
    public Stopwatch ScreenshotHoldTimer { get; } = new();

    /// <summary>
    /// The required double-touch hold duration in seconds (2.0s) before executing a screenshot.
    /// </summary>
    public double RequiredScreenshotHoldSeconds { get; internal set; } = 2.0;

    /// <summary>
    /// Elapsed double-touch hold duration in seconds.
    /// </summary>
    public double ScreenshotHoldDurationSeconds { get; internal set; } = 0.0;

    /// <summary>
    /// Normalized hold progress from 0.0 to 1.0.
    /// </summary>
    public double ScreenshotHoldProgress { get; internal set; } = 0.0;

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
    public DateTime LastScreenshotTime { get; internal set; } = DateTime.MinValue;

    /// <summary>
    /// 2D camera coordinates rectangle of the most recently captured screenshot.
    /// </summary>
    public Rect2f LastCapturedFrameRect { get; internal set; }

    /// <summary>
    /// Absolute file system path of the most recently saved screenshot file.
    /// </summary>
    public string LastSavedFilePath { get; internal set; } = string.Empty;

    // --- Maximize Touch Tracking ---

    /// <summary>
    /// Gets or sets a value indicating whether both index fingertips are actively touching.
    /// </summary>
    public bool IsTouchActive { get; internal set; } = false;

    /// <summary>
    /// Number of consecutive frames the index fingertips have been touching.
    /// </summary>
    public int ConsecutiveTouchFrames { get; internal set; } = 0;

    /// <summary>
    /// Timestamp when index fingertip touch was established.
    /// </summary>
    public DateTime TouchStartTime { get; internal set; } = DateTime.MinValue;

    /// <summary>
    /// Horizontal distance between index fingertips captured at the touch anchor moment.
    /// </summary>
    public double TouchAnchorDistance { get; internal set; } = 0.0;

    /// <summary>
    /// 2D coordinate of the first hand's index fingertip at touch time.
    /// </summary>
    public Point2f TouchPoint1 { get; internal set; }

    /// <summary>
    /// 2D coordinate of the second hand's index fingertip at touch time.
    /// </summary>
    public Point2f TouchPoint2 { get; internal set; }

    // --- Minimize Synchronous Downward Tracking ---

    /// <summary>
    /// Sliding window queue of timestamped dual-hand palm positions for downward velocity analysis.
    /// </summary>
    public Queue<(Point2f Hand1, Point2f Hand2, DateTime Time)> DownwardHistory { get; } = new();

    // --- Visual Feedback & Telemetry ---

    /// <summary>
    /// Action label ("MAXIMIZE", "MINIMIZE", "SCREENSHOT", or "PLAY / PAUSE") of the most recently executed gesture.
    /// </summary>
    public string LastAction { get; internal set; } = string.Empty;

    /// <summary>
    /// Timestamp of the last executed action for floating AR animation rendering.
    /// </summary>
    public DateTime LastFeedbackTime { get; internal set; } = DateTime.MinValue;

    /// <summary>
    /// 2D center point between both hands where visual feedback animation originates.
    /// </summary>
    public Point2f LastFeedbackCenter { get; internal set; }
}
