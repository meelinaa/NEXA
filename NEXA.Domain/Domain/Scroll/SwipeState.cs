using System;
using System.Collections.Generic;

namespace NEXA.Domain.Scroll;

/// <summary>
/// State container tracking temporal position history, recoil protection, and physics momentum for swipe gestures.
/// <para>
/// <b>What it is:</b> The state machine memory model for <see cref="ScrollDetector"/>.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description>Maintains a sliding 250ms history queue of timestamped vertical palm coordinates.</description></item>
/// <item><description>Tracks hand recoil rest states to prevent accidental reverse-scroll triggers upon hand return.</description></item>
/// <item><description>Stores physical inertia velocity and accumulated sub-notch scroll deltas for smooth momentum coasting.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Why it is used:</b> Encapsulates dynamic gesture metrics and physics tuning constants in a clean domain structure.
/// </para>
/// <para>
/// <b>Consequence:</b> Provides stable, jitter-free swipe evaluation across consecutive frames.
/// </para>
/// </summary>
public class SwipeState
{
    /// <summary>
    /// Sliding window queue of timestamped vertical palm positions (Y coordinate in pixels, timestamp).
    /// </summary>
    public Queue<(double Y, DateTime Time)> History { get; } = new();

    /// <summary>
    /// Timestamp of the most recently triggered swipe gesture.
    /// </summary>
    public DateTime LastSwipeTime { get; internal set; } = DateTime.MinValue;

    /// <summary>
    /// Flag indicating that a swipe occurred and the detector is waiting for the hand to come to a rest before allowing a new swipe.
    /// </summary>
    public bool WaitingForRest { get; internal set; } = false;

    /// <summary>
    /// The most recently computed absolute speed (in pixels per millisecond).
    /// </summary>
    public double LastSpeed { get; internal set; } = 0.0;

    /// <summary>
    /// The net vertical displacement across the current history window (in pixels).
    /// </summary>
    public double LastDeltaY { get; internal set; } = 0.0;

    /// <summary>
    /// The latest linear regression slope value (px/ms) indicating movement direction and speed.
    /// </summary>
    public double LastSlope { get; internal set; } = 0.0;

    /// <summary>
    /// Current virtual physics momentum velocity (scroll units per frame). Decays over time via <see cref="MomentumDecay"/>.
    /// </summary>
    public double MomentumVelocity { get; internal set; } = 0.0;

    /// <summary>
    /// Accumulated fractional scroll delta waiting to reach full whole WHEEL_DELTA (120) units.
    /// </summary>
    public double AccumulatedDelta { get; internal set; } = 0.0;

    /// <summary>
    /// Timestamp of the last frame where momentum decay and scroll ticks were processed.
    /// </summary>
    public DateTime LastMomentumUpdate { get; internal set; } = DateTime.MinValue;

    /// <summary>
    /// Duration of the sliding history sample window (250 milliseconds).
    /// </summary>
    public static readonly TimeSpan WindowSize = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Hard minimum cooldown interval (350 milliseconds) immediately following a detected swipe.
    /// </summary>
    public static readonly TimeSpan Cooldown = TimeSpan.FromMilliseconds(350);

    /// <summary>
    /// Minimum required vertical displacement in pixels (25.0 px) across the window to qualify as a swipe.
    /// </summary>
    public const double MinDistance = 25.0;

    /// <summary>
    /// Minimum required regression slope magnitude in px/ms (0.16 px/ms) to qualify as an intentional swipe.
    /// </summary>
    public const double MinSpeed = 0.16;

    /// <summary>
    /// Rest speed threshold in px/ms (0.04 px/ms); hand speed must drop below this threshold to exit rest-wait state.
    /// </summary>
    public const double RestSpeedThreshold = 0.04;

    /// <summary>
    /// Exponential friction decay multiplier applied per 16ms tick (0.91) to simulate natural touchpad inertia.
    /// </summary>
    public const double MomentumDecay = 0.91;

    /// <summary>
    /// Minimum momentum velocity cutoff below which virtual coasting stops completely.
    /// </summary>
    public const double MinMomentumVelocity = 3.0;

    /// <summary>
    /// Indicates whether the swipe detector is currently in refractory cooldown.
    /// </summary>
    public bool InCooldown => (DateTime.Now - LastSwipeTime) < Cooldown;

    /// <summary>
    /// Indicates whether inertial momentum coasting is actively generating scroll ticks.
    /// </summary>
    public bool HasActiveMomentum => Math.Abs(MomentumVelocity) >= MinMomentumVelocity;

    /// <summary>
    /// Indicates whether a swipe interaction or momentum coasting is currently active.
    /// </summary>
    public bool IsSwiping => WaitingForRest || InCooldown || HasActiveMomentum;
}
