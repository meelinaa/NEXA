using System;
using OpenCvSharp;

namespace NEXA.Domain.EarsMute;

/// <summary>
/// State tracking model for the "Hear No Evil" hands-to-ears speaker audio mute gesture.
/// <para>
/// <b>What it is:</b> State container tracking proximity hold durations, timestamps, and cooldown periods.
/// </para>
/// </summary>
public class HearNoEvilState
{
    /// <summary>
    /// Gets or sets a value indicating whether hands are currently held in proximity to the ears.
    /// </summary>
    public bool IsInProximity { get; set; }

    /// <summary>
    /// Accumulated continuous duration in seconds that hands have been held at the ears.
    /// </summary>
    public double HoldDurationSeconds { get; set; }

    /// <summary>
    /// Required hold duration in seconds before triggering mute (0.35s, identical to microphone mute).
    /// </summary>
    public double RequiredHoldSeconds { get; set; } = 0.35;

    /// <summary>
    /// Normalized hold progress ratio [0.0 to 1.0].
    /// </summary>
    public float HoldProgress => (float)Math.Clamp(HoldDurationSeconds / RequiredHoldSeconds, 0.0, 1.0);

    /// <summary>
    /// Timestamp of the last active proximity frame.
    /// </summary>
    public DateTime LastHoldTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Timestamp of the last successful speaker mute toggle.
    /// </summary>
    public DateTime LastToggleTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Gets a value indicating whether the detector is in refractory cooldown (1.5s) to eliminate toggle flicker.
    /// </summary>
    public bool InCooldown => (DateTime.Now - LastToggleTime).TotalSeconds < 1.5;

    /// <summary>
    /// Cached state indicating whether master speaker audio output is currently muted.
    /// </summary>
    public bool IsSpeakerMuted { get; set; }

    /// <summary>
    /// Last detected spatial coordinate of the left ear.
    /// </summary>
    public Point2f LastLeftEar { get; set; }

    /// <summary>
    /// Last detected spatial coordinate of the right ear.
    /// </summary>
    public Point2f LastRightEar { get; set; }

    /// <summary>
    /// Resets proximity timers and hold duration.
    /// </summary>
    public void Reset()
    {
        IsInProximity = false;
        HoldDurationSeconds = 0;
        LastHoldTime = DateTime.MinValue;
    }
}
