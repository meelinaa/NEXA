using System;
using OpenCvSharp;

namespace NEXA.Filter;

/// <summary>
/// Two-Dimensional Adaptive First-Order Low-Pass Filter (1€ Filter 2D) using Euclidean velocity for isotropic spatial smoothing.
/// <para>
/// <b>What it is:</b> A 2D extension of the 1€ Filter designed specifically for planar points $(X, Y)$ and facial landmarks.
/// </para>
/// <para>
/// <b>What it does:</b> Computes the true 2D Euclidean speed vector $\sqrt{dx^2 + dy^2}$ and applies a uniform, isotropic smoothing factor $\alpha$ across both axes.
/// </para>
/// <para>
/// <b>Why it is used:</b> Independent 1D filters adapt cutoff frequencies per-axis, causing anisotropic warping/skewing of facial shapes during diagonal motion.
/// </para>
/// </summary>
public class OneEuroFilter2D
{
    private readonly double _freq;
    private readonly double _minCutoff;
    private readonly double _beta;
    private readonly double _dCutoff;

    private readonly LowPassFilter _x = new();
    private readonly LowPassFilter _y = new();
    private readonly LowPassFilter _dx = new();
    private readonly LowPassFilter _dy = new();

    private double? _lastTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="OneEuroFilter2D"/> class.
    /// </summary>
    /// <param name="freq">Estimated sampling rate in Hz (default: 30.0).</param>
    /// <param name="minCutoff">Minimum cutoff frequency in Hz (default: 1.2).</param>
    /// <param name="beta">Velocity adaptation parameter (default: 0.005).</param>
    /// <param name="dCutoff">Derivative cutoff frequency in Hz (default: 1.0).</param>
    public OneEuroFilter2D(double freq = 30.0, double minCutoff = 1.2, double beta = 0.005, double dCutoff = 1.0)
    {
        _freq = freq;
        _minCutoff = minCutoff;
        _beta = beta;
        _dCutoff = dCutoff;
    }

    /// <summary>
    /// Processes a new raw 2D coordinate with its timestamp and returns the isotropically filtered point.
    /// </summary>
    /// <param name="point">The raw $(X, Y)$ coordinates.</param>
    /// <param name="timestamp">The frame timestamp in seconds.</param>
    /// <returns>The adaptively smoothed 2D point.</returns>
    public Point2f Filter(Point2f point, double timestamp)
    {
        double dt = _lastTime.HasValue ? timestamp - _lastTime.Value : 1.0 / _freq;
        if (dt <= 0) dt = 1.0 / _freq;
        _lastTime = timestamp;

        // 1. Compute raw velocity derivatives for X and Y
        double dValX = _x.HasLastValue ? (point.X - _x.LastValue) / dt : 0.0;
        double dValY = _y.HasLastValue ? (point.Y - _y.LastValue) / dt : 0.0;

        // 2. Smooth velocity derivatives
        double alphaD = Alpha(dt, _dCutoff);
        double edX = _dx.Filter(dValX, alphaD);
        double edY = _dy.Filter(dValY, alphaD);

        // 3. Compute Euclidean magnitude of 2D velocity vector
        double euclideanSpeed = Math.Sqrt(edX * edX + edY * edY);

        // 4. Adapt cutoff frequency isotropically based on 2D motion speed
        double cutoff = _minCutoff + _beta * euclideanSpeed;
        double alpha = Alpha(dt, cutoff);

        // 5. Apply identical alpha smoothing to both X and Y coordinates
        float smoothX = (float)_x.Filter(point.X, alpha);
        float smoothY = (float)_y.Filter(point.Y, alpha);

        return new Point2f(smoothX, smoothY);
    }

    /// <summary>
    /// Resets all internal coordinate and derivative filter memories.
    /// </summary>
    public void Reset()
    {
        _x.Reset();
        _y.Reset();
        _dx.Reset();
        _dy.Reset();
        _lastTime = null;
    }

    private static double Alpha(double dt, double cutoff)
    {
        double tau = 1.0 / (2.0 * Math.PI * cutoff);
        return 1.0 / (1.0 + tau / dt);
    }
}
