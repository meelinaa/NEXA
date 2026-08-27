using System;
using OpenCvSharp;

namespace NEXA.Face;

/// <summary>
/// Domain data transfer model representing a detected human face with all 468 MediaPipe facial landmark contour points, spatial bounding box, and tracked mouth region.
/// <para>
/// <b>What it is:</b> The 468-point facial telemetry model powering MediaPipe FaceMesh tracking and the "Shhh" mute gesture.
/// </para>
/// </summary>
public class TrackedFace
{
    /// <summary>
    /// The 2D bounding box surrounding the face in camera pixel coordinates.
    /// </summary>
    public Rect2f BoundingBox { get; set; }

    /// <summary>
    /// All 468 discrete 2D facial landmark contour coordinates in camera pixel space.
    /// </summary>
    public Point2f[] Landmarks { get; set; } = new Point2f[468];

    /// <summary>
    /// Compatibility alias referencing facial landmarks.
    /// </summary>
    public Point2f[] Landmarks68 => Landmarks;

    /// <summary>
    /// The exact center point of the mouth lips in camera pixel coordinates.
    /// </summary>
    public Point2f MouthCenter { get; set; }

    /// <summary>
    /// Proximity radius around the mouth center for the "Shhh" gesture.
    /// </summary>
    public float MouthRadius { get; set; }

    /// <summary>
    /// Estimated position of the left eye pupil / center (Landmark 386).
    /// </summary>
    public Point2f LeftEye { get; set; }

    /// <summary>
    /// Estimated position of the right eye pupil / center (Landmark 159).
    /// </summary>
    public Point2f RightEye { get; set; }

    /// <summary>
    /// Estimated position of the nose tip (Landmark 1).
    /// </summary>
    public Point2f NoseTip { get; set; }

    /// <summary>
    /// Detection confidence score [0.0 to 1.0].
    /// </summary>
    public float Confidence { get; set; } = 1.0f;
}
