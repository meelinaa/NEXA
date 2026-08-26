using System;
using System.Collections.Generic;
using NEXA.Adapters.Output;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.TwoHand;

/// <summary>
/// Domain-level analyzer evaluating two-hand Maximize, Minimize, and Camera-Frame Screenshot gestures with a 2.0-second hold requirement.
/// <para>
/// <b>What it is:</b> A multi-hand spatial gesture analyzer coordinating window sizing and screen capture interactions.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="number">
/// <item><description><b>Camera-Frame Screenshot:</b> When both hands form an "L" posture, calculates a viewfinder bounding box. Holding both Index tips and Thumb tips touching continuously for 2.0 seconds triggers a fullscreen capture.</description></item>
/// <item><description><b>Maximize Detection:</b> Detects index fingertip touch followed by rapid horizontal expansion (>40% increase within 600ms), gated by 3.0s window and suppressed for 2.0s post-screenshot.</description></item>
/// <item><description><b>Minimize Detection:</b> Detects dual side-by-side hands executing a synchronous fast downward swipe (>40px in &lt;300ms).</description></item>
/// <item><description><b>Cooldown Protection:</b> Enforces a 750ms refractory period following any action, plus a 2.0s block on Maximize post-screenshot.</description></item>
/// </list>
/// </para>
/// </summary>
public class TwoHandGestureDetector
{
    /// <summary>
    /// Gets the internal state machine tracking timing, touch anchors, velocity queues, and framing rectangles.
    /// </summary>
    public TwoHandGestureState State { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether two-hand gesture detection is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Notifies the detector that a fist-grab gesture was released, restarting the 3.0-second active window.
    /// </summary>
    public void NotifyFistReleased()
    {
        State.LastFistReleaseTime = DateTime.Now;
    }

    /// <summary>
    /// Evaluates tracked hands for the current frame to detect Screenshot, Maximize, or Minimize gestures.
    /// </summary>
    /// <param name="hands">The list of active tracked hands.</param>
    /// <param name="inputSink">The output adapter holding the focused window handle.</param>
    /// <returns>A <see cref="TwoHandGestureDecision"/> if an action was triggered; otherwise, <c>null</c>.</returns>
    public TwoHandGestureDecision? Update(List<TrackedHand>? hands, IInputSink inputSink)
    {
        if (!Enabled || hands == null || hands.Count != 2)
        {
            State.IsCameraFrameActive = false;
            State.ScreenshotHoldTimer.Reset();
            State.ScreenshotHoldDurationSeconds = 0.0;
            State.ScreenshotHoldProgress = 0.0;
            ResetTouchState();
            State.DownwardHistory.Clear();
            return null;
        }

        TrackedHand hand1 = hands[0];
        TrackedHand hand2 = hands[1];

        Point2f index1 = hand1.SmoothedLandmarks2D[8]; // Index fingertip 1
        Point2f index2 = hand2.SmoothedLandmarks2D[8]; // Index fingertip 2
        Point2f thumb1 = hand1.SmoothedLandmarks2D[4]; // Thumb tip 1
        Point2f thumb2 = hand2.SmoothedLandmarks2D[4]; // Thumb tip 2
        Point2f palm1 = hand1.SmoothedLandmarks2D[9];  // Palm center 1
        Point2f palm2 = hand2.SmoothedLandmarks2D[9];  // Palm center 2

        double palmSize1 = hand1.Distance(0, 9);
        double palmSize2 = hand2.Distance(0, 9);
        double avgPalmSize = Math.Max(20.0, (palmSize1 + palmSize2) / 2.0);

        DateTime now = DateTime.Now;

        // =========================================================================
        // GESTURE 1: CAMERA-FRAME SCREENSHOT (Dual "L" Hands + 2.0s Double Touch Hold)
        // =========================================================================
        bool isHand1L = IsLPosture(hand1);
        bool isHand2L = IsLPosture(hand2);

        if (isHand1L && isHand2L)
        {
            float minX = Math.Min(Math.Min(thumb1.X, index1.X), Math.Min(thumb2.X, index2.X));
            float maxX = Math.Max(Math.Max(thumb1.X, index1.X), Math.Max(thumb2.X, index2.X));
            float minY = Math.Min(Math.Min(thumb1.Y, index1.Y), Math.Min(thumb2.Y, index2.Y));
            float maxY = Math.Max(Math.Max(thumb1.Y, index1.Y), Math.Max(thumb2.Y, index2.Y));

            State.IsCameraFrameActive = true;
            State.LiveCameraFrameRect = new Rect2f(minX, minY, Math.Max(20f, maxX - minX), Math.Max(20f, maxY - minY));

            double distIndex = Math.Sqrt(Math.Pow(index1.X - index2.X, 2) + Math.Pow(index1.Y - index2.Y, 2));
            double distThumb = Math.Sqrt(Math.Pow(thumb1.X - thumb2.X, 2) + Math.Pow(thumb1.Y - thumb2.Y, 2));
            double touchThreshold = avgPalmSize * 0.45;

            bool isIndexTouching = distIndex <= touchThreshold;
            bool isThumbTouching = distThumb <= touchThreshold;

            if (!State.IsScreenshotBlocked && isIndexTouching && isThumbTouching)
            {
                if (!State.ScreenshotHoldTimer.IsRunning)
                {
                    State.ScreenshotHoldTimer.Restart();
                }

                State.ScreenshotHoldDurationSeconds = State.ScreenshotHoldTimer.Elapsed.TotalSeconds;
                State.ScreenshotHoldProgress = Math.Clamp(State.ScreenshotHoldDurationSeconds / State.RequiredScreenshotHoldSeconds, 0.0, 1.0);

                if (State.ScreenshotHoldProgress >= 1.0)
                {
                    State.LastScreenshotTime = now;
                    State.LastCapturedFrameRect = State.LiveCameraFrameRect;
                    State.ScreenshotBlockTimer.Restart(); // 2.0s cooldown
                    State.ScreenshotHoldTimer.Reset();
                    State.ScreenshotHoldDurationSeconds = 0.0;
                    State.ScreenshotHoldProgress = 0.0;
                    State.IsCameraFrameActive = false;
                    State.LastAction = "SCREENSHOT";
                    State.LastFeedbackTime = now;
                    State.LastFeedbackCenter = new Point2f((minX + maxX) / 2f, (minY + maxY) / 2f);

                    ResetTouchState();
                    State.DownwardHistory.Clear();

                    return new TwoHandGestureDecision(TwoHandAction.Screenshot, IntPtr.Zero, State.LastCapturedFrameRect);
                }
            }
            else
            {
                State.ScreenshotHoldTimer.Reset();
                State.ScreenshotHoldDurationSeconds = 0.0;
                State.ScreenshotHoldProgress = 0.0;
            }
        }
        else
        {
            State.IsCameraFrameActive = false;
            State.ScreenshotHoldTimer.Reset();
            State.ScreenshotHoldDurationSeconds = 0.0;
            State.ScreenshotHoldProgress = 0.0;
        }

        // =========================================================================
        // GATING FOR WINDOW ACTIONS (Maximize / Minimize)
        // =========================================================================
        if (!State.IsWindowActive || State.InCooldown)
        {
            ResetTouchState();
            State.DownwardHistory.Clear();
            return null;
        }

        IntPtr targetHwnd = inputSink.LastFocusedHwnd;
        if (targetHwnd == IntPtr.Zero)
        {
            return null;
        }

        // =========================================================================
        // GESTURE 2: MAXIMIZE (Index-Touch -> Horizontal Expansion Apart)
        // (Suppressed for 2.0s post-screenshot to eliminate disambiguation conflicts)
        // =========================================================================
        if (!State.IsScreenshotBlocked)
        {
            double distFingertips = Math.Sqrt(Math.Pow(index1.X - index2.X, 2) + Math.Pow(index1.Y - index2.Y, 2));
            double touchThreshold = avgPalmSize * 0.38;

            if (!State.IsTouchActive)
            {
                if (distFingertips <= touchThreshold)
                {
                    State.ConsecutiveTouchFrames++;
                    if (State.ConsecutiveTouchFrames >= 2)
                    {
                        State.IsTouchActive = true;
                        State.TouchStartTime = now;
                        State.TouchAnchorDistance = Math.Abs(index1.X - index2.X);
                        State.TouchPoint1 = index1;
                        State.TouchPoint2 = index2;
                    }
                }
                else
                {
                    State.ConsecutiveTouchFrames = 0;
                }
            }
            else
            {
                double elapsedTouchSeconds = (now - State.TouchStartTime).TotalSeconds;

                if (elapsedTouchSeconds > 0.8)
                {
                    ResetTouchState();
                }
                else
                {
                    double currentHorizontalDist = Math.Abs(index1.X - index2.X);
                    double initialDist = Math.Max(15.0, State.TouchAnchorDistance);
                    double expansionRatio = currentHorizontalDist / initialDist;
                    double absoluteSeparation = currentHorizontalDist - initialDist;

                    if ((expansionRatio >= 1.40 || absoluteSeparation >= avgPalmSize * 0.70) && elapsedTouchSeconds <= 0.60)
                    {
                        State.LastTriggerTime = now;
                        State.LastAction = "MAXIMIZE";
                        State.LastFeedbackTime = now;
                        State.LastFeedbackCenter = new Point2f((index1.X + index2.X) / 2f, (index1.Y + index2.Y) / 2f);

                        ResetTouchState();
                        State.DownwardHistory.Clear();

                        return new TwoHandGestureDecision(TwoHandAction.Maximize, targetHwnd);
                    }
                }
            }
        }

        // =========================================================================
        // GESTURE 3: MINIMIZE (Dual Synchronous Fast Downward Swipe)
        // =========================================================================
        State.DownwardHistory.Enqueue((palm1, palm2, now));

        while (State.DownwardHistory.Count > 0 && (now - State.DownwardHistory.Peek().Time).TotalMilliseconds > 300)
        {
            State.DownwardHistory.Dequeue();
        }

        if (State.DownwardHistory.Count >= 3)
        {
            (Point2f oldestHand1, Point2f oldestHand2, DateTime oldestTime) = State.DownwardHistory.Peek();
            double timeDeltaSec = (now - oldestTime).TotalSeconds;

            if (timeDeltaSec > 0.04 && timeDeltaSec <= 0.30)
            {
                double dy1 = palm1.Y - oldestHand1.Y;
                double dy2 = palm2.Y - oldestHand2.Y;
                double dx1 = Math.Abs(palm1.X - oldestHand1.X);
                double dx2 = Math.Abs(palm2.X - oldestHand2.X);

                double requiredDownDistance = avgPalmSize * 0.40;

                bool isBothDownward = dy1 >= requiredDownDistance && dy2 >= requiredDownDistance;
                bool isPrimarilyVertical = dy1 > dx1 * 1.3 && dy2 > dx2 * 1.3;

                double palmCenterDist = Math.Sqrt(Math.Pow(palm1.X - palm2.X, 2) + Math.Pow(palm1.Y - palm2.Y, 2));
                bool isSideBySide = palmCenterDist >= avgPalmSize * 0.8 && palmCenterDist <= avgPalmSize * 4.0;

                if (isBothDownward && isPrimarilyVertical && isSideBySide)
                {
                    State.LastTriggerTime = now;
                    State.LastAction = "MINIMIZE";
                    State.LastFeedbackTime = now;
                    State.LastFeedbackCenter = new Point2f((palm1.X + palm2.X) / 2f, (palm1.Y + palm2.Y) / 2f);

                    ResetTouchState();
                    State.DownwardHistory.Clear();

                    return new TwoHandGestureDecision(TwoHandAction.Minimize, targetHwnd);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Helper evaluating whether an individual hand is forming an "L" posture (Thumb + Index extended at 48°-135°, other fingers curled).
    /// </summary>
    private static bool IsLPosture(TrackedHand hand)
    {
        Point2f p2 = hand.SmoothedLandmarks2D[2];
        Point2f p4 = hand.SmoothedLandmarks2D[4];
        Point2f p5 = hand.SmoothedLandmarks2D[5];
        Point2f p8 = hand.SmoothedLandmarks2D[8];

        Point2f thumbVec = new(p4.X - p2.X, p4.Y - p2.Y);
        Point2f indexVec = new(p8.X - p5.X, p8.Y - p5.Y);

        double dot = thumbVec.X * indexVec.X + thumbVec.Y * indexVec.Y;
        double magThumb = Math.Sqrt(thumbVec.X * thumbVec.X + thumbVec.Y * thumbVec.Y);
        double magIndex = Math.Sqrt(indexVec.X * indexVec.X + indexVec.Y * indexVec.Y);
        double cosAngle = dot / Math.Max(1e-5, magThumb * magIndex);
        double angleDeg = Math.Acos(Math.Clamp(cosAngle, -1.0, 1.0)) * 180.0 / Math.PI;

        bool isAngleL = angleDeg >= 48.0 && angleDeg <= 135.0;

        double distThumb0 = hand.Distance(4, 0);
        double distThumb2 = hand.Distance(2, 0);
        double distIndex0 = hand.Distance(8, 0);
        double distIndex5 = hand.Distance(5, 0);

        bool isThumbExtended = distThumb0 > distThumb2 * 1.05;
        bool isIndexExtended = distIndex0 > distIndex5 * 1.10;

        double distMiddle0 = hand.Distance(12, 0);
        double distMiddle9 = hand.Distance(9, 0);
        double distRing0 = hand.Distance(16, 0);
        double distRing13 = hand.Distance(13, 0);
        double distPinky0 = hand.Distance(20, 0);
        double distPinky17 = hand.Distance(17, 0);

        bool isMiddleCurled = distMiddle0 < distMiddle9 * 1.25;
        bool isRingCurled = distRing0 < distRing13 * 1.25;
        bool isPinkyCurled = distPinky0 < distPinky17 * 1.25;

        return (hand.Gesture == "L" || (isAngleL && isThumbExtended && isIndexExtended)) &&
               isMiddleCurled && isRingCurled && isPinkyCurled;
    }

    /// <summary>
    /// Resets active index-touch tracking metrics.
    /// </summary>
    public void ResetTouchState()
    {
        State.IsTouchActive = false;
        State.ConsecutiveTouchFrames = 0;
        State.TouchStartTime = DateTime.MinValue;
        State.TouchAnchorDistance = 0.0;
    }
}
