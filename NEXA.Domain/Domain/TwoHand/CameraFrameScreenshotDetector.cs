using System;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.TwoHand;

/// <summary>
/// Domain analyzer detecting dual "L" hands framing a camera viewfinder and holding contact for 2.0s to trigger a fullscreen screenshot.
/// <para>
/// <b>What it is:</b> Viewfinder geometry and double-touch hold detector for screenshot capture.
/// </para>
/// </summary>
public class CameraFrameScreenshotDetector
{
    /// <summary>
    /// Evaluates tracked hands for the Camera-Frame Screenshot gesture.
    /// </summary>
    /// <param name="hand1">First tracked hand.</param>
    /// <param name="hand2">Second tracked hand.</param>
    /// <param name="state">Shared two-hand gesture state container.</param>
    /// <param name="avgPalmSize">Average palm size in pixels.</param>
    /// <returns><c>true</c> if a screenshot gesture was triggered; otherwise, <c>false</c>.</returns>
    public bool Update(TrackedHand hand1, TrackedHand hand2, TwoHandGestureState state, double avgPalmSize)
    {
        Point2f index1 = hand1.SmoothedLandmarks2D[8];
        Point2f index2 = hand2.SmoothedLandmarks2D[8];
        Point2f thumb1 = hand1.SmoothedLandmarks2D[4];
        Point2f thumb2 = hand2.SmoothedLandmarks2D[4];

        DateTime now = DateTime.Now;

        bool isHand1L = IsLPosture(hand1);
        bool isHand2L = IsLPosture(hand2);

        if (isHand1L && isHand2L)
        {
            float minX = Math.Min(Math.Min(thumb1.X, index1.X), Math.Min(thumb2.X, index2.X));
            float maxX = Math.Max(Math.Max(thumb1.X, index1.X), Math.Max(thumb2.X, index2.X));
            float minY = Math.Min(Math.Min(thumb1.Y, index1.Y), Math.Min(thumb2.Y, index2.Y));
            float maxY = Math.Max(Math.Max(thumb1.Y, index1.Y), Math.Max(thumb2.Y, index2.Y));

            state.IsCameraFrameActive = true;
            state.LiveCameraFrameRect = new Rect2f(minX, minY, Math.Max(20f, maxX - minX), Math.Max(20f, maxY - minY));

            double distIndex = Math.Sqrt(Math.Pow(index1.X - index2.X, 2) + Math.Pow(index1.Y - index2.Y, 2));
            double distThumb = Math.Sqrt(Math.Pow(thumb1.X - thumb2.X, 2) + Math.Pow(thumb1.Y - thumb2.Y, 2));
            double distIndex1Thumb2 = Math.Sqrt(Math.Pow(index1.X - thumb2.X, 2) + Math.Pow(index1.Y - thumb2.Y, 2));
            double distThumb1Index2 = Math.Sqrt(Math.Pow(thumb1.X - index2.X, 2) + Math.Pow(thumb1.Y - index2.Y, 2));

            // Forgiving touch threshold (~75-90px) accommodating natural finger gap distances
            double touchThreshold = Math.Max(75.0, avgPalmSize * 0.75);

            // Accept any valid framing contact (Index-Index & Thumb-Thumb OR Index-Thumb & Thumb-Index OR direct fingertip touch)
            bool isTouching = (distIndex <= touchThreshold && distThumb <= touchThreshold) ||
                              (distIndex1Thumb2 <= touchThreshold && distThumb1Index2 <= touchThreshold) ||
                              (distIndex <= touchThreshold || distThumb <= touchThreshold || distIndex1Thumb2 <= touchThreshold || distThumb1Index2 <= touchThreshold);

            if (!state.IsScreenshotBlocked && isTouching)
            {
                if (!state.ScreenshotHoldTimer.IsRunning)
                {
                    state.ScreenshotHoldTimer.Restart();
                }

                state.ScreenshotHoldDurationSeconds = state.ScreenshotHoldTimer.Elapsed.TotalSeconds;
                state.ScreenshotHoldProgress = Math.Clamp(state.ScreenshotHoldDurationSeconds / state.RequiredScreenshotHoldSeconds, 0.0, 1.0);

                if (state.ScreenshotHoldProgress >= 1.0)
                {
                    state.LastScreenshotTime = now;
                    state.LastCapturedFrameRect = state.LiveCameraFrameRect;
                    state.ScreenshotBlockTimer.Restart(); // 2.0s cooldown
                    state.ScreenshotHoldTimer.Reset();
                    state.ScreenshotHoldDurationSeconds = 0.0;
                    state.ScreenshotHoldProgress = 0.0;
                    state.IsCameraFrameActive = false;
                    state.LastAction = "SCREENSHOT";
                    state.LastFeedbackTime = now;
                    state.LastFeedbackCenter = new Point2f((minX + maxX) / 2f, (minY + maxY) / 2f);

                    return true;
                }
            }
            else
            {
                state.ScreenshotHoldTimer.Reset();
                state.ScreenshotHoldDurationSeconds = 0.0;
                state.ScreenshotHoldProgress = 0.0;
            }
        }
        else
        {
            state.IsCameraFrameActive = false;
            state.ScreenshotHoldTimer.Reset();
            state.ScreenshotHoldDurationSeconds = 0.0;
            state.ScreenshotHoldProgress = 0.0;
        }

        return false;
    }

    /// <summary>
    /// Helper evaluating whether an individual hand is forming an "L" posture.
    /// </summary>
    public static bool IsLPosture(TrackedHand hand)
    {
        if (hand.Gesture == "L") return true;

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

        bool isAngleL = angleDeg is >= 38.0 and <= 145.0;

        double distThumb0 = hand.Distance(4, 0);
        double distThumb2 = hand.Distance(2, 0);
        double distIndex0 = hand.Distance(8, 0);
        double distIndex5 = hand.Distance(5, 0);

        bool isThumbExtended = distThumb0 > distThumb2 * 0.95;
        bool isIndexExtended = distIndex0 > distIndex5 * 1.05;

        // Ring and Pinky should not be fully extended like open palm
        double distRing0 = hand.Distance(16, 0);
        double distRing13 = hand.Distance(13, 0);
        double distPinky0 = hand.Distance(20, 0);
        double distPinky17 = hand.Distance(17, 0);

        bool ringExt = distRing0 > distRing13 * 1.25;
        bool pinkyExt = distPinky0 > distPinky17 * 1.25;

        return (isAngleL && isThumbExtended && isIndexExtended) && (!ringExt || !pinkyExt);
    }
}
