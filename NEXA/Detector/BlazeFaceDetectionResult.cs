using System;
using OpenCvSharp;

namespace NEXA.Detector;

/// <summary>
/// Domain data transfer model representing the output of the Stage 1 BlazeFace object detector.
/// <para>
/// <b>What it is:</b> Calibrated bounding box, confidence score, and 6 facial keypoints extracted from the camera frame.
/// </para>
/// </summary>
public class BlazeFaceDetectionResult
{
    /// <summary>
    /// The calibrated 2D bounding box surrounding the face in full camera pixel coordinates.
    /// </summary>
    public Rect2d Box { get; set; }

    /// <summary>
    /// Detection confidence score in range [0.0 to 1.0].
    /// </summary>
    public float Score { get; set; }

    /// <summary>
    /// Six facial anchor keypoints in camera pixel space:
    /// [0] Right Eye, [1] Left Eye, [2] Nose Tip, [3] Mouth Center, [4] Right Ear Tragion, [5] Left Ear Tragion.
    /// </summary>
    public Point2f[] Keypoints { get; set; } = new Point2f[6];
}
