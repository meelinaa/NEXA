using System;
using System.Diagnostics;
using OpenCvSharp;

namespace NEXA.Domain.Lock;

/// <summary>
/// State container tracking the multi-stage security sequence (Open -> Fist -> Open -> Fist), inter-step transition stopwatches, and cooldown timestamps.
/// <para>
/// <b>What it is:</b> State machine holding current milestone, transition timeout stopwatches, consecutive frame debounce counters, and cooldown tracking.
/// </para>
/// </summary>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description>Maintains the active <see cref="CurrentStep"/> (1 through 4).</description></item>
/// <item><description>Enforces a strict <see cref="StepTimeoutSeconds"/> (0.80s) maximum window for each posture transition.</description></item>
/// <item><description>Tracks consecutive frame debouncing counters for posture stability.</description></item>
/// <item><description>Enforces a 3.0s cooldown following an executed PC lock.</description></item>
/// </list>
/// </para>
/// </summary>
public class LockSequenceState
{
    /// <summary>
    /// The current milestone step in the 4-stage sequence.
    /// </summary>
    public LockSequenceStep CurrentStep { get; set; } = LockSequenceStep.Idle;

    /// <summary>
    /// High-precision stopwatch tracking elapsed time since the most recent step transition.
    /// </summary>
    public Stopwatch StepTimer { get; } = new();

    /// <summary>
    /// Maximum allowed time in seconds (0.80s) between successive posture changes before the state machine resets to <see cref="LockSequenceStep.Idle"/>.
    /// </summary>
    public double StepTimeoutSeconds { get; set; } = 0.80;

    /// <summary>
    /// Number of consecutive frames the current required posture has been maintained (for debouncing).
    /// </summary>
    public int ConsecutivePoseFrames { get; set; } = 0;

    /// <summary>
    /// Timestamp of the most recent workstation lock event.
    /// </summary>
    public DateTime LastLockTriggerTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Dedicated stopwatch enforcing a 3.0-second post-lock refractory cooldown.
    /// </summary>
    public Stopwatch LockCooldownTimer { get; } = new();

    /// <summary>
    /// Indicates whether the lock sequence is currently in cooldown.
    /// </summary>
    public bool InCooldown => LockCooldownTimer.IsRunning && LockCooldownTimer.Elapsed.TotalSeconds < 3.0;

    /// <summary>
    /// Normalized remaining time in the current step transition window from 1.0 (just transitioned) down to 0.0 (timed out).
    /// </summary>
    public double RemainingWindowProgress
    {
        get
        {
            if (CurrentStep == LockSequenceStep.Idle || !StepTimer.IsRunning)
                return 0.0;
            double elapsed = StepTimer.Elapsed.TotalSeconds;
            return Math.Clamp(1.0 - (elapsed / StepTimeoutSeconds), 0.0, 1.0);
        }
    }

    /// <summary>
    /// 2D camera coordinates of the hand when transitioning steps (for floating HUD feedback).
    /// </summary>
    public Point2f LastHandPos { get; set; }

    /// <summary>
    /// Resets the sequence state machine back to <see cref="LockSequenceStep.Idle"/>.
    /// </summary>
    public void Reset()
    {
        CurrentStep = LockSequenceStep.Idle;
        StepTimer.Reset();
        ConsecutivePoseFrames = 0;
    }
}
