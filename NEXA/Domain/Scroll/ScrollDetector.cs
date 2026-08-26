using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Scroll;

/// <summary>
/// Domain-level gesture analyzer for vertical palm swipe detection and physics momentum scrolling.
/// <para>
/// <b>What it is:</b> A pure computational detector that converts vertical hand motions into discrete and continuous scroll commands.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="number">
/// <item><description><b>Activation Gating:</b> Requires an active 3.0s window following mouse pointing or a previous swipe to prevent accidental triggers.</description></item>
/// <item><description><b>Least-Squares Linear Regression:</b> Computes trend slope (px/ms) over all points in a 250ms history window to determine speed and direction robustly.</description></item>
/// <item><description><b>Consistency Validation:</b> Enforces agreement between regression slope sign and total net displacement to filter noisy directional reversals.</description></item>
/// <item><description><b>Rest State Protection:</b> Locks opposite-direction triggering until the hand comes to a physical rest below <see cref="SwipeState.RestSpeedThreshold"/>.</description></item>
/// <item><description><b>Physics Inertia Decay:</b> Gradually expels accumulated velocity across frames to produce natural touchpad-style smooth scrolling.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Why it is used:</b> Isolates mathematical gesture recognition from Win32 interop, allowing testable and deterministic scrolling behavior.
/// </para>
/// <para>
/// <b>Consequence:</b> Generates <see cref="ScrollDecision"/> objects whenever whole <see cref="WHEEL_DELTA"/> units are ready to be dispatched.
/// </para>
/// </summary>
public class ScrollDetector
{
    /// <summary>
    /// Standard Windows mouse wheel detent increment value (120 units per scroll notch).
    /// </summary>
    public const int WHEEL_DELTA = 120;

    /// <summary>
    /// State machine tracking temporal history, recoil cooldowns, and momentum velocity.
    /// </summary>
    public SwipeState State { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether scroll detection is active.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Timestamp of when the mouse pointer gesture was last active.
    /// </summary>
    public DateTime LastPointerActiveTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Allowed time window (3.0 seconds) after pointing or swiping during which scroll gestures remain enabled.
    /// </summary>
    public static readonly TimeSpan ScrollWindowDuration = TimeSpan.FromSeconds(3.0);

    /// <summary>
    /// Counter tracking consecutive frames with invalid gestures to debounce temporary tracking dropouts caused by motion blur.
    /// </summary>
    private int _invalidGestureFrameCount = 0;

    /// <summary>
    /// Number of consecutive dropped frames allowed before clearing swipe history.
    /// </summary>
    private const int InvalidGestureTolerance = 3;

    /// <summary>
    /// Direction string ("UP" or "DOWN") of the most recently triggered swipe.
    /// </summary>
    public string LastSwipeDirection { get; private set; } = "";

    /// <summary>
    /// Timestamp of the latest triggered swipe feedback event.
    /// </summary>
    public DateTime LastFeedbackTime { get; private set; } = DateTime.MinValue;

    /// <summary>
    /// 2D image coordinates of the palm center when the swipe was initiated.
    /// </summary>
    public Point2f LastSwipePoint { get; private set; }

    /// <summary>
    /// Initial impulse velocity magnitude assigned to the momentum accumulator.
    /// </summary>
    public double LastInitialVelocity { get; private set; } = 0.0;

    /// <summary>
    /// Indicates whether the 3-second activation window is currently open based on recent pointer or swipe activity.
    /// </summary>
    public bool IsWindowActive
    {
        get
        {
            double secSincePointer = (DateTime.Now - LastPointerActiveTime).TotalSeconds;
            double secSinceSwipe = (DateTime.Now - State.LastSwipeTime).TotalSeconds;
            return secSincePointer <= ScrollWindowDuration.TotalSeconds || secSinceSwipe <= ScrollWindowDuration.TotalSeconds;
        }
    }

    /// <summary>
    /// The remaining duration (in seconds) before the 3-second activation window expires.
    /// </summary>
    public double RemainingWindowSeconds
    {
        get
        {
            double secSincePointer = (DateTime.Now - LastPointerActiveTime).TotalSeconds;
            double secSinceSwipe = (DateTime.Now - State.LastSwipeTime).TotalSeconds;
            double minElapsed = Math.Min(secSincePointer, secSinceSwipe);
            return Math.Max(0.0, ScrollWindowDuration.TotalSeconds - minElapsed);
        }
    }

    /// <summary>
    /// Computes the linear regression slope (Least Squares method) across all timestamped vertical positions in the history queue.
    /// Formula: <c>slope = (N*sum(t*y) - sum(t)*sum(y)) / (N*sum(t^2) - (sum(t))^2)</c>.
    /// </summary>
    /// <param name="history">The queue of historical (Y, Time) measurements.</param>
    /// <param name="referenceTime">Reference time to compute relative delta milliseconds.</param>
    /// <returns>The slope in pixels per millisecond (negative = upward motion, positive = downward motion).</returns>
    public static double CalculateTrendSlope(Queue<(double Y, DateTime Time)> history, DateTime referenceTime)
    {
        int n = history.Count;
        if (n < 2) return 0.0;

        double sumT = 0.0;
        double sumY = 0.0;
        double sumTY = 0.0;
        double sumTT = 0.0;

        foreach ((double Y, DateTime Time) item in history)
        {
            double t = (item.Time - referenceTime).TotalMilliseconds;
            double y = item.Y;

            sumT += t;
            sumY += y;
            sumTY += t * y;
            sumTT += t * t;
        }

        double denominator = (n * sumTT) - (sumT * sumT);
        if (Math.Abs(denominator) < 1e-6) return 0.0;

        return ((n * sumTY) - (sumT * sumY)) / denominator;
    }

    /// <summary>
    /// Updates physical momentum inertia, decays velocity over time, and returns a <see cref="ScrollDecision"/> if whole notch units are ready.
    /// </summary>
    /// <returns>A <see cref="ScrollDecision"/> with accumulated whole wheel deltas, or <c>null</c> if no scroll is ready.</returns>
    public ScrollDecision? UpdateMomentum()
    {
        if (!Enabled || Math.Abs(State.MomentumVelocity) < SwipeState.MinMomentumVelocity)
        {
            State.MomentumVelocity = 0.0;
            State.AccumulatedDelta = 0.0;
            return null;
        }

        DateTime now = DateTime.Now;
        double dt = State.LastMomentumUpdate == DateTime.MinValue
            ? 16.0
            : (now - State.LastMomentumUpdate).TotalMilliseconds;
        State.LastMomentumUpdate = now;

        // Normalize delta time relative to standard 60 FPS tick (16ms)
        double normalizedTicks = Math.Clamp(dt / 16.0, 0.1, 4.0);
        State.AccumulatedDelta += State.MomentumVelocity * normalizedTicks;

        // Extract integer multiples of standard WHEEL_DELTA (120)
        int wholeUnits = (int)(State.AccumulatedDelta / WHEEL_DELTA) * WHEEL_DELTA;
        ScrollDecision? decision = null;

        if (wholeUnits != 0)
        {
            decision = new ScrollDecision(wholeUnits);
            State.AccumulatedDelta -= wholeUnits;
        }

        // Apply exponential velocity decay (friction)
        State.MomentumVelocity *= Math.Pow(SwipeState.MomentumDecay, normalizedTicks);
        return decision;
    }

    /// <summary>
    /// Evaluates hand motion in the current frame to detect new swipe gestures and initiate momentum impulses.
    /// </summary>
    /// <param name="hand">The tracked hand instance with smoothed landmarks.</param>
    /// <returns>A <see cref="ScrollDecision"/> if an immediate discrete scroll is triggered, or <c>null</c>.</returns>
    public ScrollDecision? Update(TrackedHand? hand)
    {
        if (!Enabled || hand == null)
        {
            _invalidGestureFrameCount++;
            if (_invalidGestureFrameCount >= InvalidGestureTolerance)
            {
                State.History.Clear();
                State.WaitingForRest = false;
            }
            return null;
        }

        // Check 3-second activation window
        if (!IsWindowActive)
        {
            State.History.Clear();
            State.WaitingForRest = false;
            _invalidGestureFrameCount = 0;
            return null;
        }

        string currentGesture = hand.Gesture;
        bool isValidGesture = currentGesture == "Hand Up" || currentGesture == "Hand Down" || currentGesture == "Open Palm" || currentGesture == "Tracking";

        // Gesture dropout debounce protection
        if (!isValidGesture)
        {
            _invalidGestureFrameCount++;
            if (_invalidGestureFrameCount >= InvalidGestureTolerance)
            {
                State.History.Clear();
                State.WaitingForRest = false;
            }
            return null;
        }

        _invalidGestureFrameCount = 0;

        Point2f palmCenter = hand.SmoothedLandmarks2D[9];
        DateTime now = DateTime.Now;

        State.History.Enqueue((palmCenter.Y, now));

        // Purge historical samples older than 250ms
        while (State.History.Count > 0 && now - State.History.Peek().Time > SwipeState.WindowSize)
        {
            State.History.Dequeue();
        }

        // Require minimum sample count for stable regression
        if (State.History.Count < 4) return null;

        // Compute linear regression slope and speed
        double slope = CalculateTrendSlope(State.History, now);
        double speed = Math.Abs(slope);
        State.LastSlope = slope;
        State.LastSpeed = speed;

        (double Y, DateTime Time) oldest = State.History.Peek();
        double totalDisplacement = palmCenter.Y - oldest.Y;
        State.LastDeltaY = totalDisplacement;

        // Rest phase enforcement after a prior swipe
        if (State.WaitingForRest)
        {
            bool cooldownPassed = now - State.LastSwipeTime >= SwipeState.Cooldown;
            if (cooldownPassed && speed < SwipeState.RestSpeedThreshold)
            {
                State.WaitingForRest = false;
            }
            return null;
        }

        // Swipe evaluation: Check distance and speed thresholds
        if (Math.Abs(totalDisplacement) > SwipeState.MinDistance && speed > SwipeState.MinSpeed)
        {
            // Consistency check: Slope and total displacement must agree in sign
            bool slopeSaysUp = slope < 0;
            bool displacementSaysUp = totalDisplacement < 0;
            if (slopeSaysUp != displacementSaysUp)
            {
                return null;
            }
            bool isMovingUp = slopeSaysUp;

            double speedNormalized = Math.Clamp((speed - SwipeState.MinSpeed) / 0.50, 0.0, 1.0);
            double initialVelocity = 45.0 + speedNormalized * 115.0;
            LastInitialVelocity = initialVelocity;

            // Directional mapping: Hand moving up (decreasing Y) -> Scroll DOWN; Hand moving down -> Scroll UP
            double newVelocity = isMovingUp ? -initialVelocity : initialVelocity;

            // Chained swipe momentum compounding
            if (Math.Sign(newVelocity) == Math.Sign(State.MomentumVelocity))
            {
                State.MomentumVelocity = Math.Clamp(State.MomentumVelocity + newVelocity * 0.75, -240.0, 240.0);
            }
            else
            {
                State.MomentumVelocity = newVelocity;
            }

            LastSwipeDirection = isMovingUp ? "DOWN" : "UP";
            LastSwipePoint = palmCenter;
            LastFeedbackTime = now;
            State.LastSwipeTime = now;
            State.LastMomentumUpdate = now;
            State.History.Clear();
            State.WaitingForRest = true;
        }

        return null;
    }
}
