using System;
using System.Diagnostics;
using OpenCvSharp;

namespace NEXA.Domain.Click;

/// <summary>
/// State container tracking the progress, anchor location, and cooldown of dwell-click hover interactions.
/// <para>
/// <b>What it is:</b> The state model for hands-free mouse clicking via stationary hovering.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description>Maintains an anchor screen coordinate when the user begins hovering in one place.</description></item>
/// <item><description>Measures continuous hover duration within a tolerance radius (<see cref="DwellRadiusPx"/>) using a high-precision stopwatch.</description></item>
/// <item><description>Enforces a 500ms post-click cooldown to prevent accidental double-clicks.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Why it is used:</b> Enables users to click UI buttons and desktop icons simply by holding their index fingertip steady without needing to pinch or switch gestures.
/// </para>
/// <para>
/// <b>Consequence:</b> Provides a predictable, fatigue-free clicking mechanism.
/// </para>
/// </summary>
public class DwellClickState
{
    /// <summary>
    /// Gets or sets a value indicating whether the user's fingertip is actively hovering inside the dwell anchor radius.
    /// </summary>
    public bool IsHovering { get; internal set; } = false;

    /// <summary>
    /// The normalized hover completion progress from 0.0 (started) to 1.0 (click triggered).
    /// Used directly to render the radial charging progress ring.
    /// </summary>
    public double HoverProgress { get; internal set; } = 0.0;

    /// <summary>
    /// The maximum allowable drift radius in screen pixels (28.0 px) around the anchor point before the hover timer resets.
    /// </summary>
    public double DwellRadiusPx { get; internal set; } = 28.0;

    /// <summary>
    /// The required stationary dwell duration in seconds (0.85s) before triggering a left mouse click.
    /// </summary>
    public double RequiredDwellSeconds { get; internal set; } = 0.85;

    /// <summary>
    /// The stationary screen coordinate anchor point where the current dwell cycle started.
    /// </summary>
    public Point2f AnchorScreenPos { get; internal set; }

    /// <summary>
    /// High-precision stopwatch tracking elapsed hover duration at the current anchor point.
    /// </summary>
    public Stopwatch DwellTimer { get; } = new();

    /// <summary>
    /// Timestamp of the most recent successfully dispatched click event.
    /// </summary>
    public DateTime LastClickTime { get; internal set; } = DateTime.MinValue;

    /// <summary>
    /// Indicates whether the clicker is currently in a 500ms post-click cooldown refractory period.
    /// </summary>
    public bool InCooldown => (DateTime.Now - LastClickTime).TotalMilliseconds < 500;
}
