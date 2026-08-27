using System;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.TwoHand;

/// <summary>
/// Domain analyzer detecting dual synchronous downward hand swipes to minimize the active focused window.
/// <para>
/// <b>What it is:</b> Dual-hand vertical downward velocity and kinematics analyzer.
/// </para>
/// </summary>
public class TwoHandMinimizeDetector
{
    /// <summary>
    /// Evaluates tracked hands for the window minimize gesture.
    /// </summary>
    /// <param name="hand1">First tracked hand.</param>
    /// <param name="hand2">Second tracked hand.</param>
    /// <param name="state">Shared two-hand gesture state container.</param>
    /// <param name="avgPalmSize">Average palm size in pixels.</param>
    /// <returns><c>true</c> if a minimize gesture was triggered; otherwise, <c>false</c>.</returns>
    public bool Update(TrackedHand hand1, TrackedHand hand2, TwoHandGestureState state, double avgPalmSize)
    {
        Point2f palm1 = hand1.SmoothedLandmarks2D[9];
        Point2f palm2 = hand2.SmoothedLandmarks2D[9];

        DateTime now = DateTime.Now;

        state.DownwardHistory.Enqueue((palm1, palm2, now));

        while (state.DownwardHistory.Count > 0 && (now - state.DownwardHistory.Peek().Time).TotalMilliseconds > 300)
        {
            state.DownwardHistory.Dequeue();
        }

        if (state.DownwardHistory.Count >= 3)
        {
            (Point2f oldestHand1, Point2f oldestHand2, DateTime oldestTime) = state.DownwardHistory.Peek();
            double timeDeltaSec = (now - oldestTime).TotalSeconds;

            if (timeDeltaSec is > 0.04 and <= 0.30)
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
                    state.LastTriggerTime = now;
                    state.LastAction = "MINIMIZE";
                    state.LastFeedbackTime = now;
                    state.LastFeedbackCenter = new Point2f((palm1.X + palm2.X) / 2f, (palm1.Y + palm2.Y) / 2f);

                    state.DownwardHistory.Clear();
                    return true;
                }
            }
        }

        return false;
    }
}
