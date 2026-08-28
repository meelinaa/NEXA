namespace NEXA.Object;

/// <summary>
/// Domain model representing an interactive 2D virtual test target object in camera space.
/// <para>
/// <b>What it is:</b> An augmented-reality UI object used to demonstrate and validate gesture-driven spatial manipulation.
/// </para>
/// <para>
/// <b>What it does:</b> Stores center position coordinates (X, Y) and unscaled base dimensions (Width, Height).
/// </para>
/// <para>
/// <b>Why it is used:</b> Provides a visual target for fist-grab dragging and pinch-to-zoom scaling without requiring third-party application windows.
/// </para>
/// <para>
/// <b>Consequence:</b> Renders on the video frame and moves/scales smoothly in response to hand interactions.
/// </para>
/// </summary>
public class TestObject
{
    /// <summary>
    /// Current horizontal center position in camera frame pixels.
    /// </summary>
    public double X { get; set; } = 950;

    /// <summary>
    /// Current vertical center position in camera frame pixels.
    /// </summary>
    public double Y { get; set; } = 480;

    /// <summary>
    /// Unscaled base width in pixels before zoom scaling factors are applied.
    /// </summary>
    public int BaseWidth { get; set; } = 180;

    /// <summary>
    /// Unscaled base height in pixels before zoom scaling factors are applied.
    /// </summary>
    public int BaseHeight { get; set; } = 120;
}
