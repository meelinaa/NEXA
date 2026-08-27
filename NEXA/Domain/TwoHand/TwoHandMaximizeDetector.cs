using System;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.TwoHand;

/// <summary>
/// Domain analyzer detecting index-fingertip contact followed by rapid horizontal expansion to maximize the active focused window.
/// <para>
/// <b>What it is:</b> Dual-index touch and outward expansion gesture detector.
/// </para>
/// </summary>
public class TwoHandMaximizeDetector
{
    /// <summary>
    /// Evaluates tracked hands for the window maximize gesture.
    /// </summary>
    /// <param name="hand1">First tracked hand.</param>
    /// <param name="hand2">Second tracked hand.</param>
    /// <param name="state">Shared two-hand gesture state container.</param>
    /// <param name="avgPalmSize">Average palm size in pixels.</param>
    /// <returns><c>true</c> if a maximize gesture was triggered; otherwise, <c>false</c>.</returns>
    public bool Update(TrackedHand hand1, TrackedHand hand2, TwoHandGestureState state, double avgPalmSize)
    {
        if (state.IsScreenshotBlocked)
            return false;

        Point2f index1 = hand1.SmoothedLandmarks2D[8];
        Point2f index2 = hand2.SmoothedLandmarks2D[8];

        DateTime now = DateTime.Now;

        double distFingertips = Math.Sqrt(Math.Pow(index1.X - index2.X, 2) + Math.Pow(index1.Y - index2.Y, 2));
        double touchThreshold = avgPalmSize * 0.38;

        if (!state.IsTouchActive)
        {
            if (distFingertips <= touchThreshold)
            {
                state.ConsecutiveTouchFrames++;
                if (state.ConsecutiveTouchFrames >= 2)
                {
                    state.IsTouchActive = true;
                    state.TouchStartTime = now;
                    state.TouchAnchorDistance = Math.Abs(index1.X - index2.X);
                    state.TouchPoint1 = index1;
                    state.TouchPoint2 = index2;
                }
            }
            else
            {
                state.ConsecutiveTouchFrames = 0;
            }
        }
        else
        {
            double elapsedTouchSeconds = (now - state.TouchStartTime).TotalSeconds;

            if (elapsedTouchSeconds > 0.8)
            {
                ResetTouchState(state);
            }
            else
            {
                double currentHorizontalDist = Math.Abs(index1.X - index2.X);
                double initialDist = Math.Max(15.0, state.TouchAnchorDistance);
                double expansionRatio = currentHorizontalDist / initialDist;
                double absoluteSeparation = currentHorizontalDist - initialDist;

                if ((expansionRatio >= 1.40 || absoluteSeparation >= avgPalmSize * 0.70) && elapsedTouchSeconds <= 0.60)
                {
                    state.LastTriggerTime = now;
                    state.LastAction = "MAXIMIZE";
                    state.LastFeedbackTime = now;
                    state.LastFeedbackCenter = new Point2f((index1.X + index2.X) / 2f, (index1.Y + index2.Y) / 2f);

                    ResetTouchState(state);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Resets active index-touch tracking metrics.
    /// </summary>
    /// <param name="state">Shared two-hand gesture state container.</param>
    public static void ResetTouchState(TwoHandGestureState state)
    {
        state.IsTouchActive = false;
        state.ConsecutiveTouchFrames = 0;
        state.TouchStartTime = DateTime.MinValue;
        state.TouchAnchorDistance = 0.0;
    }
}
