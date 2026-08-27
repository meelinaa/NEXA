namespace NEXA.Configuration;

/// <summary>
/// Mathematical signal smoothing and 1€-filter configuration options.
/// </summary>
public class FilterOptions
{
    /// <summary>
    /// Minimum cutoff frequency in Hz for jitter suppression when motionless. Default is 1.2 Hz.
    /// </summary>
    public double MinCutoffFrequency { get; set; } = 1.2;

    /// <summary>
    /// Speed adaptation coefficient beta for lag-free rapid motion tracking. Default is 0.05.
    /// </summary>
    public double BetaSpeedCoefficient { get; set; } = 0.05;
}
