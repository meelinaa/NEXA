using System;

namespace NEXA.Domain.Grab;

/// <summary>
/// Motion smoothing filter for dragged window coordinates applying exponential smoothing and deadzone filtering.
/// <para>
/// <b>What it is:</b> Low-pass spatial filter eliminating hand jitter during desktop window translation.
/// </para>
/// </summary>
public class WindowGrabSmoother
{
    private double _smoothedTargetX = 0;
    private double _smoothedTargetY = 0;
    private bool _hasInitialized = false;

    /// <summary>
    /// Gets the current smoothed horizontal target coordinate in pixels.
    /// </summary>
    public int SmoothedX => (int)Math.Round(_smoothedTargetX);

    /// <summary>
    /// Gets the current smoothed vertical target coordinate in pixels.
    /// </summary>
    public int SmoothedY => (int)Math.Round(_smoothedTargetY);

    /// <summary>
    /// Re-anchors or initializes the internal smoothing filter coordinates immediately without transition lag.
    /// </summary>
    /// <param name="x">Horizontal coordinate in pixels.</param>
    /// <param name="y">Vertical coordinate in pixels.</param>
    public void SetPosition(double x, double y)
    {
        _smoothedTargetX = x;
        _smoothedTargetY = y;
        _hasInitialized = true;
    }

    /// <summary>
    /// Applies velocity-adaptive exponential smoothing and deadzone filtering to raw target coordinates.
    /// </summary>
    /// <param name="rawTargetX">Raw computed X coordinate.</param>
    /// <param name="rawTargetY">Raw computed Y coordinate.</param>
    /// <returns>A tuple with (smoothedX, smoothedY).</returns>
    public (int smoothedX, int smoothedY) Smooth(double rawTargetX, double rawTargetY)
    {
        if (!_hasInitialized)
        {
            _smoothedTargetX = rawTargetX;
            _smoothedTargetY = rawTargetY;
            _hasInitialized = true;
        }
        else
        {
            double diffX = rawTargetX - _smoothedTargetX;
            double diffY = rawTargetY - _smoothedTargetY;
            double dist = Math.Sqrt(diffX * diffX + diffY * diffY);

            // Deadzone: ignore micro-tremors smaller than 3.0 pixels
            if (dist > 3.0)
            {
                // Dynamic alpha: 0.20 (heavy smoothing during slow positioning) up to 0.80 (rapid sweeping)
                double alpha = Math.Clamp(0.20 + (dist / 180.0) * 0.55, 0.20, 0.80);
                _smoothedTargetX += diffX * alpha;
                _smoothedTargetY += diffY * alpha;
            }
        }

        return ((int)Math.Round(_smoothedTargetX), (int)Math.Round(_smoothedTargetY));
    }

    /// <summary>
    /// Resets the internal smoothing accumulator.
    /// </summary>
    public void Reset()
    {
        _smoothedTargetX = 0;
        _smoothedTargetY = 0;
        _hasInitialized = false;
    }
}
