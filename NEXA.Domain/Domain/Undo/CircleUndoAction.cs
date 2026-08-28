namespace NEXA.Domain.Undo;

/// <summary>
/// Actions triggered by rotational wrist twist gestures.
/// </summary>
public enum CircleUndoAction
{
    /// <summary>
    /// No undo/redo action.
    /// </summary>
    None = 0,

    /// <summary>
    /// Counter-clockwise twist triggering Undo (Ctrl + Z).
    /// </summary>
    Undo,

    /// <summary>
    /// Clockwise twist triggering Redo (Ctrl + Y).
    /// </summary>
    Redo
}
