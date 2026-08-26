namespace NEXA.Adapters.Output;

/// <summary>
/// Output port abstraction interface for capturing desktop screen regions, writing image files, and setting clipboard bitmaps.
/// <para>
/// <b>What it is:</b> A decoupled contract defining desktop screen capture capabilities.
/// </para>
/// <para>
/// <b>What it does:</b> Captures pixel rectangles from the Windows desktop, copies them to the system clipboard, and writes timestamped image files.
/// </para>
/// <para>
/// <b>Why it is used:</b> Isolates native Win32 GDI screen scraping and clipboard interop from gesture detection logic.
/// </para>
/// </summary>
public interface IScreenshotSink
{
    /// <summary>
    /// Captures a specified rectangular region of the desktop, copies it to the Windows clipboard, and saves it as a PNG file.
    /// </summary>
    /// <param name="screenX">Left coordinate in desktop screen pixel space.</param>
    /// <param name="screenY">Top coordinate in desktop screen pixel space.</param>
    /// <param name="width">Width of the capture region in pixels.</param>
    /// <param name="height">Height of the capture region in pixels.</param>
    /// <param name="outputDirectory">Target folder path to save the screenshot image.</param>
    /// <param name="savedFilePath">Receives the absolute file path of the saved image on success.</param>
    /// <returns><c>true</c> if the capture succeeded; otherwise, <c>false</c>.</returns>
    bool CaptureScreenRegion(int screenX, int screenY, int width, int height, string outputDirectory, out string savedFilePath);
}
