using System;
using System.Collections.Generic;
using NEXA.Abstractions;
using OpenCvSharp;

namespace NEXA.Tests.Fakes;

/// <summary>
/// In-memory test double for <see cref="IFrameSource"/> simulating video capture from webcams or video files with programmatic frame feeds.
/// </summary>
public class FakeFrameSource : IFrameSource
{
    private readonly Queue<Mat> _frames = new();
    private readonly int _frameCountToSupply;
    private readonly bool _shouldOpenSucceed;
    private readonly bool _supplyEmptyFrames;
    private readonly Func<int, Mat>? _frameGenerator;
    private int _framesRead = 0;

    /// <summary>
    /// Gets a value indicating whether the Open method was invoked.
    /// </summary>
    public bool IsOpenCalled { get; private set; }

    /// <summary>
    /// Gets the total number of frames successfully read from this source.
    /// </summary>
    public int FramesReadCount => _framesRead;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeFrameSource"/> class.
    /// </summary>
    /// <param name="frameCountToSupply">Number of frames to supply before reporting EOF.</param>
    /// <param name="shouldOpenSucceed">Whether Open() should return true.</param>
    /// <param name="supplyEmptyFrames">Whether to supply empty frames.</param>
    /// <param name="frameGenerator">Optional custom procedural frame generator function.</param>
    public FakeFrameSource(
        int frameCountToSupply = 1,
        bool shouldOpenSucceed = true,
        bool supplyEmptyFrames = false,
        Func<int, Mat>? frameGenerator = null)
    {
        _frameCountToSupply = frameCountToSupply;
        _shouldOpenSucceed = shouldOpenSucceed;
        _supplyEmptyFrames = supplyEmptyFrames;
        _frameGenerator = frameGenerator;
    }

    /// <summary>
    /// Enqueues a pre-recorded or synthetic frame into the source buffer.
    /// </summary>
    /// <param name="frame">The frame to enqueue.</param>
    public void EnqueueFrame(Mat frame)
    {
        _frames.Enqueue(frame);
    }

    /// <inheritdoc/>
    public bool Open(int index)
    {
        IsOpenCalled = true;
        return _shouldOpenSucceed;
    }

    /// <inheritdoc/>
    public bool IsOpened() => _shouldOpenSucceed;

    /// <inheritdoc/>
    public bool Read(Mat image)
    {
        if (_frames.Count > 0)
        {
            using Mat queued = _frames.Dequeue();
            queued.CopyTo(image);
            _framesRead++;
            return true;
        }

        if (_framesRead >= _frameCountToSupply)
        {
            return false;
        }

        _framesRead++;

        if (_supplyEmptyFrames)
        {
            return true;
        }

        if (_frameGenerator != null)
        {
            using Mat generated = _frameGenerator(_framesRead);
            generated.CopyTo(image);
            return true;
        }

        using Mat dummy = new(720, 1280, MatType.CV_8UC3, new Scalar(40, 40, 40));
        dummy.CopyTo(image);
        return true;
    }

    /// <inheritdoc/>
    public bool Set(VideoCaptureProperties property, double value) => true;

    /// <inheritdoc/>
    public void Dispose()
    {
        while (_frames.TryDequeue(out Mat? mat))
        {
            mat?.Dispose();
        }
    }
}
