using System;
using OpenCvSharp;

namespace NEXA.Hand;

/// <summary>
/// Anatomical heuristic classifier evaluating skeletal joint extensions, thumb abduction angles, and inter-finger separation gaps.
/// <para>
/// <b>What it is:</b> Static domain classifier converting 21 3D hand skeleton landmarks into discrete gesture labels.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description>Evaluates individual finger extensions against knuckle and intermediate PIP joint thresholds.</description></item>
/// <item><description>Computes thumb abduction angles and L-sign ratios.</description></item>
/// <item><description>Detects Spock Vulcan salutes, Open Palm / Hand Up / Hand Down directions, Thumbs Up vs Fist, Peace, Rock, Pinch, and Pointing.</description></item>
/// </list>
/// </para>
/// </summary>
public static class HandGestureClassifier
{
    /// <summary>
    /// Classifies the anatomical posture of a tracked hand into a recognized gesture label.
    /// </summary>
    /// <param name="hand">The tracked hand instance containing smoothed 2D/3D landmarks.</param>
    /// <returns>A string identifying the classified gesture.</returns>
    public static string Classify(TrackedHand hand)
    {
        Point2f[] lm = hand.SmoothedLandmarks2D;
        double palmSize = hand.Distance(0, 9); // Distance from wrist (0) to middle finger MCP knuckle (9)
        if (palmSize <= 1.0)
        {
            return "Hand";
        }

        // 1. Finger Extension Checks: Fingertip must be significantly farther from wrist and knuckle than the intermediate PIP joint
        bool indexExt = hand.Distance(8, 0) > hand.Distance(6, 0) * 1.12 && hand.Distance(8, 5) > hand.Distance(6, 5) * 1.25;
        bool middleExt = hand.Distance(12, 0) > hand.Distance(10, 0) * 1.12 && hand.Distance(12, 9) > hand.Distance(10, 9) * 1.25;
        bool ringExt = hand.Distance(16, 0) > hand.Distance(14, 0) * 1.12 && hand.Distance(16, 13) > hand.Distance(14, 13) * 1.25;
        bool pinkyExt = hand.Distance(20, 0) > hand.Distance(18, 0) * 1.12 && hand.Distance(20, 17) > hand.Distance(18, 17) * 1.25;

        // 2. Thumb Abduction Checks: Distance from thumb tip (4) to index (5) and middle (9) knuckles
        double thumbToKnuckle5 = hand.Distance(4, 5);
        double thumbToKnuckle9 = hand.Distance(4, 9);

        bool thumbStretchedOut = thumbToKnuckle5 > palmSize * 0.58 && thumbToKnuckle9 > palmSize * 0.68;
        bool thumbWideL = thumbToKnuckle5 > palmSize * 0.72 && thumbToKnuckle9 > palmSize * 0.85;

        // 3. Inter-finger angular separation gaps
        double indexMiddleGap = hand.Distance(8, 12);
        double middleRingGap = hand.Distance(12, 16);
        double ringPinkyGap = hand.Distance(16, 20);

        bool spockSplit = middleRingGap > indexMiddleGap * 1.5 && middleRingGap > ringPinkyGap * 1.5;

        // --- GESTURE HIERARCHY EVALUATION ---

        // A. Spock (Vulcan Salute)
        if (thumbStretchedOut && indexExt && middleExt && ringExt && pinkyExt && spockSplit)
        {
            return "Spock";
        }

        // B. Open Hand: Hand Up vs Hand Down
        bool allFingersExtended = indexExt && middleExt && ringExt && pinkyExt;
        if (allFingersExtended || (indexExt && middleExt && ringExt))
        {
            bool fingersPointingUp = lm[12].Y < (lm[0].Y - palmSize * 0.5) && lm[12].Y < lm[9].Y;
            bool fingersPointingDown = lm[12].Y > lm[9].Y || lm[8].Y > lm[5].Y || lm[12].Y > (lm[0].Y - palmSize * 0.2);

            if (fingersPointingUp)
            {
                return "Hand Up";
            }

            if (fingersPointingDown)
            {
                return "Hand Down";
            }

            return "Open Palm";
        }

        // C. 4 Fingers Folded (Index, Middle, Ring, Pinky curled into palm)
        if (!indexExt && !middleExt && !ringExt && !pinkyExt)
        {
            double thumbToIndexTip = hand.Distance(4, 8);
            bool thumbPointsUp = lm[4].Y < (lm[2].Y - palmSize * 0.15);

            if (thumbStretchedOut && thumbToIndexTip > palmSize * 0.50 && thumbPointsUp)
            {
                return "Thumbs Up";
            }

            // Otherwise, clenched fist
            return "Fist";
        }

        // D. Peace / Victory (Index and Middle extended)
        if (indexExt && middleExt && !ringExt && !pinkyExt)
        {
            return "Peace";
        }

        // E. Rock / Spider-Man (Index and Pinky extended)
        if (indexExt && pinkyExt && !middleExt && !ringExt)
        {
            return "Rock";
        }

        // F. Pinch Detection (Thumb tip and Index fingertip in contact)
        double thumbIndexDist = hand.Distance(4, 8);
        if (thumbIndexDist < palmSize * 0.25)
        {
            if (!middleExt && !ringExt && !pinkyExt)
            {
                return "Pinch Closed";
            }

            return "Pinch";
        }

        // G. Single Pointer Finger Active (Middle, Ring, Pinky folded)
        if (indexExt && !middleExt && !ringExt && !pinkyExt)
        {
            if (thumbWideL)
            {
                return "L";
            }

            return "Pointing";
        }

        return "Tracking";
    }
}
