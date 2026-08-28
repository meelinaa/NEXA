using System;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.TwoHand;

/// <summary>
/// Domain analyzer detecting the two-hand Clap / Prayer gesture to trigger global media play/pause.
/// <para>
/// <b>What it is:</b> Spatial multi-hand proximity and velocity kinematics analyzer.
/// </para>
/// </summary>
public class ClapPlayPauseDetector
{
    /// <summary>
    /// Evaluates tracked hands for the Clap / Prayer gesture.
    /// </summary>
    /// <param name="hand1">First tracked hand.</param>
    /// <param name="hand2">Second tracked hand.</param>
    /// <param name="state">Shared two-hand gesture state container.</param>
    /// <param name="avgPalmSize">Average palm size in pixels.</param>
    /// <returns><c>true</c> if a clap/prayer gesture was triggered; otherwise, <c>false</c>.</returns>
    public bool Update(TrackedHand hand1, TrackedHand hand2, TwoHandGestureState state, double avgPalmSize)
    {
        Point2f wrist1 = hand1.SmoothedLandmarks2D[0];
        Point2f wrist2 = hand2.SmoothedLandmarks2D[0];
        Point2f palm1 = hand1.SmoothedLandmarks2D[9];
        Point2f palm2 = hand2.SmoothedLandmarks2D[9];

        DateTime now = DateTime.Now;

        bool isOpenPalm1 = IsOpenPalmPosture(hand1);
        bool isOpenPalm2 = IsOpenPalmPosture(hand2);

        if (isOpenPalm1 && isOpenPalm2)
        {
            double distPalms = Math.Sqrt(Math.Pow(palm1.X - palm2.X, 2) + Math.Pow(palm1.Y - palm2.Y, 2));
            double distWrists = Math.Sqrt(Math.Pow(wrist1.X - wrist2.X, 2) + Math.Pow(wrist1.Y - wrist2.Y, 2));
            double clapThreshold = avgPalmSize * 0.50;

            if (!state.IsMediaPlayPauseInCooldown && distPalms <= clapThreshold && distWrists <= avgPalmSize * 0.70)
            {
                state.ConsecutiveClapFrames++;
                if (state.ConsecutiveClapFrames >= 2)
                {
                    state.MediaPlayPauseCooldownTimer.Restart();
                    state.ConsecutiveClapFrames = 0;
                    state.LastMediaPlayPauseTime = now;
                    state.LastMediaFeedbackCenter = new Point2f((palm1.X + palm2.X) / 2f, (palm1.Y + palm2.Y) / 2f);
                    state.LastAction = "PLAY / PAUSE";
                    state.LastFeedbackTime = now;
                    state.LastFeedbackCenter = state.LastMediaFeedbackCenter;

                    return true;
                }
            }
            else
            {
                state.ConsecutiveClapFrames = 0;
            }
        }
        else
        {
            state.ConsecutiveClapFrames = 0;
        }

        return false;
    }

    /// <summary>
    /// Helper evaluating whether an individual hand is in Open Palm posture with all 5 extended fingers.
    /// </summary>
    public static bool IsOpenPalmPosture(TrackedHand hand)
    {
        if (hand.Gesture == "Open Palm")
            return true;

        double distThumb0 = hand.Distance(4, 0);
        double distThumb2 = hand.Distance(2, 0);
        double distIndex0 = hand.Distance(8, 0);
        double distIndex5 = hand.Distance(5, 0);
        double distMiddle0 = hand.Distance(12, 0);
        double distMiddle9 = hand.Distance(9, 0);
        double distRing0 = hand.Distance(16, 0);
        double distRing13 = hand.Distance(13, 0);
        double distPinky0 = hand.Distance(20, 0);
        double distPinky17 = hand.Distance(17, 0);

        bool isThumbExtended = distThumb0 > distThumb2 * 1.05;
        bool isIndexExtended = distIndex0 > distIndex5 * 1.10;
        bool isMiddleExtended = distMiddle0 > distMiddle9 * 1.10;
        bool isRingExtended = distRing0 > distRing13 * 1.10;
        bool isPinkyExtended = distPinky0 > distPinky17 * 1.10;

        return isThumbExtended && isIndexExtended && isMiddleExtended && isRingExtended && isPinkyExtended;
    }
}
