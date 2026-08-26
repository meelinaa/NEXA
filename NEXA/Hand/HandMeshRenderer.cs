using OpenCvSharp;

namespace NEXA.Hand;

/// <summary>
/// Visualization renderer for hand skeletal mesh overlays, joint nodes, and HUD status badges.
/// <para>
/// <b>What it is:</b> An OpenCV-based 2D overlay graphics renderer designed for high-visibility visual feedback.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description>Draws anatomical bone connection lines between consecutive finger joints with an outer glow aesthetic.</description></item>
/// <item><description>Renders color-coded joint landmark nodes (tips, knuckles, and wrist).</description></item>
/// <item><description>Draws futuristic corner-bracket bounding boxes with a HUD metadata badge displaying handedness, confidence %, and classified gesture.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Why it is used:</b> Provides real-time visual verification of hand tracking accuracy and gesture classification state for the user.
/// </para>
/// <para>
/// <b>Consequence:</b> Transforms raw numerical landmark coordinates into an intuitive, polished augmented-reality overlay.
/// </para>
/// </summary>
public class HandMeshRenderer
{
    /// <summary>
    /// Standard anatomical bone connectivity topology defined by MediaPipe (21 joints connected across 5 fingers and the palm base arch).
    /// </summary>
    public static readonly (int Start, int End)[] BoneConnections =
    [
        // Thumb ray
        (0, 1), (1, 2), (2, 3), (3, 4),
        // Index finger ray
        (0, 5), (5, 6), (6, 7), (7, 8),
        // Middle finger ray
        (9, 10), (10, 11), (11, 12),
        // Ring finger ray
        (13, 14), (14, 15), (15, 16),
        // Pinky finger ray
        (0, 17), (17, 18), (18, 19), (19, 20),
        // Palm base arch (MCP knuckle baseline)
        (5, 9), (9, 13), (13, 17)
    ];

    /// <summary>
    /// Landmark indices corresponding to the 5 fingertips (Thumb=4, Index=8, Middle=12, Ring=16, Pinky=20).
    /// </summary>
    private static readonly int[] FingertipIds = [4, 8, 12, 16, 20];

    /// <summary>
    /// Gets or sets a value indicating whether the bounding box and HUD tag should be rendered.
    /// </summary>
    public bool ShowBoundingBox { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether joint landmark circles should be rendered.
    /// </summary>
    public bool ShowJoints { get; set; } = true;

    /// <summary>
    /// Renders the complete hand skeletal overlay and HUD badges onto the provided video frame.
    /// </summary>
    /// <param name="frame">The camera image frame (OpenCV Mat) to draw on.</param>
    /// <param name="hands">The list of active tracked hands.</param>
    public void Render(Mat frame, List<TrackedHand> hands)
    {
        if (hands == null || hands.Count == 0)
            return;

        foreach (TrackedHand hand in hands)
        {
            Point2f[] pts = hand.SmoothedLandmarks2D;

            // 1. Draw crisp glowing bone connection lines (neon green with bright core)
            foreach ((int start, int end) in BoneConnections)
            {
                Point p1 = new((int)Math.Round(pts[start].X), (int)Math.Round(pts[start].Y));
                Point p2 = new((int)Math.Round(pts[end].X), (int)Math.Round(pts[end].Y));

                // Outer thick glowing line
                Cv2.Line(frame, p1, p2, new Scalar(0, 255, 120), 3, LineTypes.AntiAlias);
                // Inner crisp bright core line
                Cv2.Line(frame, p1, p2, new Scalar(230, 255, 230), 1, LineTypes.AntiAlias);
            }

            // 2. Draw color-coded joint landmark nodes
            if (ShowJoints)
            {
                for (int i = 0; i < pts.Length; i++)
                {
                    Point pt = new((int)Math.Round(pts[i].X), (int)Math.Round(pts[i].Y));

                    if (Array.IndexOf(FingertipIds, i) >= 0)
                    {
                        // Fingertips: Vibrant gold/yellow with white center
                        Cv2.Circle(frame, pt, 6, new Scalar(0, 215, 255), -1, LineTypes.AntiAlias);
                        Cv2.Circle(frame, pt, 3, new Scalar(255, 255, 255), -1, LineTypes.AntiAlias);
                    }
                    else if (i == 0)
                    {
                        // Wrist base: Magenta
                        Cv2.Circle(frame, pt, 6, new Scalar(255, 50, 200), -1, LineTypes.AntiAlias);
                        Cv2.Circle(frame, pt, 3, new Scalar(255, 255, 255), -1, LineTypes.AntiAlias);
                    }
                    else
                    {
                        // Knuckle joints: Cyan
                        Cv2.Circle(frame, pt, 4, new Scalar(255, 200, 0), -1, LineTypes.AntiAlias);
                        Cv2.Circle(frame, pt, 2, new Scalar(255, 255, 255), -1, LineTypes.AntiAlias);
                    }
                }
            }

            // 3. Draw futuristic corner-bracket bounding box & metadata tag
            if (ShowBoundingBox && hand.BoundingBox.Width > 0 && hand.BoundingBox.Height > 0)
                DrawBoundingBoxAndHUD(frame, hand);
        }
    }

    /// <summary>
    /// Draws corner brackets and a dark badge containing handedness, confidence percentage, and gesture label.
    /// </summary>
    private static void DrawBoundingBoxAndHUD(Mat frame, TrackedHand hand)
    {
        Rect2f box = hand.BoundingBox;
        int x = (int)Math.Max(0, box.X);
        int y = (int)Math.Max(0, box.Y);
        int w = (int)Math.Min(frame.Width - x, box.Width);
        int h = (int)Math.Min(frame.Height - y, box.Height);

        if (w <= 0 || h <= 0)
            return;

        int cornerLen = Math.Min(25, Math.Min(w / 4, h / 4));
        Scalar boxColor = new(0, 220, 255);

        // Top-Left corner
        Cv2.Line(frame, new Point(x, y), new Point(x + cornerLen, y), boxColor, 2);
        Cv2.Line(frame, new Point(x, y), new Point(x, y + cornerLen), boxColor, 2);
        // Top-Right corner
        Cv2.Line(frame, new Point(x + w, y), new Point(x + w - cornerLen, y), boxColor, 2);
        Cv2.Line(frame, new Point(x + w, y), new Point(x + w, y + cornerLen), boxColor, 2);
        // Bottom-Left corner
        Cv2.Line(frame, new Point(x, y + h), new Point(x + cornerLen, y + h), boxColor, 2);
        Cv2.Line(frame, new Point(x, y + h), new Point(x, y + h - cornerLen), boxColor, 2);
        // Bottom-Right corner
        Cv2.Line(frame, new Point(x + w, y + h), new Point(x + w - cornerLen, y + h), boxColor, 2);
        Cv2.Line(frame, new Point(x + w, y + h), new Point(x + w, y + cornerLen), boxColor, 2);

        // Render metadata tag badge: "[Handedness] ([Confidence]%)  [Gesture]"
        string tagText = $"{hand.Handedness} ({hand.Confidence * 100:0}%)  {hand.Gesture}";
        Size textSize = Cv2.GetTextSize(tagText, HersheyFonts.HersheySimplex, 0.55, 1, out _);

        int tagY = Math.Max(textSize.Height + 10, y - 8);
        Rect tagRect = new(x, tagY - textSize.Height - 6, textSize.Width + 12, textSize.Height + 8);

        if (tagRect.X >= 0 && tagRect.Y >= 0 && tagRect.Right <= frame.Width && tagRect.Bottom <= frame.Height)
        {
            Cv2.Rectangle(frame, tagRect, new Scalar(15, 15, 20), -1);
            Cv2.Rectangle(frame, tagRect, boxColor, 1);
            Cv2.PutText(frame, tagText, new Point(x + 6, tagY - 2), HersheyFonts.HersheySimplex, 0.55, new Scalar(255, 255, 255), 1, LineTypes.AntiAlias);
        }
    }
}
