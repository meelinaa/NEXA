using System;
using System.IO;
using NEXA.Abstractions;

namespace NEXA.Tests.Fakes;

/// <summary>
/// In-memory test double for <see cref="IScreenshotSink"/> simulating screen capture without GDI BitBlt or clipboard manipulation.
/// </summary>
public class FakeScreenshotSink : IScreenshotSink
{
    public int CaptureCount { get; private set; } = 0;
    public bool ShouldSucceed { get; set; } = true;

    public bool CaptureScreenRegion(
        int screenX,
        int screenY,
        int width,
        int height,
        string outputDirectory,
        out string savedFilePath)
    {
        CaptureCount++;

        if (!ShouldSucceed)
        {
            savedFilePath = string.Empty;
            return false;
        }

        savedFilePath = Path.Combine(outputDirectory, $"fake_screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        return true;
    }
}
