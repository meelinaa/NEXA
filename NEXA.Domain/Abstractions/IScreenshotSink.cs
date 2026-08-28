namespace NEXA.Abstractions;

/// <summary>
/// Abstraction contract for taking desktop and region screenshots and persisting them to disk.
/// <para>
/// <b>What it is:</b> Interface decoupling screenshot capturing and disk I/O from domain gesture logic.
/// </para>
/// </summary>
public interface IScreenshotSink
{
    /// <summary>
    /// Captures the specified desktop screen region using hardware BitBlt, saves the image to disk, and copies it to the system clipboard.
    /// </summary>
    /// <param name="screenX">Left horizontal coordinate on primary monitor.</param>
    /// <param name="screenY">Top vertical coordinate on primary monitor.</param>
    /// <param name="width">Width of capture region in pixels.</param>
    /// <param name="height">Height of capture region in pixels.</param>
    /// <param name="outputDirectory">Directory path where screenshot PNG files are persisted.</param>
    /// <param name="savedFilePath">Outputs the absolute path of the newly saved PNG image file.</param>
    /// <returns><c>true</c> if capture and file persistence succeeded; otherwise, <c>false</c>.</returns>
    bool CaptureScreenRegion(int screenX, int screenY, int width, int height, string outputDirectory, out string savedFilePath);
}
