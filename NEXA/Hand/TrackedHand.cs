using OpenCvSharp;

namespace NEXA.Hand;

public class TrackedHand
{
    public HandLandmarkResult RawResult { get; set; } = new();
    public Point2f[] SmoothedLandmarks2D { get; set; } = new Point2f[21];
    public Point3f[] SmoothedLandmarks3D { get; set; } = new Point3f[21];
    public string Gesture { get; set; } = "Unknown";
    public string Handedness => RawResult.Handedness;
    public float Confidence => RawResult.Confidence;
    public Rect2f BoundingBox => RawResult.BoundingBox;
    public double Distance(int idx1, int idx2)
    {
        var p1 = SmoothedLandmarks2D[idx1];
        var p2 = SmoothedLandmarks2D[idx2];
        float dx = p1.X - p2.X;
        float dy = p1.Y - p2.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}