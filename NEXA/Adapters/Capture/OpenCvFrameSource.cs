using System;
using NEXA.Abstractions;
using OpenCvSharp;

namespace NEXA.Adapters.Capture;

/// <summary>
/// Concrete OpenCV hardware implementation of <see cref="IFrameSource"/> wrapping <see cref="VideoCapture"/>.
/// </summary>
public class OpenCvFrameSource : IFrameSource
{
    private VideoCapture? _capture;

    /// <inheritdoc/>
    public bool Open(int index)
    {
        _capture?.Dispose();
        _capture = new VideoCapture(index, VideoCaptureAPIs.ANY);
        return _capture.IsOpened();
    }

    /// <inheritdoc/>
    public bool IsOpened()
    {
        return _capture != null && _capture.IsOpened();
    }

    /// <inheritdoc/>
    public bool Read(Mat image)
    {
        return _capture != null && _capture.Read(image);
    }

    /// <inheritdoc/>
    public bool Set(VideoCaptureProperties property, double value)
    {
        return _capture != null && _capture.Set(property, value);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _capture?.Dispose();
        _capture = null;
        GC.SuppressFinalize(this);
    }
}
