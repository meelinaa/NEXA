using System;
using NEXA.Adapters.Output;
using NEXA.Common;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Lock;

/// <summary>
/// Application adapter orchestrating multi-stage security gesture evaluation, Windows OS session locking, and augmented reality milestone HUD rendering.
/// <para>
/// <b>What it is:</b> The controller executing PC lock commands upon completion of the 🖐️ &rarr; ✊ &rarr; 🖐️ &rarr; ✊ sequence.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description>Feeds primary tracked hand into <see cref="LockSequenceDetector"/>.</description></item>
/// <item><description>Dispatches <see cref="IInputSink.LockWorkstation"/> when all 4 steps are confirmed.</description></item>
/// <item><description>Renders a 4-milestone security sequence HUD badge with live 800ms countdown timer bars.</description></item>
/// </list>
/// </para>
/// </summary>
public class LockSequenceController
{
    /// <summary>
    /// The input sink used to invoke the OS session lock.
    /// </summary>
    private readonly IInputSink _inputSink;

    /// <summary>
    /// The domain-level temporal sequence detector.
    /// </summary>
    public LockSequenceDetector Detector { get; }

    /// <summary>
    /// Gets or sets a value indicating whether lock sequence detection is enabled.
    /// </summary>
    public bool Enabled
    {
        get => Detector.Enabled;
        set => Detector.Enabled = value;
    }

    /// <summary>
    /// Gets the internal state machine from the detector.
    /// </summary>
    public LockSequenceState State => Detector.State;

    /// <summary>
    /// Initializes a new instance of the <see cref="LockSequenceController"/> class.
    /// </summary>
    /// <param name="inputSink">The output adapter for OS inputs.</param>
    public LockSequenceController(IInputSink? inputSink = null)
    {
        _inputSink = inputSink ?? new Win32InputSink();
        Detector = new LockSequenceDetector();
    }

    /// <summary>
    /// Evaluates the tracked hand for the current frame and executes a workstation lock if the 4-step sequence completes.
    /// </summary>
    /// <param name="hand">The primary tracked hand.</param>
    public void Update(TrackedHand? hand)
    {
        bool shouldLock = Detector.Update(hand);
        if (shouldLock)
        {
            _inputSink.LockWorkstation();
        }
    }

    /// <summary>
    /// Renders visual sequence milestones and countdown progress bars onto the camera frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="hand">The primary tracked hand.</param>
    public void RenderFeedback(Mat frame, TrackedHand? hand)
    {
        DateTime now = DateTime.Now;

        // 1. Render Active Sequence 4-Step Badge
        if (State.CurrentStep != LockSequenceStep.Idle && hand != null)
        {
            int stepNum = (int)State.CurrentStep;
            Point handCenter = new((int)State.LastHandPos.X, (int)State.LastHandPos.Y);

            int badgeW = 210;
            int badgeH = 44;
            int badgeX = Math.Clamp(handCenter.X - badgeW / 2, 10, frame.Width - badgeW - 10);
            int badgeY = Math.Clamp(handCenter.Y - 90, 10, frame.Height - badgeH - 10);
            Rect badgeRect = new(badgeX, badgeY, badgeW, badgeH);

            // Translucent dark background
            using (Mat overlay = frame.Clone())
            {
                Cv2.Rectangle(overlay, badgeRect, new Scalar(20, 20, 30), -1);
                Cv2.AddWeighted(overlay, 0.80, frame, 0.20, 0, frame);
            }

            Cv2.Rectangle(frame, badgeRect, new Scalar(0, 180, 255), 1, LineTypes.AntiAlias);

            // Header text with remaining time
            double remainingSec = Math.Max(0.0, State.StepTimeoutSeconds - State.StepTimer.Elapsed.TotalSeconds);
            string headerText = $"LOCK PC: STEP {stepNum}/4 ({remainingSec:F1}s)";
            Cv2.PutText(frame, TextSanitizer.ToSafeAscii(headerText), new Point(badgeX + 8, badgeY + 16),
                HersheyFonts.HersheySimplex, 0.38, new Scalar(0, 220, 255), 1, LineTypes.AntiAlias);

            // 4 Milestone Step Dots / Boxes
            string[] stepLabels = new string[] { "[1:OFFEN]", "[2:FAUST]", "[3:OFFEN]", "[4:FAUST]" };
            for (int i = 0; i < 4; i++)
            {
                int boxX = badgeX + 6 + i * 49;
                int boxY = badgeY + 22;
                Rect stepBox = new(boxX, boxY, 46, 16);

                Scalar boxColor = (i + 1) <= stepNum
                    ? new Scalar(0, 255, 120) // Completed (Green)
                    : new Scalar(80, 80, 90); // Upcoming (Gray)

                if (i + 1 == stepNum)
                {
                    boxColor = new Scalar(0, 220, 255); // Current Active (Cyan)
                }

                Cv2.Rectangle(frame, stepBox, boxColor, 1, LineTypes.AntiAlias);
                Cv2.PutText(frame, TextSanitizer.ToSafeAscii(stepLabels[i]), new Point(boxX + 2, boxY + 12),
                    HersheyFonts.HersheySimplex, 0.28, boxColor, 1, LineTypes.AntiAlias);
            }

            // Timeout countdown progress bar at bottom of badge
            int barW = (int)(badgeW * State.RemainingWindowProgress);
            if (barW > 0)
            {
                Rect barRect = new(badgeX, badgeY + badgeH - 3, barW, 3);
                Cv2.Rectangle(frame, barRect, new Scalar(0, 220, 255), -1);
            }
        }

        // 2. Lock Triggered Alert Animation
        double elapsedLock = (now - State.LastLockTriggerTime).TotalMilliseconds;
        if (elapsedLock < 1200)
        {
            float progress = (float)(elapsedLock / 1200.0);
            int animRadius = (int)(25 + progress * 60);
            Point center = new((int)State.LastHandPos.X, (int)State.LastHandPos.Y);

            Cv2.Circle(frame, center, animRadius, new Scalar(0, 0, 255), 3, LineTypes.AntiAlias);
            Cv2.PutText(frame, TextSanitizer.ToSafeAscii("* PC LOCKED (WIN+L) *"), new Point(center.X - 85, center.Y - animRadius - 10),
                HersheyFonts.HersheySimplex, 0.58, new Scalar(0, 0, 255), 2, LineTypes.AntiAlias);
        }
    }
}
