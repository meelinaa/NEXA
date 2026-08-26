namespace NEXA.Domain.TwoHand;

/// <summary>
/// Enumeration of distinct window manipulation actions triggered by two-hand gestures.
/// <para>
/// <b>What it is:</b> A domain enum representing discrete window operations.
/// </para>
/// <para>
/// <b>Why it is used:</b> Provides strongly-typed gesture command identifiers.
/// </para>
/// </summary>
public enum TwoHandAction
{
    /// <summary>
    /// Maximizes the window to full screen.
    /// </summary>
    Maximize,

    /// <summary>
    /// Minimizes the window to the taskbar.
    /// </summary>
    Minimize,

    /// <summary>
    /// Restores the window to its normal geometry.
    /// </summary>
    Restore
}
