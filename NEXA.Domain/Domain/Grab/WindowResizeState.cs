using OpenCvSharp;

namespace NEXA.Domain.Grab;

/// <summary>
/// State container tracking continuous pinch-zoom window resizing metrics, aperture baselines, and smoothed window geometry.
/// <para>
/// <b>What it is:</b> The state model managing stepless two-hand window dimension scaling.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description>Maintains live and baseline thumb-index aperture ratios.</description></item>
/// <item><description>Stores initial unscaled window dimensions captured at pinch start.</description></item>
/// <item><description>Maintains smoothed target width and height values to eliminate resizing jitter.</description></item>
/// <item><description>Tracks 2D fingertip coordinates for AR measurement caliper visualization.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Why it is used:</b> Isolates the multi-frame zoom memory and mathematical state from gesture evaluation and OS invocation.
/// </para>
/// </summary>
public class WindowResizeState
{
    /// <summary>
    /// Gets or sets a value indicating whether a continuous pinch-resize interaction is currently active.
    /// </summary>
    public bool IsActive { get; internal set; } = false;

    /// <summary>
    /// The baseline normalized aperture ratio (Thumb-Index distance / Palm size) locked at the onset of the resize gesture.
    /// </summary>
    public double BaselineRatio { get; internal set; } = 1.0;

    /// <summary>
    /// The live instantaneous aperture ratio evaluated in the current frame.
    /// </summary>
    public double LiveRatio { get; internal set; } = 0.0;

    /// <summary>
    /// The current relative multiplier applied to the base window dimensions (e.g. 1.25 for +25% size).
    /// </summary>
    public double CurrentScale { get; internal set; } = 1.0;

    /// <summary>
    /// The last stable scaling multiplier before gesture engagement or re-anchoring.
    /// </summary>
    public double LastStableScale { get; internal set; } = 1.0;

    /// <summary>
    /// The native window pixel width captured at the start of the grab/resize session.
    /// </summary>
    public int BaseWidth { get; internal set; } = 0;

    /// <summary>
    /// The native window pixel height captured at the start of the grab/resize session.
    /// </summary>
    public int BaseHeight { get; internal set; } = 0;

    /// <summary>
    /// The calculated and clamped target window pixel width for the current frame.
    /// </summary>
    public int CurrentWidth { get; internal set; } = 0;

    /// <summary>
    /// The calculated and clamped target window pixel height for the current frame.
    /// </summary>
    public int CurrentHeight { get; internal set; } = 0;

    /// <summary>
    /// Smoothed continuous accumulator for window width.
    /// </summary>
    public double SmoothedWidth { get; internal set; } = 0.0;

    /// <summary>
    /// Smoothed continuous accumulator for window height.
    /// </summary>
    public double SmoothedHeight { get; internal set; } = 0.0;

    /// <summary>
    /// Flag indicating whether the exponential smoothing accumulators have been initialized with the base window dimensions.
    /// </summary>
    public bool HasInitializedSmoothing { get; internal set; } = false;

    /// <summary>
    /// 2D camera coordinate of the resizing hand's thumb tip (Landmark 4).
    /// </summary>
    public Point2f ThumbTip { get; internal set; }

    /// <summary>
    /// 2D camera coordinate of the resizing hand's index fingertip (Landmark 8).
    /// </summary>
    public Point2f IndexTip { get; internal set; }
}
