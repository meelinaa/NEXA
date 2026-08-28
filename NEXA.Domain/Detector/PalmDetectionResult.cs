using OpenCvSharp;

namespace NEXA.Detector;

/// <summary>
/// Data structure representing the output of the Stage 1 Palm Detection model.
/// <para>
/// <b>What it is:</b> A localized detection candidate for a human palm within a camera frame.
/// </para>
/// <para>
/// <b>What it does:</b> Encapsulates the 2D bounding box, the 7 foundational anatomical palm keypoints,
/// and the classification confidence score calculated by <see cref="PalmDetector"/>.
/// </para>
/// <para>
/// <b>Why it is used:</b> Provides the geometric boundary and orientation angle required by the Stage 2
/// <c>HandLandmarkEstimator</c> to crop and align the hand region of interest (ROI) before predicting 21 3D joint landmarks.
/// </para>
/// <para>
/// <b>Consequence:</b> Enables robust hand tracking by isolating the hand region from background clutter.
/// </para>
/// </summary>
public class PalmDetectionResult
{
    /// <summary>
    /// The rectangular bounding box enclosing the palm in original frame pixel coordinates (X, Y, Width, Height).
    /// </summary>
    public Rect2f Box { get; set; }

    /// <summary>
    /// The 7 foundational palm keypoints in pixel coordinates:
    /// <list type="number">
    /// <item><description>Wrist center</description></item>
    /// <item><description>Index finger MCP knuckle</description></item>
    /// <item><description>Middle finger MCP knuckle</description></item>
    /// <item><description>Ring finger MCP knuckle</description></item>
    /// <item><description>Pinky finger MCP knuckle</description></item>
    /// <item><description>Thumb MCP knuckle</description></item>
    /// <item><description>Thumb IP joint</description></item>
    /// </list>
    /// Used to calculate hand orientation angle (rotation) and palm scale.
    /// </summary>
    public Point2f[] Keypoints { get; set; } = new Point2f[7];

    /// <summary>
    /// The Sigmoid confidence probability score (ranging from 0.0f to 1.0f) representing the model's certainty that a palm is present.
    /// </summary>
    public float Score { get; set; }
}
