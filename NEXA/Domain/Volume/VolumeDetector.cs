using System;
using NEXA.Abstractions;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Volume;

/// <summary>
/// Domain-level analyzer detecting L-gesture activation gates and continuous rotary hand angle tilting for system volume adjustment.
/// <para>
/// <b>What it is:</b> A continuous angular dial analyzer translating hand orientation into master audio volume scalars.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="number">
/// <item><description><b>L-Gesture Recognition:</b> Verifies that Thumb and Index fingers form a 55°-130° angle while Middle, Ring, and Pinky fingers remain curled into the palm.</description></item>
/// <item><description><b>Rotary Angle Estimation:</b> Tracks the angular orientation of the Index finger vector relative to the locked baseline angle.</description></item>
/// <item><description><b>Continuous Dial Mapping:</b> Maps clock-wise tilt to volume increments and counter-clockwise tilt to volume decrements with a +/-2.0° deadzone.</description></item>
/// <item><description><b>Adaptive Exponential Smoothing:</b> Smooths volume changes to prevent abrupt audio stepping or jitter.</description></item>
/// </list>
/// </para>
/// </summary>
public class VolumeDetector
{
    /// <summary>
    /// Gets the internal state container tracking baseline angles, volume levels, and dial geometry.
    /// </summary>
    public VolumeState State { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether rotary volume detection is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Evaluates tracked hand data for the current frame to compute master audio volume adjustments.
    /// </summary>
    /// <param name="hand">The tracked hand instance.</param>
    /// <param name="audioSink">The audio adapter used to query and update Windows master volume.</param>
    /// <returns>A tuple containing a boolean indicating if volume control is active and the target volume scalar [0.0 to 1.0].</returns>
    public (bool isActive, float targetVolume) Update(TrackedHand? hand, IAudioSink audioSink)
    {
        if (!Enabled || hand == null)
        {
            Reset();
            return (false, 0.5f);
        }

        Point2f p0 = hand.SmoothedLandmarks2D[0];   // Wrist
        Point2f p2 = hand.SmoothedLandmarks2D[2];   // Thumb MCP
        Point2f p4 = hand.SmoothedLandmarks2D[4];   // Thumb Tip
        Point2f p5 = hand.SmoothedLandmarks2D[5];   // Index MCP
        Point2f p8 = hand.SmoothedLandmarks2D[8];   // Index Tip
        Point2f p9 = hand.SmoothedLandmarks2D[9];   // Middle MCP
        Point2f p12 = hand.SmoothedLandmarks2D[12]; // Middle Tip
        Point2f p13 = hand.SmoothedLandmarks2D[13]; // Ring MCP
        Point2f p16 = hand.SmoothedLandmarks2D[16]; // Ring Tip
        Point2f p17 = hand.SmoothedLandmarks2D[17]; // Pinky MCP
        Point2f p20 = hand.SmoothedLandmarks2D[20]; // Pinky Tip

        State.DialCenter = p9;
        State.IndexTip = p8;
        State.ThumbTip = p4;

        // 1. Check L-Gesture Finger Geometry
        // Vector Thumb (2 -> 4) and Vector Index (5 -> 8)
        Point2f thumbVec = new(p4.X - p2.X, p4.Y - p2.Y);
        Point2f indexVec = new(p8.X - p5.X, p8.Y - p5.Y);

        double dot = thumbVec.X * indexVec.X + thumbVec.Y * indexVec.Y;
        double magThumb = Math.Sqrt(thumbVec.X * thumbVec.X + thumbVec.Y * thumbVec.Y);
        double magIndex = Math.Sqrt(indexVec.X * indexVec.X + indexVec.Y * indexVec.Y);
        double cosAngle = dot / Math.Max(1e-5, magThumb * magIndex);
        double angleDeg = Math.Acos(Math.Clamp(cosAngle, -1.0, 1.0)) * 180.0 / Math.PI;

        bool isAngleL = angleDeg >= 48.0 && angleDeg <= 135.0;

        // Verify Index and Thumb are extended
        double distThumb0 = hand.Distance(4, 0);
        double distThumb2 = hand.Distance(2, 0);
        double distIndex0 = hand.Distance(8, 0);
        double distIndex5 = hand.Distance(5, 0);
        bool isThumbExtended = distThumb0 > distThumb2 * 1.05;
        bool isIndexExtended = distIndex0 > distIndex5 * 1.10;

        // Verify Middle, Ring, Pinky are curled inward
        double distMiddle0 = hand.Distance(12, 0);
        double distMiddle9 = hand.Distance(9, 0);
        double distRing0 = hand.Distance(16, 0);
        double distRing13 = hand.Distance(13, 0);
        double distPinky0 = hand.Distance(20, 0);
        double distPinky17 = hand.Distance(17, 0);

        bool isMiddleCurled = distMiddle0 < distMiddle9 * 1.25;
        bool isRingCurled = distRing0 < distRing13 * 1.25;
        bool isPinkyCurled = distPinky0 < distPinky17 * 1.25;

        bool isLPosture = (hand.Gesture == "L" || (isAngleL && isThumbExtended && isIndexExtended)) &&
                          isMiddleCurled && isRingCurled && isPinkyCurled;

        if (!isLPosture)
        {
            Reset();
            return (false, State.SmoothedVolume);
        }

        // 2. Compute Index Finger Rotary Angle
        // Angle in degrees: 0 is horizontal right, -90 is straight up, +90 is straight down
        double currentAngle = Math.Atan2(indexVec.Y, indexVec.X) * 180.0 / Math.PI;
        State.LiveAngle = currentAngle;

        // 3. Gesture Initiation: Lock Baseline Angle & Capture Starting System Volume
        if (!State.IsActive)
        {
            State.IsActive = true;
            State.BaselineAngle = currentAngle;
            State.BaselineVolume = audioSink.GetMasterVolume();
            State.SmoothedVolume = State.BaselineVolume;
            State.TargetVolume = State.BaselineVolume;
            State.AngleDelta = 0.0;
            return (true, State.SmoothedVolume);
        }

        // 4. Compute Angular Delta with Circular Wrap-Around Protection
        double delta = currentAngle - State.BaselineAngle;
        while (delta > 180.0) delta -= 360.0;
        while (delta < -180.0) delta += 360.0;

        // Deadzone: ignore micro-wobbles within +/- 2.0 degrees
        if (Math.Abs(delta) < 2.0)
        {
            delta = 0.0;
        }

        State.AngleDelta = delta;

        // Clockwise rotation (tilting right) increases volume; Counter-clockwise decreases volume
        // Sensitivity: 75 degrees rotation maps to 100% full-scale volume range
        double volumeChange = delta / 75.0;
        float rawTargetVolume = (float)Math.Clamp(State.BaselineVolume + volumeChange, 0.0, 1.0);
        State.TargetVolume = rawTargetVolume;

        // Adaptive smoothing
        float diff = rawTargetVolume - State.SmoothedVolume;
        State.SmoothedVolume += diff * 0.40f;

        return (true, State.SmoothedVolume);
    }

    /// <summary>
    /// Resets the rotary state machine upon gesture release.
    /// </summary>
    public void Reset()
    {
        State.IsActive = false;
        State.BaselineAngle = 0.0;
        State.LiveAngle = 0.0;
        State.AngleDelta = 0.0;
    }
}
