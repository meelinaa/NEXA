namespace NEXA.Configuration;

/// <summary>
/// Camera hardware acquisition and streaming configuration options.
/// </summary>
public class CameraOptions
{
    /// <summary>
    /// The zero-based hardware camera index. Default is 0.
    /// </summary>
    public int DeviceIndex { get; set; } = 0;

    /// <summary>
    /// Target frame capture width in pixels. Default is 1280.
    /// </summary>
    public int FrameWidth { get; set; } = 1280;

    /// <summary>
    /// Target frame capture height in pixels. Default is 720.
    /// </summary>
    public int FrameHeight { get; set; } = 720;

    /// <summary>
    /// Target camera video capture frame rate in FPS. Default is 30.
    /// </summary>
    public int TargetFps { get; set; } = 30;
}
