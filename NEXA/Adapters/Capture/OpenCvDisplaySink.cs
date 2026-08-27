using System;
using NEXA.Abstractions;
using OpenCvSharp;

namespace NEXA.Adapters.Capture;

/// <summary>
/// Concrete OpenCV implementation of <see cref="IDisplaySink"/> wrapping <see cref="Window"/>.
/// </summary>
public class OpenCvDisplaySink : IDisplaySink
{
    private Window? _window;
    private readonly string _windowTitle;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCvDisplaySink"/> class.
    /// </summary>
    /// <param name="windowTitle">The presentation window title bar text.</param>
    public OpenCvDisplaySink(string windowTitle = "NEXA - MediaPipe Hand Tracking [ONNX]")
    {
        _windowTitle = windowTitle;
    }

    /// <inheritdoc/>
    public void ShowImage(Mat image)
    {
        _window ??= new Window(_windowTitle, WindowFlags.AutoSize);
        _window.ShowImage(image);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _window?.Dispose();
        _window = null;
        GC.SuppressFinalize(this);
    }
}
