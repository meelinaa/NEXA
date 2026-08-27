using System;
using NEXA.Common;
using OpenCvSharp;

namespace NEXA.Face;

/// <summary>
/// Visualizer rendering a clean head bounding box around the detected face and an isolated 468-point MediaPipe FaceMesh telemetry card in the bottom-left corner of the HUD.
/// <para>
/// <b>What it is:</b> The visual telemetry renderer for 468-point FaceMesh inspection.
/// </para>
/// </summary>
public class FaceMeshRenderer
{
    /// <summary>
    /// Gets or sets a value indicating whether to show the face bounding box around the head.
    /// </summary>
    public bool ShowHeadBoundingBox { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to show the bottom-left face mesh PIP widget.
    /// </summary>
    public bool ShowMeshWidget { get; set; } = true;

    private static readonly int[] FaceOval = [10, 338, 297, 332, 284, 251, 389, 356, 454, 323, 361, 288, 397, 365, 379, 378, 400, 377, 152, 148, 176, 149, 150, 136, 172, 58, 132, 93, 234, 127, 162, 21, 54, 103, 67, 109, 10];
    private static readonly int[] RightEyebrow = [70, 63, 105, 66, 107, 55, 65, 52, 53, 46];
    private static readonly int[] LeftEyebrow = [300, 293, 334, 296, 336, 285, 295, 282, 283, 276];
    private static readonly int[] NoseBridge = [168, 6, 197, 195, 5, 4, 1, 19, 94, 2];
    private static readonly int[] RightEye = [33, 7, 163, 144, 145, 153, 154, 155, 133, 173, 157, 158, 159, 160, 161, 246];
    private static readonly int[] LeftEye = [362, 382, 381, 380, 374, 373, 390, 249, 263, 466, 388, 387, 386, 385, 384, 398];
    private static readonly int[] OuterLips = [61, 185, 40, 39, 37, 0, 267, 269, 270, 409, 291, 375, 321, 405, 314, 17, 84, 181, 91, 146];
    private static readonly int[] InnerLips = [78, 191, 80, 81, 82, 13, 312, 311, 310, 415, 308, 324, 318, 402, 317, 14, 87, 178, 88, 95];

    /// <summary>
    /// Renders the clean head bounding box on the camera view and the 468-point face mesh telemetry in a dedicated bottom-left box.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="face">The detected face instance.</param>
    public void Render(Mat frame, TrackedFace? face)
    {
        if (face == null || frame == null || frame.Empty())
            return;

        // 1. Render Clean Bounding Box Around the Head
        if (ShowHeadBoundingBox)
        {
            Rect headRect = new(
                (int)Math.Round(face.BoundingBox.X),
                (int)Math.Round(face.BoundingBox.Y),
                (int)Math.Round(face.BoundingBox.Width),
                (int)Math.Round(face.BoundingBox.Height)
            );

            Scalar headColor = new(0, 220, 255); // Cyan
            Cv2.Rectangle(frame, headRect, headColor, 1, LineTypes.AntiAlias);

            int cornerLen = Math.Min(20, (int)(headRect.Width * 0.15));
            // Top-Left corner bracket
            Cv2.Line(frame, new Point(headRect.X, headRect.Y), new Point(headRect.X + cornerLen, headRect.Y), headColor, 2, LineTypes.AntiAlias);
            Cv2.Line(frame, new Point(headRect.X, headRect.Y), new Point(headRect.X, headRect.Y + cornerLen), headColor, 2, LineTypes.AntiAlias);
            // Top-Right corner bracket
            Cv2.Line(frame, new Point(headRect.Right, headRect.Y), new Point(headRect.Right - cornerLen, headRect.Y), headColor, 2, LineTypes.AntiAlias);
            Cv2.Line(frame, new Point(headRect.Right, headRect.Y), new Point(headRect.Right, headRect.Y + cornerLen), headColor, 2, LineTypes.AntiAlias);
            // Bottom-Left corner bracket
            Cv2.Line(frame, new Point(headRect.X, headRect.Bottom), new Point(headRect.X + cornerLen, headRect.Bottom), headColor, 2, LineTypes.AntiAlias);
            Cv2.Line(frame, new Point(headRect.X, headRect.Bottom), new Point(headRect.X, headRect.Bottom - cornerLen), headColor, 2, LineTypes.AntiAlias);
            // Bottom-Right corner bracket
            Cv2.Line(frame, new Point(headRect.Right, headRect.Bottom), new Point(headRect.Right - cornerLen, headRect.Bottom), headColor, 2, LineTypes.AntiAlias);
            Cv2.Line(frame, new Point(headRect.Right, headRect.Bottom), new Point(headRect.Right, headRect.Bottom - cornerLen), headColor, 2, LineTypes.AntiAlias);

            Cv2.PutText(frame, TextSanitizer.ToSafeAscii("[FACE ONNX 468]"), new Point(headRect.X, Math.Max(20, headRect.Y - 6)),
                HersheyFonts.HersheySimplex, 0.40, headColor, 1, LineTypes.AntiAlias);
        }

        // 2. Render Isolated 468-Point Face Mesh in Bottom-Left PIP Box
        if (ShowMeshWidget && face.Landmarks != null && face.Landmarks.Length >= 468)
        {
            int pipW = 150;
            int pipH = 175;
            int pipX = 10;
            int pipY = frame.Height - pipH - 10;
            Rect pipRect = new(pipX, pipY, pipW, pipH);

            // Semi-transparent background card
            using Mat overlay = frame.Clone();
            Cv2.Rectangle(overlay, pipRect, new Scalar(10, 12, 18), -1);
            Cv2.AddWeighted(overlay, 0.75, frame, 0.25, 0, frame);
            Cv2.Rectangle(frame, pipRect, new Scalar(0, 220, 255), 1, LineTypes.AntiAlias);

            // Title tag
            Cv2.PutText(frame, TextSanitizer.ToSafeAscii("FACE MESH 468"), new Point(pipX + 22, pipY + 16),
                HersheyFonts.HersheySimplex, 0.36, new Scalar(0, 255, 255), 1, LineTypes.AntiAlias);

            // Find min/max bounds of raw landmarks to normalize them inside the PIP card
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int i = 0; i < 468; i++)
            {
                Point2f pt = face.Landmarks[i];
                if (pt.X < minX) minX = pt.X;
                if (pt.X > maxX) maxX = pt.X;
                if (pt.Y < minY) minY = pt.Y;
                if (pt.Y > maxY) maxY = pt.Y;
            }

            float rangeX = Math.Max(1f, maxX - minX);
            float rangeY = Math.Max(1f, maxY - minY);

            // Target dimensions inside PIP
            float targetW = pipW - 24;
            float targetH = pipH - 34;
            float scale = Math.Min(targetW / rangeX, targetH / rangeY);

            float offsetX = pipX + 12 + (targetW - rangeX * scale) / 2f;
            float offsetY = pipY + 24 + (targetH - rangeY * scale) / 2f;

            Point2f[] pipPts = new Point2f[468];
            for (int i = 0; i < 468; i++)
            {
                pipPts[i] = new Point2f(
                    offsetX + (face.Landmarks[i].X - minX) * scale,
                    offsetY + (face.Landmarks[i].Y - minY) * scale
                );
            }

            Scalar meshColor = new(0, 220, 255); // Cyan
            Scalar pointColor = new(0, 255, 120); // Green
            Scalar lipColor = new(0, 100, 255); // Orange

            // Draw Contour PolyLines in PIP
            DrawIndexLoop(frame, pipPts, FaceOval, true, meshColor);
            DrawIndexLoop(frame, pipPts, RightEyebrow, false, meshColor);
            DrawIndexLoop(frame, pipPts, LeftEyebrow, false, meshColor);
            DrawIndexLoop(frame, pipPts, NoseBridge, false, meshColor);
            DrawIndexLoop(frame, pipPts, RightEye, true, meshColor);
            DrawIndexLoop(frame, pipPts, LeftEye, true, meshColor);
            DrawIndexLoop(frame, pipPts, OuterLips, true, lipColor);
            DrawIndexLoop(frame, pipPts, InnerLips, true, lipColor);

            // Draw Dots in PIP
            for (int i = 0; i < 468; i++)
            {
                Point pt = new((int)Math.Round(pipPts[i].X), (int)Math.Round(pipPts[i].Y));
                Scalar dotColor = (i is 0 or 13 or 14 or 17 or 61 or 291) ? lipColor : pointColor;
                Cv2.Circle(frame, pt, 1, dotColor, -1, LineTypes.AntiAlias);
            }
        }
    }

    private static void DrawIndexLoop(Mat frame, Point2f[] pts, int[] indices, bool isClosed, Scalar color)
    {
        for (int i = 0; i < indices.Length - 1; i++)
        {
            int idx1 = indices[i];
            int idx2 = indices[i + 1];
            if (idx1 < pts.Length && idx2 < pts.Length)
            {
                Point p1 = new((int)Math.Round(pts[idx1].X), (int)Math.Round(pts[idx1].Y));
                Point p2 = new((int)Math.Round(pts[idx2].X), (int)Math.Round(pts[idx2].Y));
                Cv2.Line(frame, p1, p2, color, 1, LineTypes.AntiAlias);
            }
        }

        if (isClosed && indices.Length > 0)
        {
            int firstIdx = indices[0];
            int lastIdx = indices[^1];
            if (firstIdx < pts.Length && lastIdx < pts.Length)
            {
                Point pFirst = new((int)Math.Round(pts[firstIdx].X), (int)Math.Round(pts[firstIdx].Y));
                Point pLast = new((int)Math.Round(pts[lastIdx].X), (int)Math.Round(pts[lastIdx].Y));
                Cv2.Line(frame, pLast, pFirst, color, 1, LineTypes.AntiAlias);
            }
        }
    }
}
