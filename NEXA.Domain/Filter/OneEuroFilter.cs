using System;

namespace NEXA.Filter;

/// <summary>
/// Adaptive First-Order Low-Pass Filter (1€ Filter) for low-latency, jitter-free real-time interactive tracking.
/// <para>
/// <b>What it is:</b> An adaptive filter algorithm designed by Géry Casiez, Nicolas Roussel, and Daniel Vogel (CHI 2012)
/// specifically for noisy Human-Computer Interaction (HCI) input signals.
/// </para>
/// <para>
/// <b>What it does:</b> Dynamically adapts its cutoff frequency based on the speed (derivative) of the input:
/// <list type="bullet">
/// <item><description>At low speeds / stillness: Uses a low cutoff frequency (<see cref="_minCutoff"/>) to aggressively eliminate webcam landmark jitter.</description></item>
/// <item><description>At high speeds: Scales the cutoff frequency linearly with speed (<see cref="_beta"/>) to eliminate lag and provide zero-latency responsiveness.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Why it is used:</b> Standard low-pass filters introduce sluggish delay during fast hand swipes and inadequate jitter removal during pointing.
/// The 1€ filter resolves this classic trade-off between jitter and lag.
/// </para>
/// <para>
/// <b>Consequence:</b> Hand landmarks and mouse cursor movements feel smooth, rock-steady when still, and instant during rapid motion.
/// </para>
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="OneEuroFilter"/> class with specified tuning parameters.
/// </remarks>
/// <param name="freq">Estimated sampling rate in Hz (default: 30.0).</param>
/// <param name="minCutoff">Minimum cutoff frequency in Hz; smaller = heavier still-hand smoothing (default: 1.0).</param>
/// <param name="beta">Velocity adaptation parameter; larger = faster lag elimination during motion (default: 0.007).</param>
/// <param name="dCutoff">Derivative cutoff frequency in Hz (default: 1.0).</param>
public class OneEuroFilter(double freq = 30.0, double minCutoff = 1.0, double beta = 0.007, double dCutoff = 1.0)
{
    /// <summary>
    /// Default sampling frequency (frames per second), used as a fallback if timestamps are missing or invalid.
    /// </summary>
    private readonly double _freq = freq;

    /// <summary>
    /// Minimum cutoff frequency (Hz) at zero velocity. Lower values provide stronger smoothing when the hand is still.
    /// </summary>
    private readonly double _minCutoff = minCutoff;

    /// <summary>
    /// Speed coefficient / sensitivity. Higher values increase responsiveness during fast motions to eliminate lag.
    /// </summary>
    private readonly double _beta = beta;

    /// <summary>
    /// Cutoff frequency (Hz) used for filtering the derivative (speed estimation).
    /// </summary>
    private readonly double _dCutoff = dCutoff;

    /// <summary>
    /// Primary low-pass filter instance for the positional signal.
    /// </summary>
    private readonly LowPassFilter _x = new();

    /// <summary>
    /// Secondary low-pass filter instance for the velocity derivative signal.
    /// </summary>
    private readonly LowPassFilter _dx = new();

    /// <summary>
    /// Timestamp (in seconds) of the previous processed frame.
    /// </summary>
    private double? _lastTime;

    /// <summary>
    /// Processes a new raw coordinate measurement with its associated timestamp and returns the dynamically smoothed value.
    /// </summary>
    /// <param name="value">The raw incoming measurement (e.g., X, Y, or Z coordinate in pixel/normalized space).</param>
    /// <param name="timestamp">The current frame timestamp in seconds.</param>
    /// <returns>The adaptive filtered output coordinate.</returns>
    public double Filter(double value, double timestamp)
    {
        // Compute delta time (dt) between consecutive frames; guard against zero or negative time intervals
        double dt = _lastTime.HasValue ? timestamp - _lastTime.Value : 1.0 / _freq;
        if (dt <= 0) dt = 1.0 / _freq;
        _lastTime = timestamp;

        // 1. Calculate raw velocity derivative: dx/dt
        double dValue = _x.HasLastValue ? (value - _x.LastValue) / dt : 0.0;

        // 2. Smooth the velocity derivative using derivative cutoff frequency
        double edValue = _dx.Filter(dValue, Alpha(dt, _dCutoff));

        // 3. Adaptively compute dynamic cutoff frequency based on current speed
        double cutoff = _minCutoff + _beta * Math.Abs(edValue);

        // 4. Apply primary filter to the position value using adaptive cutoff
        return _x.Filter(value, Alpha(dt, cutoff));
    }

    /// <summary>
    /// Clears historical filter states and timestamps.
    /// Call when a tracked hand disappears or a new hand instance begins tracking.
    /// </summary>
    public void Reset()
    {
        _x.Reset();
        _dx.Reset();
        _lastTime = null;
    }

    /// <summary>
    /// Computes the smoothing coefficient (alpha) for a given delta time and cutoff frequency.
    /// Formula: <c>alpha = 1 / (1 + (1 / (2 * PI * cutoff)) / dt)</c>.
    /// </summary>
    private static double Alpha(double dt, double cutoff)
    {
        double tau = 1.0 / (2.0 * Math.PI * cutoff);
        return 1.0 / (1.0 + tau / dt);
    }
}
