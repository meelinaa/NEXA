namespace NEXA.Configuration;

/// <summary>
/// Domain gesture recognition thresholds and timing parameters.
/// </summary>
public class GestureOptions
{
    /// <summary>
    /// Duration in milliseconds the hand must hover within radius to trigger a dwell click. Default is 850ms.
    /// </summary>
    public int DwellClickMilliseconds { get; set; } = 850;

    /// <summary>
    /// Maximum allowed displacement jitter in pixels during dwell countdown. Default is 18px.
    /// </summary>
    public float DwellRadiusPixels { get; set; } = 18f;

    /// <summary>
    /// Deadzone threshold percentage (+/-) for pinch-to-resize stability. Default is 0.035 (3.5%).
    /// </summary>
    public double ResizeDeadzonePercent { get; set; } = 0.035;

    /// <summary>
    /// Percentage comfort margins applied to frame boundaries for screen coordinate projection. Default is 0.15 (15%).
    /// </summary>
    public float ScreenComfortMarginPercent { get; set; } = 0.15f;

    /// <summary>
    /// Wrist tilt angle in degrees required to trigger Undo / Redo actions. Default is 42.0 degrees.
    /// </summary>
    public double UndoRotationThresholdDegrees { get; set; } = 42.0;
}
