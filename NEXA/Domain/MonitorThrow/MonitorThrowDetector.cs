using System;
using System.Collections.Generic;
using NEXA.Adapters.Output;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.MonitorThrow;

/// <summary>
/// Domain-level analyzer detecting edge-on ("Knife Hand") horizontal swipe gestures to transfer windows across desktop monitors.
/// <para>
/// <b>What it is:</b> A spatial gesture analyzer combining hand surface normal estimation with horizontal velocity kinematics.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="number">
/// <item><description><b>Hand Orientation Estimation:</b> Computes the 2D projection ratio between Index MCP [5] and Pinky MCP [17] relative to Palm size [0-9]. When the hand is turned side-on toward the camera, this ratio drops below 0.45.</description></item>
/// <item><description><b>Swipe Kinematics:</b> Analyzes rapid horizontal displacement (&gt;55px in &lt;250ms) with minimal vertical variance (&lt;45px).</description></item>
/// <item><description><b>Cooldown Protection:</b> Enforces an 800ms refractory period following every transfer to prevent runaway display cycles.</description></item>
/// </list>
/// </para>
/// </summary>
public class MonitorThrowDetector
{
    /// <summary>
    /// Gets the internal state container tracking posture metrics and velocity queues.
    /// </summary>
    public MonitorThrowState State { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether monitor throw gesture detection is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Threshold ratio between knuckle distance (Index MCP to Pinky MCP) and palm size below which the hand is classified as edge-on.
    /// </summary>
    public const double EdgeOnThreshold = 0.45;

    /// <summary>
    /// Minimum horizontal pixel displacement required within the velocity window.
    /// </summary>
    public const double MinSwipeDistance = 55.0;

    /// <summary>
    /// Maximum allowed vertical drift during a valid horizontal throw swipe.
    /// </summary>
    public const double MaxVerticalDrift = 45.0;

    /// <summary>
    /// Evaluates tracked hand data for the current frame to detect multi-monitor throw gestures.
    /// </summary>
    /// <param name="hand">The tracked hand instance.</param>
    /// <param name="inputSink">The output adapter holding the focused target window.</param>
    /// <returns>A <see cref="MonitorThrowDecision"/> if a transfer was triggered; otherwise, <c>null</c>.</returns>
    public MonitorThrowDecision? Update(TrackedHand? hand, IInputSink inputSink)
    {
        if (!Enabled || hand == null || State.InCooldown)
        {
            State.History.Clear();
            State.IsEdgeOnPosture = false;
            return null;
        }

        IntPtr targetHwnd = inputSink.LastFocusedHwnd;
        if (targetHwnd == IntPtr.Zero)
        {
            State.History.Clear();
            return null;
        }

        double palmSize = hand.Distance(0, 9);
        double knuckleDist = hand.Distance(5, 17); // Distance between Index MCP and Pinky MCP
        double knuckleRatio = knuckleDist / Math.Max(1.0, palmSize);

        State.KnuckleCompressionRatio = knuckleRatio;

        // Verify edge-on posture: Knuckles compressed in 2D projection and hand is not a fist
        bool isNotFist = hand.Gesture != "Fist" && hand.Gesture != "Pinch Closed";
        bool isEdgeOn = isNotFist && knuckleRatio <= EdgeOnThreshold;
        State.IsEdgeOnPosture = isEdgeOn;

        if (!isEdgeOn)
        {
            State.History.Clear();
            return null;
        }

        Point2f palmCenter = hand.SmoothedLandmarks2D[9];
        DateTime now = DateTime.Now;

        State.History.Enqueue((palmCenter.X, palmCenter.Y, now));

        // Purge samples older than 250ms
        while (State.History.Count > 0 && (now - State.History.Peek().Time).TotalMilliseconds > 250.0)
        {
            State.History.Dequeue();
        }

        if (State.History.Count >= 3)
        {
            (double X, double Y, DateTime Time) oldest = State.History.Peek();
            double deltaX = palmCenter.X - oldest.X;
            double deltaY = Math.Abs(palmCenter.Y - oldest.Y);

            if (Math.Abs(deltaX) > MinSwipeDistance && deltaY < MaxVerticalDrift)
            {
                MonitorThrowDirection dir = deltaX > 0
                    ? MonitorThrowDirection.Right
                    : MonitorThrowDirection.Left;

                State.LastDirection = dir == MonitorThrowDirection.Right ? "RIGHT" : "LEFT";
                State.LastSwipeTime = now;
                State.LastFeedbackTime = now;
                State.LastSwipeCenter = palmCenter;
                State.History.Clear();

                return new MonitorThrowDecision(dir, targetHwnd);
            }
        }

        return null;
    }
}
