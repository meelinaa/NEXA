using System;
using OpenCvSharp;

namespace NEXA.Face;

/// <summary>
/// Augmented-reality visualizer rendering all 68 Dlib facial landmark tracking points, contour meshes, and mouth targets onto the camera frame.
/// <para>
/// <b>What it is:</b> The visual telemetry renderer for real-time 68-point face mesh tracking.
/// </para>
/// </summary>
public class FaceMeshRenderer
{
    /// <summary>
    /// Gets or sets a value indicating whether to render the 68 landmark dots.
    /// </summary>
    public bool ShowPoints { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to render facial feature contour lines.
    /// </summary>
    public bool ShowContours { get; set; } = true;

    /// <summary>
    /// Renders the 68-point facial mesh, contour lines, and bounding box onto the camera frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="face">The detected face instance.</param>
    public void Render(Mat frame, TrackedFace? face)
    {
        if (face == null || frame == null || frame.Empty())
            return;

        Scalar meshColor = new(0, 220, 255); // Vibrant Cyan
        Scalar pointColor = new(0, 255, 120); // Neon Lime Green
        Scalar lipColor = new(0, 100, 255); // Orange/Red for Lips

        // 1. Render Facial Contour PolyLines
        if (ShowContours && face.Landmarks68 != null && face.Landmarks68.Length >= 68)
        {
            // Jawline [0..16]
            DrawContour(frame, face.Landmarks68, 0, 16, false, meshColor);

            // Right Eyebrow [17..21]
            DrawContour(frame, face.Landmarks68, 17, 21, false, meshColor);

            // Left Eyebrow [22..26]
            DrawContour(frame, face.Landmarks68, 22, 26, false, meshColor);

            // Nose Bridge [27..30] & Base [31..35]
            DrawContour(frame, face.Landmarks68, 27, 30, false, meshColor);
            DrawContour(frame, face.Landmarks68, 31, 35, true, meshColor);

            // Right Eye [36..41]
            DrawContour(frame, face.Landmarks68, 36, 41, true, meshColor);

            // Left Eye [42..47]
            DrawContour(frame, face.Landmarks68, 42, 47, true, meshColor);

            // Outer Lips [48..59]
            DrawContour(frame, face.Landmarks68, 48, 59, true, lipColor);

            // Inner Lips [60..67]
            DrawContour(frame, face.Landmarks68, 60, 67, true, lipColor);
        }

        // 2. Render Discrete Landmark Dots
        if (ShowPoints && face.Landmarks68 != null)
        {
            for (int i = 0; i < face.Landmarks68.Length; i++)
            {
                Point pt = new((int)Math.Round(face.Landmarks68[i].X), (int)Math.Round(face.Landmarks68[i].Y));
                if (pt.X > 0 && pt.Y > 0)
                {
                    Scalar dotColor = (i >= 48 && i <= 67) ? lipColor : pointColor;
                    Cv2.Circle(frame, pt, 2, dotColor, -1, LineTypes.AntiAlias);
                }
            }
        }
    }

    private static void DrawContour(Mat frame, Point2f[] pts, int startIdx, int endIdx, bool isClosed, Scalar color)
    {
        for (int i = startIdx; i < endIdx; i++)
        {
            Point p1 = new((int)Math.Round(pts[i].X), (int)Math.Round(pts[i].Y));
            Point p2 = new((int)Math.Round(pts[i + 1].X), (int)Math.Round(pts[i + 1].Y));
            if (p1.X > 0 && p1.Y > 0 && p2.X > 0 && p2.Y > 0)
            {
                Cv2.Line(frame, p1, p2, color, 1, LineTypes.AntiAlias);
            }
        }

        if (isClosed)
        {
            Point pFirst = new((int)Math.Round(pts[startIdx].X), (int)Math.Round(pts[startIdx].Y));
            Point pLast = new((int)Math.Round(pts[endIdx].X), (int)Math.Round(pts[endIdx].Y));
            if (pFirst.X > 0 && pFirst.Y > 0 && pLast.X > 0 && pLast.Y > 0)
            {
                Cv2.Line(frame, pLast, pFirst, color, 1, LineTypes.AntiAlias);
            }
        }
    }
}
