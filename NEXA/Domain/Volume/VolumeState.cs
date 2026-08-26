using OpenCvSharp;

namespace NEXA.Domain.Volume;

/// <summary>
/// State container tracking L-gesture engagement, baseline rotary angles, and smoothed volume scalars.
/// <para>
/// <b>What it is:</b> The state model managing continuous hand rotation audio adjustments.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description>Tracks whether the L-gesture rotary gate is currently engaged.</description></item>
/// <item><description>Holds baseline and live rotation angles of the index finger vector.</description></item>
/// <item><description>Maintains initial baseline volume and live target volume percentages.</description></item>
/// <item><description>Stores the 2D dial center and tip coordinates for AR circular dial gauge rendering.</description></item>
/// </list>
/// </para>
/// </summary>
public class VolumeState
{
    /// <summary>
    /// Gets or sets a value indicating whether the L-gesture rotary volume dial is actively engaged.
    /// </summary>
    public bool IsActive { get; set; } = false;

    /// <summary>
    /// The baseline orientation angle in degrees locked at the start of the L-gesture.
    /// </summary>
    public double BaselineAngle { get; set; } = 0.0;

    /// <summary>
    /// The instantaneous orientation angle in degrees in the current frame.
    /// </summary>
    public double LiveAngle { get; set; } = 0.0;

    /// <summary>
    /// The continuous signed angle delta in degrees relative to the baseline orientation.
    /// </summary>
    public double AngleDelta { get; set; } = 0.0;

    /// <summary>
    /// The master audio volume level [0.0 to 1.0] captured at the start of the dial interaction.
    /// </summary>
    public float BaselineVolume { get; set; } = 0.5f;

    /// <summary>
    /// The calculated continuous master audio volume level [0.0 to 1.0] for the current frame.
    /// </summary>
    public float TargetVolume { get; set; } = 0.5f;

    /// <summary>
    /// The exponentially smoothed volume level for jitter-free audio slider adjustment.
    /// </summary>
    public float SmoothedVolume { get; set; } = 0.5f;

    /// <summary>
    /// 2D camera coordinate of the dial center (Middle finger MCP / Palm center [9]).
    /// </summary>
    public Point2f DialCenter { get; set; }

    /// <summary>
    /// 2D camera coordinate of the index fingertip [8].
    /// </summary>
    public Point2f IndexTip { get; set; }

    /// <summary>
    /// 2D camera coordinate of the thumb tip [4].
    /// </summary>
    public Point2f ThumbTip { get; set; }
}
