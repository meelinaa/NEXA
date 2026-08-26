namespace NEXA.Domain.Grab;

/// <summary>
/// Enumeration of desktop screen edge snapping geometries supported during window dragging.
/// <para>
/// <b>What it is:</b> A domain enum categorizing edge docking alignments.
/// </para>
/// <para>
/// <b>Why it is used:</b> Provides strongly typed identifiers for Windows-style snap actions.
/// </para>
/// </summary>
public enum WindowSnapType
{
    /// <summary>
    /// No snap docking active; window follows hand cursor freely.
    /// </summary>
    None,

    /// <summary>
    /// Snapped to the left half of the display (X=0, Y=0, Width=ScreenWidth/2, Height=ScreenHeight).
    /// </summary>
    LeftHalf,

    /// <summary>
    /// Snapped to the right half of the display (X=ScreenWidth/2, Y=0, Width=ScreenWidth/2, Height=ScreenHeight).
    /// </summary>
    RightHalf,

    /// <summary>
    /// Snapped to full screen maximization (X=0, Y=0, Width=ScreenWidth, Height=ScreenHeight).
    /// </summary>
    TopMaximize
}
