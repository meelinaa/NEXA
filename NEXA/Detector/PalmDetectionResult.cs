using OpenCvSharp;

namespace NEXA.Detector;

public class PalmDetectionResult
{
    public Rect2f Box { get; set; }
    public Point2f[] Keypoints { get; set; } = new Point2f[7];
    public float Score { get; set; }
}
