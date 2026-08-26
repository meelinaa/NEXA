namespace NEXA.Object;

/// <summary>
/// State model for relative optical zoom scaling controlled via continuous hand gestures (Pinch Closed to L-Sign).
/// <para>
/// <b>What it is:</b> The state machine tracking baseline geometric aperture ratios and persistent zoom scale factors.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description>Captures the initial thumb-to-index aperture ratio when entering zoom mode.</description></item>
/// <item><description>Computes relative continuous magnification multipliers as the hand transitions dynamically between closed pinch and wide L.</description></item>
/// <item><description>Persists the last stable zoom level upon gesture release.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Why it is used:</b> Enables seamless relative zoom in/out interactions without jumpy resets.
/// </para>
/// <para>
/// <b>Consequence:</b> Provides a fluid and predictable magnification control interface.
/// </para>
/// </summary>
public class ZoomState
{
    /// <summary>
    /// Gets or sets a value indicating whether zoom scaling is actively being controlled.
    /// </summary>
    public bool Active { get; set; } = false;

    /// <summary>
    /// The normalized aperture ratio (ThumbIndexDistance / PalmSize) captured at the initiation of the zoom gesture.
    /// </summary>
    public double BaselineRatio { get; set; } = 1.0;

    /// <summary>
    /// The live dynamic zoom factor (e.g. 1.0 = 100% scale, 2.0 = 200% scale) clamped between 0.3x and 3.5x.
    /// </summary>
    public double CurrentZoom { get; set; } = 1.0;

    /// <summary>
    /// The persistent zoom level stored when the zoom gesture is released, used as the starting point for subsequent zoom cycles.
    /// </summary>
    public double LastStableZoom { get; set; } = 1.0;

    /// <summary>
    /// The instantaneous thumb-to-index aperture ratio measured in the current frame.
    /// </summary>
    public double LiveRatio { get; set; } = 0.0;
}
