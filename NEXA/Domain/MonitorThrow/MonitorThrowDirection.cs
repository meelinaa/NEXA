namespace NEXA.Domain.MonitorThrow;

/// <summary>
/// Enumeration of horizontal monitor transfer directions.
/// <para>
/// <b>What it is:</b> A domain enum specifying the target display relative to the current display.
/// </para>
/// <para>
/// <b>Why it is used:</b> Provides strongly typed direction identifiers for monitor relocation gestures.
/// </para>
/// </summary>
public enum MonitorThrowDirection
{
    /// <summary>
    /// Transfer window to the physically adjacent display to the left.
    /// </summary>
    Left,

    /// <summary>
    /// Transfer window to the physically adjacent display to the right.
    /// </summary>
    Right
}
