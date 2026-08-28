namespace NEXA.Domain.Lock;

/// <summary>
/// Milestones in the 4-stage workstation lock sequence (Open -> Fist -> Open -> Fist).
/// </summary>
public enum LockSequenceStep
{
    /// <summary>
    /// Idle state, waiting for the first gesture step.
    /// </summary>
    Idle = 0,

    /// <summary>
    /// Step 1: Open palm detected.
    /// </summary>
    OpenPalm1 = 1,

    /// <summary>
    /// Step 2: Fist detected following open palm.
    /// </summary>
    Fist1 = 2,

    /// <summary>
    /// Step 3: Second open palm detected.
    /// </summary>
    OpenPalm2 = 3,

    /// <summary>
    /// Step 4: Second fist detected (triggers PC lock).
    /// </summary>
    Fist2 = 4
}
