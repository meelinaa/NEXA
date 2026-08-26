namespace NEXA.Domain.Lock;

/// <summary>
/// Discrete sequential milestones in the 4-stage security gesture sequence for locking the PC.
/// <para>
/// <b>Sequence:</b> <see cref="Idle"/> &rarr; <see cref="OpenPalm1"/> &rarr; <see cref="Fist1"/> &rarr; <see cref="OpenPalm2"/> &rarr; <see cref="Fist2"/> (Trigger Lock).
/// </para>
/// </summary>
public enum LockSequenceStep
{
    /// <summary>
    /// No security lock sequence is actively underway.
    /// </summary>
    Idle = 0,

    /// <summary>
    /// Step 1: Initial Open Palm posture (🖐️) confirmed.
    /// </summary>
    OpenPalm1 = 1,

    /// <summary>
    /// Step 2: First Fist posture (✊) confirmed within 800ms.
    /// </summary>
    Fist1 = 2,

    /// <summary>
    /// Step 3: Second Open Palm posture (🖐️) confirmed within 800ms.
    /// </summary>
    OpenPalm2 = 3,

    /// <summary>
    /// Step 4: Final Fist posture (✊) confirmed within 800ms &rarr; triggers Win32 LockWorkstation.
    /// </summary>
    Fist2 = 4
}
