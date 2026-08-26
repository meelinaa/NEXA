namespace NEXA.Domain.Grab;

/// <summary>
/// Enumeration of desktop screen edge and corner snapping geometries supported during window dragging.
/// <para>
/// <b>What it is:</b> A domain enum categorizing edge and quadrant docking alignments.
/// </para>
/// <para>
/// <b>Why it is used:</b> Provides strongly typed identifiers for Windows 11-style snap layouts (Halves and Quadrants).
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
    /// Snapped to the top half of the display (X=0, Y=0, Width=ScreenWidth, Height=ScreenHeight/2).
    /// </summary>
    TopHalf,

    /// <summary>
    /// Snapped to the bottom half of the display (X=0, Y=ScreenHeight/2, Width=ScreenWidth, Height=ScreenHeight/2).
    /// </summary>
    BottomHalf,

    /// <summary>
    /// Snapped to the top-left quadrant of the display (X=0, Y=0, Width=ScreenWidth/2, Height=ScreenHeight/2).
    /// </summary>
    TopLeftCorner,

    /// <summary>
    /// Snapped to the top-right quadrant of the display (X=ScreenWidth/2, Y=0, Width=ScreenWidth/2, Height=ScreenHeight/2).
    /// </summary>
    TopRightCorner,

    /// <summary>
    /// Snapped to the bottom-left quadrant of the display (X=0, Y=ScreenHeight/2, Width=ScreenWidth/2, Height=ScreenHeight/2).
    /// </summary>
    BottomLeftCorner,

    /// <summary>
    /// Snapped to the bottom-right quadrant of the display (X=ScreenWidth/2, Y=ScreenHeight/2, Width=ScreenWidth/2, Height=ScreenHeight/2).
    /// </summary>
    BottomRightCorner
}
