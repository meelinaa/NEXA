namespace NEXA.Filter;

/// <summary>
/// First-order Infinite Impulse Response (IIR) Exponential Smoothing Low-Pass Filter.
/// <para>
/// <b>What it is:</b> A single-pole recursive filter used to attenuate high-frequency noise from continuous signal streams.
/// </para>
/// <para>
/// <b>What it does:</b> Computes a weighted moving average between the current input value and the previous filtered value using a smoothing factor <c>alpha</c>:
/// <c>Output = alpha * Input + (1 - alpha) * PreviousOutput</c>.
/// </para>
/// <para>
/// <b>Why it is used:</b> Serves as the core building block for the <see cref="OneEuroFilter"/> to smooth both raw landmark positions and their derivative velocities.
/// </para>
/// <para>
/// <b>Consequence:</b> Removes sensor noise and micro-jitter at the cost of a controlled phase lag.
/// </para>
/// </summary>
public class LowPassFilter
{
    /// <summary>
    /// The most recently computed filtered output value.
    /// </summary>
    public double LastValue { get; private set; }

    /// <summary>
    /// Indicates whether at least one valid measurement has been processed since initialization or reset.
    /// </summary>
    public bool HasLastValue { get; private set; }

    /// <summary>
    /// Filters an incoming raw numerical measurement using the specified smoothing factor.
    /// </summary>
    /// <param name="value">The raw input measurement.</param>
    /// <param name="alpha">
    /// Smoothing weight factor in the range [0.0, 1.0]:
    /// <list type="bullet">
    /// <item><description>Values near 1.0 give priority to new raw measurements (low latency, high responsiveness, less smoothing).</description></item>
    /// <item><description>Values near 0.0 give priority to historical values (heavy smoothing, high latency reduction of noise).</description></item>
    /// </list>
    /// </param>
    /// <returns>The smoothed filtered value.</returns>
    public double Filter(double value, double alpha)
    {
        // On the first frame, initialize the filter directly with the raw measurement to avoid lag-in artifacts
        double result = HasLastValue ? alpha * value + (1.0 - alpha) * LastValue : value;
        LastValue = result;
        HasLastValue = true;
        return result;
    }

    /// <summary>
    /// Resets the internal filter state, clearing historical values.
    /// Call when hand tracking is lost to prevent interpolation jumps when a new hand enters the frame.
    /// </summary>
    public void Reset()
    {
        HasLastValue = false;
        LastValue = 0;
    }
}
