using System;
using OpenCvSharp;

namespace NEXA.Face;

/// <summary>
/// Domain data transfer model representing a detected human face with all 68 Dlib facial landmark contour points, spatial bounding box, and tracked mouth region.
/// <para>
/// <b>What it is:</b> The 68-point facial telemetry model powering Dlib face tracking and the "Shhh" mute gesture.
/// </para>
/// <para>
/// <b>Landmark Indices (Dlib 68 Standard):</b>
/// <list type="bullet">
/// <item><description>Jawline: Points [0..16]</description></item>
/// <item><description>Right Eyebrow: Points [17..21]</description></item>
/// <item><description>Left Eyebrow: Points [22..26]</description></item>
/// <item><description>Nose Bridge &amp; Tip: Points [27..35]</description></item>
/// <item><description>Right Eye: Points [36..41]</description></item>
/// <item><description>Left Eye: Points [42..47]</description></item>
/// <item><description>Outer Lips: Points [48..59]</description></item>
/// <item><description>Inner Lips: Points [60..67]</description></item>
/// </list>
/// </para>
/// </summary>
public class TrackedFace
{
    /// <summary>
    /// The 2D bounding box surrounding the face in camera pixel coordinates.
    /// </summary>
    public Rect2f BoundingBox { get; set; }

    /// <summary>
    /// All 68 discrete facial landmark contour coordinates in camera pixel space.
    /// </summary>
    public Point2f[] Landmarks68 { get; set; } = new Point2f[68];

    /// <summary>
    /// The exact center point of the mouth lips in camera pixel coordinates.
    /// </summary>
    public Point2f MouthCenter { get; set; }

    /// <summary>
    /// Proximity radius around the mouth center for the "Shhh" gesture.
    /// </summary>
    public float MouthRadius { get; set; }

    /// <summary>
    /// Estimated position of the left eye.
    /// </summary>
    public Point2f LeftEye { get; set; }

    /// <summary>
    /// Estimated position of the right eye.
    /// </summary>
    public Point2f RightEye { get; set; }

    /// <summary>
    /// Estimated position of the nose tip (Landmark 30).
    /// </summary>
    public Point2f NoseTip { get; set; }

    /// <summary>
    /// Detection confidence score [0.0 to 1.0].
    /// </summary>
    public float Confidence { get; set; } = 1.0f;
}
