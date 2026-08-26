namespace NEXA.Domain.Undo;

/// <summary>
/// Enumeration of discrete Undo and Redo actions triggered by circular hand gestures.
/// <para>
/// <b>What it is:</b> A domain enum classifying the evaluated circular motion action.
/// </para>
/// </summary>
public enum CircleUndoAction
{
    /// <summary>
    /// No circular revolution threshold reached.
    /// </summary>
    None,

    /// <summary>
    /// Counter-clockwise 2x rotation (↺, ~-720°) completed &rarr; triggers Undo (Ctrl + Z).
    /// </summary>
    Undo,

    /// <summary>
    /// Clockwise 2x rotation (↻, ~+720°) completed &rarr; triggers Redo (Ctrl + Y).
    /// </summary>
    Redo
}
