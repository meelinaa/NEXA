using System;
using System.Collections.Generic;
using NEXA.Adapters.Output;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.TwoHand;

/// <summary>
/// Domain-level analyzer evaluating two-hand Maximize and Minimize gestures within a 3-second post-fist window.
/// <para>
/// <b>What it is:</b> A multi-hand spatial gesture analyzer coordinating window sizing interactions.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="number">
/// <item><description><b>Window Gating:</b> Enforces a strict 3.0-second time window following a fist-grab release.</description></item>
/// <item><description><b>Maximize Detection:</b> Detects index fingertip touch followed by rapid horizontal expansion (>40% increase within 600ms).</description></item>
/// <item><description><b>Minimize Detection:</b> Detects dual side-by-side hands executing a synchronous fast downward swipe (>40px in &lt;300ms).</description></item>
/// <item><description><b>Cooldown Protection:</b> Enforces a 750ms refractory period following any action.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Why it is used:</b> Provides intuitive two-hand window controls without accidental gesture misfires.
/// </para>
/// </summary>
public class TwoHandGestureDetector
{
    /// <summary>
    /// Gets the internal state machine tracking timing, touch anchors, and velocity queues.
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
    /// Evaluates tracked hands for the current frame to detect Maximize or Minimize gestures.
    /// </summary>
    /// <param name="hands">The list of active tracked hands.</param>
    /// <param name="inputSink">The output adapter holding the focused window handle.</param>
    /// <returns>A <see cref="TwoHandGestureDecision"/> if an action was triggered; otherwise, <c>null</c>.</returns>
    public TwoHandGestureDecision? Update(List<TrackedHand>? hands, IInputSink inputSink)
    {
        if (!Enabled || hands == null || hands.Count != 2)
        {
            ResetTouchState();
            State.DownwardHistory.Clear();
            return null;
        }

        // Gating Check 1: Must be within 3.0s window after fist release
        if (!State.IsWindowActive)
        {
            ResetTouchState();
            State.DownwardHistory.Clear();
            return null;
        }

        // Gating Check 2: Must not be in post-trigger cooldown
        if (State.InCooldown)
        {
            ResetTouchState();
            State.DownwardHistory.Clear();
            return null;
        }

        // Gating Check 3: A valid target window must be known
        IntPtr targetHwnd = inputSink.LastFocusedHwnd;
        if (targetHwnd == IntPtr.Zero)
        {
            return null;
        }

        TrackedHand hand1 = hands[0];
        TrackedHand hand2 = hands[1];

        Point2f index1 = hand1.SmoothedLandmarks2D[8]; // Index fingertip 1
        Point2f index2 = hand2.SmoothedLandmarks2D[8]; // Index fingertip 2
        Point2f palm1 = hand1.SmoothedLandmarks2D[9];  // Palm center 1
        Point2f palm2 = hand2.SmoothedLandmarks2D[9];  // Palm center 2

        double palmSize1 = hand1.Distance(0, 9);
        double palmSize2 = hand2.Distance(0, 9);
        double avgPalmSize = Math.Max(20.0, (palmSize1 + palmSize2) / 2.0);

        DateTime now = DateTime.Now;

        // =========================================================================
        // GESTURE 1: MAXIMIZE (Index-Touch -> Horizontal Expansion Apart)
        // =========================================================================
        double touchDist = Math.Sqrt(Math.Pow(index1.X - index2.X, 2) + Math.Pow(index1.Y - index2.Y, 2));
        double maxTouchThreshold = Math.Max(28.0, avgPalmSize * 0.38);

        if (touchDist <= maxTouchThreshold)
        {
            State.ConsecutiveTouchFrames++;
            if (State.ConsecutiveTouchFrames >= 2)
            {
                if (!State.IsTouchActive)
                {
                    State.IsTouchActive = true;
                    State.TouchStartTime = now;
                    State.TouchAnchorDistance = Math.Max(15.0, touchDist);
                    State.TouchPoint1 = index1;
                    State.TouchPoint2 = index2;
                }
            }
        }
        else
        {
            State.ConsecutiveTouchFrames = 0;
        }

        if (State.IsTouchActive)
        {
            double elapsedMs = (now - State.TouchStartTime).TotalMilliseconds;

            if (elapsedMs <= 600.0)
            {
                double currentDist = touchDist;
                double growth = currentDist - State.TouchAnchorDistance;
                double dy = Math.Abs(index1.Y - index2.Y);

                // Expansion condition: Distance grew by >=45% and at least 35px, with horizontal orientation
                if (currentDist >= State.TouchAnchorDistance * 1.45 && growth > 35.0 && dy < avgPalmSize * 0.70)
                {
                    State.LastAction = "MAXIMIZE";
                    State.LastTriggerTime = now;
                    State.LastFeedbackTime = now;
                    State.LastFeedbackCenter = new Point2f((index1.X + index2.X) * 0.5f, (index1.Y + index2.Y) * 0.5f);

                    ResetTouchState();
                    State.DownwardHistory.Clear();
                    return new TwoHandGestureDecision(TwoHandAction.Maximize, targetHwnd);
                }
            }
            else
            {
                // Touch window expired without sufficient expansion
                ResetTouchState();
            }
        }

        // =========================================================================
        // GESTURE 2: MINIMIZE (Side-by-Side -> Synchronous Downward Swipe)
        // =========================================================================
        if (!State.IsTouchActive)
        {
            // Verify hands are in side-by-side posture
            double horizontalGap = Math.Abs(palm1.X - palm2.X);
            double verticalDiff = Math.Abs(palm1.Y - palm2.Y);

            if (horizontalGap > avgPalmSize * 0.75 && verticalDiff < avgPalmSize * 0.65)
            {
                State.DownwardHistory.Enqueue((palm1, palm2, now));

                // Purge samples older than 300ms
                while (State.DownwardHistory.Count > 0 && (now - State.DownwardHistory.Peek().Time).TotalMilliseconds > 300.0)
                {
                    State.DownwardHistory.Dequeue();
                }

                if (State.DownwardHistory.Count >= 3)
                {
                    (Point2f Hand1, Point2f Hand2, DateTime Time) oldest = State.DownwardHistory.Peek();
                    double deltaY1 = palm1.Y - oldest.Hand1.Y;
                    double deltaY2 = palm2.Y - oldest.Hand2.Y;
                    double deltaX1 = Math.Abs(palm1.X - oldest.Hand1.X);
                    double deltaX2 = Math.Abs(palm2.X - oldest.Hand2.X);

                    // Downward swipe condition: both hands drop >40px simultaneously with minimal X drift
                    if (deltaY1 > 40.0 && deltaY2 > 40.0 && deltaX1 < 35.0 && deltaX2 < 35.0)
                    {
                        State.LastAction = "MINIMIZE";
                        State.LastTriggerTime = now;
                        State.LastFeedbackTime = now;
                        State.LastFeedbackCenter = new Point2f((palm1.X + palm2.X) * 0.5f, (palm1.Y + palm2.Y) * 0.5f);

                        ResetTouchState();
                        State.DownwardHistory.Clear();
                        return new TwoHandGestureDecision(TwoHandAction.Minimize, targetHwnd);
                    }
                }
            }
            else
            {
                State.DownwardHistory.Clear();
            }
        }

        return null;
    }

    /// <summary>
    /// Resets active touch anchor tracking.
    /// </summary>
    private void ResetTouchState()
    {
        State.IsTouchActive = false;
        State.ConsecutiveTouchFrames = 0;
        State.TouchStartTime = DateTime.MinValue;
        State.TouchAnchorDistance = 0.0;
    }
}
