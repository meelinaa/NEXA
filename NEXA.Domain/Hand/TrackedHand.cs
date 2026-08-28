using OpenCvSharp;

namespace NEXA.Hand;

/// <summary>
/// High-level domain entity representing an actively tracked hand instance.
/// <para>
/// <b>What it is:</b> The fully processed state of a single human hand within a frame.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description>Holds both raw model landmark estimations and OneEuroFilter-smoothed 2D/3D coordinates.</description></item>
/// <item><description>Stores the classified hand gesture name (e.g., "Pointing", "Hand Up", "Fist").</description></item>
/// <item><description>Provides geometric helper functions for Euclidean distance calculations between joints.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Why it is used:</b> Serves as the single source of truth for all downstream controllers (mouse, scroll, virtual object).
/// </para>
/// <para>
/// <b>Consequence:</b> Controllers interact with clean, filtered hand data rather than raw model outputs.
/// </para>
/// </summary>
public class TrackedHand
{
    /// <summary>
    /// The raw inference results produced directly by the ONNX <see cref="HandLandmarkEstimator"/>.
    /// </summary>
    public HandLandmarkResult RawResult { get; set; } = new();

    /// <summary>
    /// The 21 hand landmarks smoothed by <see cref="NEXA.Filter.OneEuroFilter"/> in 2D frame pixel coordinates.
    /// </summary>
    public Point2f[] SmoothedLandmarks2D { get; set; } = new Point2f[21];

    /// <summary>
    /// The 21 hand landmarks smoothed by <see cref="NEXA.Filter.OneEuroFilter"/> in 3D space (X, Y in pixel space, Z in depth units).
    /// </summary>
    public Point3f[] SmoothedLandmarks3D { get; set; } = new Point3f[21];

    /// <summary>
    /// The recognized hand gesture classification (e.g., "Pointing", "Hand Up", "Hand Down", "Fist", "Spock", "Pinch", "L", "Peace").
    /// </summary>
    public string Gesture { get; set; } = "Unknown";

    /// <summary>
    /// Handedness classification string ("Right" or "Left") forwarded from <see cref="RawResult"/>.
    /// </summary>
    public string Handedness => RawResult.Handedness;

    /// <summary>
    /// Hand presence detection confidence score (0.0f to 1.0f).
    /// </summary>
    public float Confidence => RawResult.Confidence;

    /// <summary>
    /// The visual bounding box enclosing the hand.
    /// </summary>
    public Rect2f BoundingBox => RawResult.BoundingBox;

    /// <summary>
    /// Computes the 2D Euclidean pixel distance between two smoothed landmark joints.
    /// </summary>
    /// <param name="idx1">Index of the first landmark (0 to 20).</param>
    /// <param name="idx2">Index of the second landmark (0 to 20).</param>
    /// <returns>The straight-line distance in pixels: <c>sqrt((x1-x2)^2 + (y1-y2)^2)</c>.</returns>
    public double Distance(int idx1, int idx2)
    {
        Point2f p1 = SmoothedLandmarks2D[idx1];
        Point2f p2 = SmoothedLandmarks2D[idx2];
        float dx = p1.X - p2.X;
        float dy = p1.Y - p2.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}