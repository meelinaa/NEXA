using OpenCvSharp;

namespace NEXA.Hand;

public class HandMeshRenderer
{
    // Standard MediaPipe Bone Connections (Anatomical Finger and Palm Bones)
    public static readonly (int Start, int End)[] BoneConnections = new[]
    {
        // Daumen (Thumb)
        (0, 1), (1, 2), (2, 3), (3, 4),
        // Zeigefinger (Index)
        (0, 5), (5, 6), (6, 7), (7, 8),
        // Mittelfinger (Middle)
        (9, 10), (10, 11), (11, 12),
        // Ringfinger (Ring)
        (13, 14), (14, 15), (15, 16),
        // Kleiner Finger (Pinky)
        (0, 17), (17, 18), (18, 19), (19, 20),
        // Handflächen-Basis (Palm Base Arc)
        (5, 9), (9, 13), (13, 17)
    };

    private static readonly int[] FingertipIds = { 4, 8, 12, 16, 20 };

    public bool ShowBoundingBox { get; set; } = true;
    public bool ShowJoints { get; set; } = true;

    public void Render(Mat frame, List<TrackedHand> hands)
    {
        if (hands == null || hands.Count == 0) return;

        foreach (var hand in hands)
        {
            var pts = hand.SmoothedLandmarks2D;

            // 1. Draw Clean Bone Connections (Nur Finger- und Handknochen, ohne Schwimmhäute)
            foreach (var (start, end) in BoneConnections)
            {
                var p1 = new Point((int)Math.Round(pts[start].X), (int)Math.Round(pts[start].Y));
                var p2 = new Point((int)Math.Round(pts[end].X), (int)Math.Round(pts[end].Y));

                // Glowing crisp bone lines (Cyan/Neon-Grün)
                Cv2.Line(frame, p1, p2, new Scalar(0, 255, 120), 3, LineTypes.AntiAlias);
                Cv2.Line(frame, p1, p2, new Scalar(230, 255, 230), 1, LineTypes.AntiAlias);
            }

            // 2. Draw Joint Landmark Nodes
            if (ShowJoints)
            {
                for (int i = 0; i < pts.Length; i++)
                {
                    var pt = new Point((int)Math.Round(pts[i].X), (int)Math.Round(pts[i].Y));

                    if (Array.IndexOf(FingertipIds, i) >= 0)
                    {
                        // Fingerspitzen: Leuchtend Gelb/Orange
                        Cv2.Circle(frame, pt, 6, new Scalar(0, 215, 255), -1, LineTypes.AntiAlias);
                        Cv2.Circle(frame, pt, 3, new Scalar(255, 255, 255), -1, LineTypes.AntiAlias);
                    }
                    else if (i == 0)
                    {
                        // Handgelenk: Magenta
                        Cv2.Circle(frame, pt, 6, new Scalar(255, 50, 200), -1, LineTypes.AntiAlias);
                        Cv2.Circle(frame, pt, 3, new Scalar(255, 255, 255), -1, LineTypes.AntiAlias);
                    }
                    else
                    {
                        // Gelenke: Cyan
                        Cv2.Circle(frame, pt, 4, new Scalar(255, 200, 0), -1, LineTypes.AntiAlias);
                        Cv2.Circle(frame, pt, 2, new Scalar(255, 255, 255), -1, LineTypes.AntiAlias);
                    }
                }
            }

            // 3. Bounding Box & HUD Tag
            if (ShowBoundingBox && hand.BoundingBox.Width > 0 && hand.BoundingBox.Height > 0)
            {
                DrawBoundingBoxAndHUD(frame, hand);
            }
        }
    }

    private static void DrawBoundingBoxAndHUD(Mat frame, TrackedHand hand)
    {
        var box = hand.BoundingBox;
        int x = (int)Math.Max(0, box.X);
        int y = (int)Math.Max(0, box.Y);
        int w = (int)Math.Min(frame.Width - x, box.Width);
        int h = (int)Math.Min(frame.Height - y, box.Height);

        if (w <= 0 || h <= 0) return;

        // Draw futuristic corner brackets
        int cornerLen = Math.Min(25, Math.Min(w / 4, h / 4));
        var boxColor = new Scalar(0, 220, 255);

        // Top-Left
        Cv2.Line(frame, new Point(x, y), new Point(x + cornerLen, y), boxColor, 2);
        Cv2.Line(frame, new Point(x, y), new Point(x, y + cornerLen), boxColor, 2);
        // Top-Right
        Cv2.Line(frame, new Point(x + w, y), new Point(x + w - cornerLen, y), boxColor, 2);
        Cv2.Line(frame, new Point(x + w, y), new Point(x + w, y + cornerLen), boxColor, 2);
        // Bottom-Left
        Cv2.Line(frame, new Point(x, y + h), new Point(x + cornerLen, y + h), boxColor, 2);
        Cv2.Line(frame, new Point(x, y + h), new Point(x, y + h - cornerLen), boxColor, 2);
        // Bottom-Right
        Cv2.Line(frame, new Point(x + w, y + h), new Point(x + w - cornerLen, y + h), boxColor, 2);
        Cv2.Line(frame, new Point(x + w, y + h), new Point(x + w, y + h - cornerLen), boxColor, 2);

        // Header Tag badge: [Handedness] [Confidence%] [Gesture]
        string tagText = $"{hand.Handedness} ({hand.Confidence * 100:0}%)  {hand.Gesture}";
        var textSize = Cv2.GetTextSize(tagText, HersheyFonts.HersheySimplex, 0.55, 1, out int baseline);

        int tagY = Math.Max(textSize.Height + 10, y - 8);
        var tagRect = new Rect(x, tagY - textSize.Height - 6, textSize.Width + 12, textSize.Height + 8);

        if (tagRect.X >= 0 && tagRect.Y >= 0 && tagRect.Right <= frame.Width && tagRect.Bottom <= frame.Height)
        {
            Cv2.Rectangle(frame, tagRect, new Scalar(15, 15, 20), -1);
            Cv2.Rectangle(frame, tagRect, boxColor, 1);
            Cv2.PutText(frame, tagText, new Point(x + 6, tagY - 2), HersheyFonts.HersheySimplex, 0.55, new Scalar(255, 255, 255), 1, LineTypes.AntiAlias);
        }
    }
}
