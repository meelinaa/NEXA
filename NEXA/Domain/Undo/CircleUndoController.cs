using System;
using NEXA.Adapters.Output;
using NEXA.Common;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Undo;

/// <summary>
/// Application adapter orchestrating Peace-sign wrist-twist gesture evaluation, Undo/Redo keyboard injection, and holographic AR rotary dial rendering.
/// <para>
/// <b>What it is:</b> The controller executing Undo (Ctrl+Z) and Redo (Ctrl+Y) when the user twists their wrist with a Peace sign (✌️).
/// </para>
/// </summary>
public class CircleUndoController
{
    /// <summary>
    /// The input sink used to inject keyboard events into the OS.
    /// </summary>
    private readonly IInputSink _inputSink;

    /// <summary>
    /// The domain-level wrist twist analyzer.
    /// </summary>
    public CircleUndoDetector Detector { get; }

    /// <summary>
    /// Gets or sets a value indicating whether wrist-twist Undo/Redo detection is enabled.
    /// </summary>
    public bool Enabled
    {
        get => Detector.Enabled;
        set => Detector.Enabled = value;
    }

    /// <summary>
    /// Gets the internal state machine from the detector.
    /// </summary>
    public CircleUndoState State => Detector.State;

    /// <summary>
    /// Initializes a new instance of the <see cref="CircleUndoController"/> class.
    /// </summary>
    /// <param name="inputSink">The output adapter for OS inputs.</param>
    public CircleUndoController(IInputSink? inputSink = null)
    {
        _inputSink = inputSink ?? new Win32InputSink();
        Detector = new CircleUndoDetector();
    }

    /// <summary>
    /// Evaluates tracked hand motion for the current frame and triggers Undo or Redo.
    /// </summary>
    /// <param name="hand">The primary tracked hand.</param>
    public void Update(TrackedHand? hand)
    {
        CircleUndoAction action = Detector.Update(hand);
        if (action == CircleUndoAction.Undo)
        {
            _inputSink.SendUndo();
        }
        else if (action == CircleUndoAction.Redo)
        {
            _inputSink.SendRedo();
        }
    }

    /// <summary>
    /// Renders visual holographic rotary dials, wrist vectors, and trigger feedback animations onto the camera frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="hand">The primary tracked hand.</param>
    public void RenderFeedback(Mat frame, TrackedHand? hand)
    {
        DateTime now = DateTime.Now;

        // 1. Render Holographic Rotary Dial & Pointer
        if (State.IsTracking && hand != null)
        {
            Point wristPt = new((int)Math.Round(State.WristPos.X), (int)Math.Round(State.WristPos.Y));
            Point tipsPt = new((int)Math.Round(State.FingerTipsPos.X), (int)Math.Round(State.FingerTipsPos.Y));

            int radius = 45;
            Cv2.Circle(frame, wristPt, radius, new Scalar(40, 45, 60), 2, LineTypes.AntiAlias);

            // Determine active twist intent
            bool isUndo = State.AngleDeltaDeg < -5.0;
            bool isRedo = State.AngleDeltaDeg > 5.0;

            Scalar dialColor = isUndo
                ? new Scalar(0, 220, 255) // Cyan for Undo
                : (isRedo ? new Scalar(255, 160, 0) : new Scalar(0, 255, 120)); // Amber for Redo, Green for Neutral

            // Pointer line from wrist to fingertips
            Cv2.Line(frame, wristPt, tipsPt, dialColor, 2, LineTypes.AntiAlias);
            Cv2.Circle(frame, tipsPt, 6, dialColor, -1, LineTypes.AntiAlias);

            // Floating Dial Badge
            string sign = State.AngleDeltaDeg >= 0 ? "+" : "";
            string actionHint = isUndo ? "UNDO <--" : (isRedo ? "REDO -->" : "DREHEN");
            string badgeText = $"[PEACE: {actionHint} ({sign}{State.AngleDeltaDeg:F0} deg / 42 deg)]";

            Point badgePos = new(Math.Max(10, wristPt.X - 85), Math.Max(25, wristPt.Y - radius - 10));
            Cv2.PutText(frame, TextSanitizer.ToSafeAscii(badgeText), badgePos,
                HersheyFonts.HersheySimplex, 0.40, dialColor, 1, LineTypes.AntiAlias);
        }

        // 2. Action Triggered Pulse Animation
        double elapsedAction = (now - State.LastActionTime).TotalMilliseconds;
        if (elapsedAction < 1000 && !string.IsNullOrEmpty(State.LastAction))
        {
            float progress = (float)(elapsedAction / 1000.0);
            int animRadius = (int)(20 + progress * 55);
            Point center = new((int)State.LastActionCenter.X, (int)State.LastActionCenter.Y);

            Scalar actionColor = State.LastAction == "UNDO"
                ? new Scalar(0, 220, 255) // Cyan
                : new Scalar(255, 160, 0); // Amber

            Cv2.Circle(frame, center, animRadius, actionColor, 3, LineTypes.AntiAlias);

            string actionLabel = State.LastAction == "UNDO" ? "* UNDO (Ctrl+Z) *" : "* REDO (Ctrl+Y) *";
            Cv2.PutText(frame, TextSanitizer.ToSafeAscii(actionLabel), new Point(center.X - 70, center.Y - animRadius - 8),
                HersheyFonts.HersheySimplex, 0.54, actionColor, 2, LineTypes.AntiAlias);
        }
    }
}
