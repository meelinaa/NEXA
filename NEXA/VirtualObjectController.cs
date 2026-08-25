using System;
using System.Diagnostics;
using OpenCvSharp;

namespace NEXA;

public class TestObject
{
    public double X { get; set; } = 950;
    public double Y { get; set; } = 480;
    public int BaseWidth { get; set; } = 180;
    public int BaseHeight { get; set; } = 120;
}

public class GrabState
{
    public bool Active { get; set; } = false;
    public double HoldDurationSeconds { get; set; } = 0.0;
    public double RequiredHoldTime { get; set; } = 2.0; // 2 Sekunden Haltezeit
    public (double X, double Y) HandOffsetToObject { get; set; }
    public Point2f LastPalmCenter { get; set; }
    public readonly Stopwatch FistTimer = new();
}

public class VirtualObjectController
{
    public TestObject TargetObject { get; } = new();
    public ZoomState ZoomState { get; } = new();
    public GrabState GrabState { get; } = new();

    public void Update(TrackedHand? hand, int frameWidth, int frameHeight)
    {
        if (hand == null)
        {
            HandleNoHand();
            return;
        }

        var lm = hand.SmoothedLandmarks2D;
        var palmCenter = lm[9]; // Middle MCP / Palm center
        string currentGesture = hand.Gesture;

        // 1. Grab (Faust) mit 5-Sekunden-Haltezeit
        UpdateGrab(palmCenter, currentGesture, frameWidth, frameHeight);

        // 2. Zoom (Pinch Closed <-> L)
        UpdateZoom(hand);
    }

    private void UpdateGrab(Point2f palmCenter, string gesture, int frameWidth, int frameHeight)
    {
        bool isFist = gesture == "Fist";
        GrabState.LastPalmCenter = palmCenter;

        if (isFist)
        {
            if (!GrabState.FistTimer.IsRunning)
            {
                GrabState.FistTimer.Restart();
            }

            GrabState.HoldDurationSeconds = GrabState.FistTimer.Elapsed.TotalSeconds;

            // Erst nach 5 Sekunden kontinuierlicher Faust wird das Greifen aktiviert!
            if (!GrabState.Active && GrabState.HoldDurationSeconds >= GrabState.RequiredHoldTime)
            {
                GrabState.Active = true;
                GrabState.HandOffsetToObject = (palmCenter.X - TargetObject.X, palmCenter.Y - TargetObject.Y);
            }
        }
        else
        {
            if (GrabState.FistTimer.IsRunning)
            {
                GrabState.FistTimer.Reset();
            }
            GrabState.HoldDurationSeconds = 0;

            if (GrabState.Active)
            {
                GrabState.Active = false;
            }
        }

        // Objekt bewegen, sobald Grab nach 5s aktiv ist
        if (GrabState.Active)
        {
            double newX = palmCenter.X - GrabState.HandOffsetToObject.X;
            double newY = palmCenter.Y - GrabState.HandOffsetToObject.Y;

            int margin = 50;
            TargetObject.X = Math.Clamp(newX, margin, frameWidth - margin);
            TargetObject.Y = Math.Clamp(newY, margin, frameHeight - margin);
        }
    }

    private void UpdateZoom(TrackedHand hand)
    {
        // Kein Zoom während aktiven Greifens
        if (GrabState.Active) return;

        bool isZoomGesture = hand.Gesture == "Pinch Closed" || hand.Gesture == "L" || hand.Gesture.Contains("Zoom");

        double palmSize = hand.Distance(0, 9);
        double thumbIndexDist = hand.Distance(4, 8);
        double ratio = palmSize > 1.0 ? thumbIndexDist / palmSize : 0.25;
        ZoomState.LiveRatio = ratio;

        if (!isZoomGesture)
        {
            if (ZoomState.Active)
            {
                ZoomState.Active = false;
                ZoomState.LastStableZoom = ZoomState.CurrentZoom;
            }
            return;
        }

        if (!ZoomState.Active)
        {
            ZoomState.Active = true;
            ZoomState.BaselineRatio = Math.Max(0.05, ratio);
            return;
        }

        double relativeScale = ratio / ZoomState.BaselineRatio;

        // Deadzone: +/- 3%
        if (Math.Abs(relativeScale - 1.0) < 0.03)
        {
            relativeScale = 1.0;
        }

        double targetZoom = ZoomState.LastStableZoom * relativeScale;
        ZoomState.CurrentZoom = Math.Clamp(targetZoom, 0.3, 3.0);
    }

    private void HandleNoHand()
    {
        if (GrabState.FistTimer.IsRunning)
        {
            GrabState.FistTimer.Reset();
        }
        GrabState.HoldDurationSeconds = 0;
        GrabState.Active = false;

        if (ZoomState.Active)
        {
            ZoomState.Active = false;
            ZoomState.LastStableZoom = ZoomState.CurrentZoom;
        }
    }

    public void Reset(int frameWidth = 1280, int frameHeight = 720)
    {
        TargetObject.X = frameWidth - 250;
        TargetObject.Y = frameHeight - 200;
        GrabState.Active = false;
        GrabState.FistTimer.Reset();
        GrabState.HoldDurationSeconds = 0;

        ZoomState.Active = false;
        ZoomState.BaselineRatio = 1.0;
        ZoomState.CurrentZoom = 1.0;
        ZoomState.LastStableZoom = 1.0;
        ZoomState.LiveRatio = 0.0;
    }

    public void Render(Mat frame)
    {
        int targetW = (int)Math.Round(TargetObject.BaseWidth * ZoomState.CurrentZoom);
        int targetH = (int)Math.Round(TargetObject.BaseHeight * ZoomState.CurrentZoom);

        int left = (int)Math.Round(TargetObject.X - targetW / 2.0);
        int top = (int)Math.Round(TargetObject.Y - targetH / 2.0);

        var rect = new Rect(
            Math.Max(2, left),
            Math.Max(2, top),
            Math.Min(frame.Width - 4, targetW),
            Math.Min(frame.Height - 4, targetH)
        );

        // Styling Color & Status
        Scalar themeColor;
        string statusTag;

        if (GrabState.Active)
        {
            themeColor = new Scalar(0, 100, 255); // Glowing Orange / Red
            statusTag = "[GRABBED - MOVING]";
        }
        else if (GrabState.HoldDurationSeconds > 0)
        {
            themeColor = new Scalar(0, 165, 255); // Amber (Holding Countdown)
            double remaining = Math.Max(0, GrabState.RequiredHoldTime - GrabState.HoldDurationSeconds);
            statusTag = $"[HOLD: {remaining:F1}s]";
        }
        else if (ZoomState.Active)
        {
            themeColor = new Scalar(0, 230, 255); // Glowing Amber / Gold
            statusTag = $"[ZOOM: {ZoomState.CurrentZoom:F2}x]";
        }
        else
        {
            themeColor = new Scalar(255, 180, 50); // Futuristic Cyan / Blue
            statusTag = $"[IDLE: {ZoomState.CurrentZoom:F2}x]";
        }

        // 1. Semi-transparent backdrop
        using (var overlay = frame.Clone())
        {
            Cv2.Rectangle(overlay, rect, new Scalar(18, 18, 26), -1);
            Cv2.AddWeighted(overlay, 0.40, frame, 0.60, 0, frame);
        }

        // 2. Corner Bracket Accents
        Cv2.Rectangle(frame, rect, themeColor, 1, LineTypes.AntiAlias);
        int cornerLen = Math.Min(20, Math.Min(rect.Width / 3, rect.Height / 3));

        if (cornerLen > 4)
        {
            Cv2.Line(frame, new Point(rect.Left, rect.Top), new Point(rect.Left + cornerLen, rect.Top), themeColor, 2);
            Cv2.Line(frame, new Point(rect.Left, rect.Top), new Point(rect.Left, rect.Top + cornerLen), themeColor, 2);

            Cv2.Line(frame, new Point(rect.Right, rect.Top), new Point(rect.Right - cornerLen, rect.Top), themeColor, 2);
            Cv2.Line(frame, new Point(rect.Right, rect.Top), new Point(rect.Right, rect.Top + cornerLen), themeColor, 2);

            Cv2.Line(frame, new Point(rect.Left, rect.Bottom), new Point(rect.Left + cornerLen, rect.Bottom), themeColor, 2);
            Cv2.Line(frame, new Point(rect.Left, rect.Bottom), new Point(rect.Left, rect.Bottom - cornerLen), themeColor, 2);

            Cv2.Line(frame, new Point(rect.Right, rect.Bottom), new Point(rect.Right - cornerLen, rect.Bottom), themeColor, 2);
            Cv2.Line(frame, new Point(rect.Right, rect.Bottom), new Point(rect.Right, rect.Bottom - cornerLen), themeColor, 2);
        }

        // 3. Center Crosshair
        Cv2.DrawMarker(frame, new Point((int)TargetObject.X, (int)TargetObject.Y), themeColor, MarkerTypes.Cross, 12, 1);

        // 4. Header Bar / Title
        string title = $"TEST WINDOW {statusTag}";
        Cv2.PutText(frame, title, new Point(Math.Max(8, rect.Left + 8), Math.Max(22, rect.Top + 20)),
            HersheyFonts.HersheySimplex, 0.40, new Scalar(255, 255, 255), 1, LineTypes.AntiAlias);

        string subText;
        if (GrabState.Active)
        {
            subText = $"Pos: ({(int)TargetObject.X}, {(int)TargetObject.Y})";
        }
        else if (GrabState.HoldDurationSeconds > 0)
        {
            subText = $"Faust 5s halten zum Greifen ({GrabState.HoldDurationSeconds:F1}s)";
        }
        else
        {
            subText = "Faust (5s): Greifen | Pinch-L: Zoom";
        }

        Cv2.PutText(frame, subText, new Point(Math.Max(8, rect.Left + 8), Math.Max(40, rect.Top + 38)),
            HersheyFonts.HersheySimplex, 0.35, new Scalar(200, 200, 200), 1, LineTypes.AntiAlias);
    }
}
