namespace NEXA.Domain.TwoHand;

/// <summary>
/// Actions triggered by bimanual two-hand gestures.
/// </summary>
public enum TwoHandAction
{
    /// <summary>
    /// No two-hand action active.
    /// </summary>
    None = 0,

    /// <summary>
    /// Fast two-hand clap toggling media play/pause.
    /// </summary>
    PlayPause,

    /// <summary>
    /// Two-hand L-shape camera crop box triggering a regional screenshot.
    /// </summary>
    Screenshot,

    /// <summary>
    /// Two-hand outward expansion maximizing the active window.
    /// </summary>
    Maximize,

    /// <summary>
    /// Two-hand inward pinch minimizing the active window.
    /// </summary>
    Minimize
}
