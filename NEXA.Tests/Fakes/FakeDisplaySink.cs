using System;
using NEXA.Abstractions;
using OpenCvSharp;

namespace NEXA.Tests.Fakes;

/// <summary>
/// In-memory test double for <see cref="IDisplaySink"/> tracking presentation calls and rendered frames.
/// </summary>
public class FakeDisplaySink : IDisplaySink
{
    /// <summary>
    /// Gets the total count of frames shown through this sink.
    /// </summary>
    public int FramesShown { get; private set; } = 0;

    /// <summary>
    /// Gets the most recent frame presented to the sink, if stored.
    /// </summary>
    public Mat? LastRenderedFrame { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether to capture a copy of the last frame.
    /// </summary>
    public bool CaptureLastFrame { get; set; } = false;

    /// <inheritdoc/>
    public void ShowImage(Mat image)
    {
        FramesShown++;

        if (CaptureLastFrame && image != null && !image.Empty())
        {
            LastRenderedFrame?.Dispose();
            LastRenderedFrame = image.Clone();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        LastRenderedFrame?.Dispose();
        LastRenderedFrame = null;
    }
}
