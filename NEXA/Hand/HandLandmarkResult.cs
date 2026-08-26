using OpenCvSharp;

namespace NEXA.Hand;

/// <summary>
/// Data container holding raw inference results from the Stage 2 Hand Landmark Estimator model.
/// <para>
/// <b>What it is:</b> The direct output of <see cref="HandLandmarkEstimator"/> containing all 21 localized finger joints in 2D and 3D space.
/// </para>
/// <para>
/// <b>What it does:</b> Stores camera-space pixel coordinates, metric 3D coordinates, bounding box regions,
/// hand presence confidence, and left/right handedness classification.
/// </para>
/// <para>
/// <b>Why it is used:</b> Acts as the standardized raw input to the temporal OneEuroFilter and gesture classification layers.
/// </para>
/// <para>
/// <b>Consequence:</b> Provides the complete anatomical model of the hand for rendering and gesture analysis.
/// </para>
/// </summary>
public class HandLandmarkResult
{
    /// <summary>
    /// The 21 hand landmarks in 3D camera coordinates:
    /// <list type="bullet">
    /// <item><description>X, Y: Normalized coordinates mapped back to original image pixel space.</description></item>
    /// <item><description>Z: Depth relative to the wrist (in normalized image units; negative values mean closer to camera).</description></item>
    /// </list>
    /// </summary>
    public Point3f[] Landmarks { get; set; } = new Point3f[21];

    /// <summary>
    /// The 21 hand landmarks projected onto the 2D image plane in camera frame pixel coordinates (X, Y).
    /// </summary>
    public Point2f[] Landmarks2D { get; set; } = new Point2f[21];

    /// <summary>
    /// The 21 hand landmarks in 3D metric world coordinates (in meters) with origin centered at the hand's geometric center.
    /// Useful for scale-independent real-world spatial measurements.
    /// </summary>
    public Point3f[] WorldLandmarks { get; set; } = new Point3f[21];

    /// <summary>
    /// The tight rectangular bounding box tightly enclosing all 21 finger landmarks in pixel coordinates.
    /// </summary>
    public Rect2f BoundingBox { get; set; }

    /// <summary>
    /// The hand presence confidence probability score (0.0f to 1.0f).
    /// Low confidence indicates the cropped area does not contain a valid hand.
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// Probability score for handedness (0.0f = definitely Left Hand, 1.0f = definitely Right Hand).
    /// </summary>
    public float HandednessScore { get; set; }

    /// <summary>
    /// Handedness classification string ("Right" or "Left") derived from <see cref="HandednessScore"/>.
    /// </summary>
    public string Handedness => HandednessScore > 0.5f ? "Right" : "Left";
}
