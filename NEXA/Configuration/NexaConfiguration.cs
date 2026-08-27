namespace NEXA.Configuration;

/// <summary>
/// Root application configuration container binding all subsystem settings from appsettings.json.
/// </summary>
public class NexaConfiguration
{
    /// <summary>
    /// Configuration section key name in appsettings.json.
    /// </summary>
    public const string SectionName = "Nexa";

    /// <summary>
    /// Camera capture settings.
    /// </summary>
    public CameraOptions Camera { get; set; } = new();

    /// <summary>
    /// Machine learning tracking and model settings.
    /// </summary>
    public TrackingOptions Tracking { get; set; } = new();

    /// <summary>
    /// Domain gesture recognition settings.
    /// </summary>
    public GestureOptions Gestures { get; set; } = new();

    /// <summary>
    /// 1€-filter signal smoothing settings.
    /// </summary>
    public FilterOptions Filtering { get; set; } = new();
}
