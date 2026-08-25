using OpenCvSharp;

namespace NEXA.Hand;

public class HandLandmarkResult
{
    public Point3f[] Landmarks { get; set; } = new Point3f[21];
    public Point2f[] Landmarks2D { get; set; } = new Point2f[21];
    public Point3f[] WorldLandmarks { get; set; } = new Point3f[21];
    public Rect2f BoundingBox { get; set; }
    public float Confidence { get; set; }
    public float HandednessScore { get; set; }
    public string Handedness => HandednessScore > 0.5f ? "Right" : "Left";
}
