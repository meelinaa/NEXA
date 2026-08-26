using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Object;

/// <summary>
/// Unified controller for interactive 2D virtual object manipulation, supporting relative grab-and-drag and continuous pinch-to-zoom scaling.
/// <para>
/// <b>What it is:</b> An augmented-reality spatial interaction engine that allows users to manipulate a virtual window in the camera viewport using hand gestures.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description><b>Clenched Fist Grabbing:</b> Requires maintaining a continuous fist gesture for 2.0s to latch onto the object, locking the relative offset and dragging it around the screen.</description></item>
/// <item><description><b>Continuous Zoom Scaling:</b> Maps the continuous aperture between closed pinch and open L-sign to smooth optical magnification (0.3x to 3.0x).</description></item>
/// <item><description><b>Augmented Reality Rendering:</b> Draws a translucent HUD window with dynamic state badges (idle, hold countdown, grabbed, zooming) and corner bracket graphics.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Why it is used:</b> Combines spatial dragging and zoom scaling onto a single visual target object without conflicting gesture triggers.
/// </para>
/// <para>
/// <b>Consequence:</b> Demonstrates multi-modal spatial computing interactions cleanly in real time.
/// </para>
/// </summary>
public class VirtualObjectController
{
    /// <summary>
    /// The target virtual object storing current 2D center coordinates and base dimensions.
    /// </summary>
    public TestObject TargetObject { get; } = new();

    /// <summary>
    /// The zoom state machine tracking continuous magnification factors.
    /// </summary>
    public ZoomState ZoomState { get; } = new();

    /// <summary>
    /// The grab state machine tracking fist hold timers and spatial offset locks.
    /// </summary>
    public GrabState GrabState { get; } = new();

    /// <summary>
    /// Updates object position and zoom scaling based on the active hand gesture.
    /// </summary>
    /// <param name="hand">The tracked hand instance.</param>
    /// <param name="frameWidth">Camera frame width in pixels.</param>
    /// <param name="frameHeight">Camera frame height in pixels.</param>
    public void Update(TrackedHand? hand, int frameWidth, int frameHeight)
    {
        if (hand == null)
        {
            HandleNoHand();
            return;
        }

        Point2f[] lm = hand.SmoothedLandmarks2D;
        Point2f palmCenter = lm[9]; // Middle finger MCP knuckle (geometric center of palm)
        string currentGesture = hand.Gesture;

        // 1. Evaluate Grab interaction (clenched fist with required hold duration)
        UpdateGrab(palmCenter, currentGesture, frameWidth, frameHeight);

        // 2. Evaluate Zoom interaction (continuous pinch-to-L continuum)
        UpdateZoom(hand);
    }

    /// <summary>
    /// Manages fist hold timer, offset locking, and dragging translation.
    /// </summary>
    private void UpdateGrab(Point2f palmCenter, string gesture, int frameWidth, int frameHeight)
    {
        bool isFist = gesture == "Fist";
        GrabState.LastPalmCenter = palmCenter;

        if (isFist)
        {
            if (!GrabState.FistTimer.IsRunning)
                GrabState.FistTimer.Restart();

            GrabState.HoldDurationSeconds = GrabState.FistTimer.Elapsed.TotalSeconds;

            // Activate grab only after holding continuous fist for the required hold time (2.0s)
            if (!GrabState.Active && GrabState.HoldDurationSeconds >= GrabState.RequiredHoldTime)
            {
                GrabState.Active = true;
                // Lock relative offset so object does not snap abruptly to palm center
                GrabState.HandOffsetToObject = (palmCenter.X - TargetObject.X, palmCenter.Y - TargetObject.Y);
            }
        }
        else
        {
            if (GrabState.FistTimer.IsRunning)
                GrabState.FistTimer.Reset();
            
            GrabState.HoldDurationSeconds = 0;

            if (GrabState.Active)
                GrabState.Active = false;
        }

        // Translate object if actively grabbed
        if (GrabState.Active)
        {
            double newX = palmCenter.X - GrabState.HandOffsetToObject.X;
            double newY = palmCenter.Y - GrabState.HandOffsetToObject.Y;

            int margin = 50;
            TargetObject.X = Math.Clamp(newX, margin, frameWidth - margin);
            TargetObject.Y = Math.Clamp(newY, margin, frameHeight - margin);
        }
    }

    /// <summary>
    /// Manages relative aperture ratio tracking and zoom factor scaling.
    /// </summary>
    private void UpdateZoom(TrackedHand hand)
    {
        // Suppress zoom while dragging an object
        if (GrabState.Active) 
            return;

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

        // Deadzone: Ignore minor drifts within +/- 3%
        if (Math.Abs(relativeScale - 1.0) < 0.03)
            relativeScale = 1.0;

        double targetZoom = ZoomState.LastStableZoom * relativeScale;
        ZoomState.CurrentZoom = Math.Clamp(targetZoom, 0.3, 3.0);
    }

    /// <summary>
    /// Resets transient timers and active interactions when tracking is lost.
    /// </summary>
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

    /// <summary>
    /// Resets the object to its default initial spawn position and 1.0x scale factor.
    /// </summary>
    /// <param name="frameWidth">Camera frame width.</param>
    /// <param name="frameHeight">Camera frame height.</param>
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

    /// <summary>
    /// Renders the virtual test object window with alpha blending, corner accents, and telemetry text onto the frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    public void Render(Mat frame)
    {
        int targetW = (int)Math.Round(TargetObject.BaseWidth * ZoomState.CurrentZoom);
        int targetH = (int)Math.Round(TargetObject.BaseHeight * ZoomState.CurrentZoom);

        int left = (int)Math.Round(TargetObject.X - targetW / 2.0);
        int top = (int)Math.Round(TargetObject.Y - targetH / 2.0);

        Rect rect = new(
            Math.Max(2, left),
            Math.Max(2, top),
            Math.Min(frame.Width - 4, targetW),
            Math.Min(frame.Height - 4, targetH)
        );

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
            themeColor = new Scalar(0, 230, 255); // Glowing Gold
            statusTag = $"[ZOOM: {ZoomState.CurrentZoom:F2}x]";
        }
        else
        {
            themeColor = new Scalar(255, 180, 50); // Futuristic Cyan
            statusTag = $"[IDLE: {ZoomState.CurrentZoom:F2}x]";
        }

        // 1. Semi-transparent backdrop
        using (Mat overlay = frame.Clone())
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
            Cv2.Line(frame, new Point(rect.Left, rect.Top), new Point(rect.Left + cornerLen, rect.Top), themeColor, 2);

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
            subText = $"Fist hold countdown: {GrabState.HoldDurationSeconds:F1}s";
        }
        else
        {
            subText = "Fist (2s): Grab | Pinch-L: Zoom";
        }

        Cv2.PutText(frame, subText, new Point(Math.Max(8, rect.Left + 8), Math.Max(40, rect.Top + 38)),
            HersheyFonts.HersheySimplex, 0.35, new Scalar(200, 200, 200), 1, LineTypes.AntiAlias);
    }
}
