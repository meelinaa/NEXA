namespace NEXA.Domain.TwoHand;

/// <summary>
/// Enumeration of distinct window manipulation and desktop capture actions triggered by two-hand gestures.
/// <para>
/// <b>What it is:</b> A domain enum representing discrete multi-hand operations.
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
    Restore,

    /// <summary>
    /// Captures the screen region framed by two "L"-shaped hands upon double touch closure.
    /// </summary>
    Screenshot
}
