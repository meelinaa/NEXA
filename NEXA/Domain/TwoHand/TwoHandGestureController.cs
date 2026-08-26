using System;
using System.Collections.Generic;
using NEXA.Adapters.Output;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.TwoHand;

/// <summary>
/// Application adapter orchestrating two-hand Maximize/Minimize window operations and augmented reality HUD rendering.
/// <para>
/// <b>What it is:</b> The controller executing multi-hand window state transitions.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description>Passes dual tracked hands into <see cref="TwoHandGestureDetector"/>.</description></item>
/// <item><description>Dispatches <see cref="IInputSink.MaximizeWindow"/> or <see cref="IInputSink.MinimizeWindow"/> based on gesture decisions.</description></item>
/// <item><description>Renders AR visual cues: active window countdown banner, fingertip touch link lines, and floating expansion/downward action arrows.</description></item>
/// </list>
/// </para>
/// </summary>
public class TwoHandGestureController
{
    /// <summary>
    /// The input sink used to apply window state changes and read focused window handles.
    /// </summary>
    private readonly IInputSink _inputSink;

    /// <summary>
    /// The core domain detector evaluating Maximize and Minimize gestures.
    /// </summary>
    public TwoHandGestureDetector Detector { get; }

    /// <summary>
    /// Gets or sets a value indicating whether two-hand gesture processing is active.
    /// </summary>
    public bool Enabled
    {
        get => Detector.Enabled;
        set => Detector.Enabled = value;
    }

    /// <summary>
    /// Gets the internal state machine from the detector.
    /// </summary>
    public TwoHandGestureState State => Detector.State;

    /// <summary>
    /// Initializes a new instance of the <see cref="TwoHandGestureController"/> class.
    /// </summary>
    /// <param name="inputSink">The output adapter for OS inputs.</param>
    public TwoHandGestureController(IInputSink? inputSink = null)
    {
        _inputSink = inputSink ?? new Win32InputSink();
        Detector = new TwoHandGestureDetector();
    }

    /// <summary>
    /// Evaluates tracked hands for the current frame and executes window maximize/minimize commands.
    /// </summary>
    /// <param name="hands">The list of active tracked hands.</param>
    public void Update(List<TrackedHand>? hands)
    {
        TwoHandGestureDecision? decision = Detector.Update(hands, _inputSink);
        if (decision != null)
        {
            if (decision.Action == TwoHandAction.Maximize)
            {
                _inputSink.MaximizeWindow(decision.TargetHwnd);
            }
            else if (decision.Action == TwoHandAction.Minimize)
            {
                _inputSink.MinimizeWindow(decision.TargetHwnd);
            }
        }
    }

    /// <summary>
    /// Renders visual feedback (3.0s window countdown badge, touch link lines, and action animations) onto the camera frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="hands">The list of active tracked hands.</param>
    public void RenderFeedback(Mat frame, List<TrackedHand>? hands)
    {
        DateTime now = DateTime.Now;

        // 1. Active 3.0s Window Status Banner (Top Center)
        if (State.IsWindowActive && _inputSink.LastFocusedHwnd != IntPtr.Zero)
        {
            string winTitle = _inputSink.LastFocusedTitle.Length > 20
                ? _inputSink.LastFocusedTitle.Substring(0, 17) + "..."
                : _inputSink.LastFocusedTitle;

            string bannerText = $"[2-HAND READY ({State.RemainingWindowSeconds:F1}s): {winTitle}]";
            Size textSize = Cv2.GetTextSize(bannerText, HersheyFonts.HersheySimplex, 0.44, 1, out _);

            int bannerX = Math.Max(10, (frame.Width - textSize.Width) / 2);
            Rect bannerRect = new(bannerX - 10, 12, textSize.Width + 20, textSize.Height + 14);

            Cv2.Rectangle(frame, bannerRect, new Scalar(15, 20, 30), -1);
            Cv2.Rectangle(frame, bannerRect, new Scalar(0, 255, 120), 1);
            Cv2.PutText(frame, bannerText, new Point(bannerX, 12 + textSize.Height + 4),
                HersheyFonts.HersheySimplex, 0.44, new Scalar(0, 255, 120), 1, LineTypes.AntiAlias);
        }

        // 2. Touch Link Line between Index Fingertips (during Maximize initiation)
        if (hands != null && hands.Count == 2 && State.IsTouchActive)
        {
            Point p1 = new((int)hands[0].SmoothedLandmarks2D[8].X, (int)hands[0].SmoothedLandmarks2D[8].Y);
            Point p2 = new((int)hands[1].SmoothedLandmarks2D[8].X, (int)hands[1].SmoothedLandmarks2D[8].Y);

            // Glowing cyan link line
            Cv2.Line(frame, p1, p2, new Scalar(0, 220, 255), 2, LineTypes.AntiAlias);
            Cv2.Circle(frame, p1, 7, new Scalar(0, 255, 120), -1, LineTypes.AntiAlias);
            Cv2.Circle(frame, p2, 7, new Scalar(0, 255, 120), -1, LineTypes.AntiAlias);
        }

        // 3. Floating Trigger Animation (Maximize Expansion or Minimize Downward Arrows)
        double elapsedFeedback = (now - State.LastFeedbackTime).TotalMilliseconds;
        if (elapsedFeedback < 650.0)
        {
            float progress = (float)(elapsedFeedback / 650.0);
            int cx = (int)State.LastFeedbackCenter.X;
            int cy = (int)State.LastFeedbackCenter.Y;

            if (State.LastAction == "MAXIMIZE")
            {
                int spread = (int)(progress * 60);
                Scalar color = new(0, 255, 120); // Neon Green

                string maxText = "<-- MAXIMIZE -->";
                Cv2.PutText(frame, maxText, new Point(Math.Max(10, cx - 80), Math.Max(30, cy)),
                    HersheyFonts.HersheySimplex, 0.65, color, 2, LineTypes.AntiAlias);

                // Expanding horizontal arrows
                Cv2.ArrowedLine(frame, new Point(cx, cy + 15), new Point(cx - spread - 20, cy + 15), color, 2);
                Cv2.ArrowedLine(frame, new Point(cx, cy + 15), new Point(cx + spread + 20, cy + 15), color, 2);
            }
            else if (State.LastAction == "MINIMIZE")
            {
                int drop = (int)(progress * 45);
                Scalar color = new(0, 165, 255); // Amber

                string minText = "v v MINIMIZE v v";
                Cv2.PutText(frame, minText, new Point(Math.Max(10, cx - 80), Math.Max(30, cy + drop)),
                    HersheyFonts.HersheySimplex, 0.65, color, 2, LineTypes.AntiAlias);

                // Downward drop arrows
                Cv2.ArrowedLine(frame, new Point(cx - 30, cy + drop), new Point(cx - 30, cy + drop + 30), color, 2);
                Cv2.ArrowedLine(frame, new Point(cx + 30, cy + drop), new Point(cx + 30, cy + drop + 30), color, 2);
            }
        }
    }
}
