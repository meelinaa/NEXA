using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using OpenCvSharp;

namespace NEXA.Application;

/// <summary>
/// High-performance asynchronous producer-consumer channel pipeline decoupling real-time camera ingestion from AI inference workers.
/// <para>
/// <b>What it is:</b> A bounded channel with <see cref="BoundedChannelFullMode.DropOldest"/> ensuring zero-latency real-time frame streaming.
/// </para>
/// </summary>
public class FrameProcessingPipeline : IDisposable
{
    private readonly Channel<Mat> _frameChannel;

    /// <summary>
    /// Gets the maximum capacity of the pipeline buffer. Default is 1 to guarantee zero frame-queue lag.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameProcessingPipeline"/> class.
    /// </summary>
    /// <param name="capacity">Buffer capacity for pending frames (default: 1).</param>
    public FrameProcessingPipeline(int capacity = 1)
    {
        Capacity = Math.Max(1, capacity);
        BoundedChannelOptions options = new(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,
            SingleReader = true
        };

        _frameChannel = Channel.CreateBounded<Mat>(options);
    }

    /// <summary>
    /// Produces and writes a video frame into the processing channel.
    /// </summary>
    /// <param name="frame">The captured camera Mat frame.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    public async ValueTask<bool> EnqueueFrameAsync(Mat frame, CancellationToken cancellationToken = default)
    {
        return await _frameChannel.Writer.WaitToWriteAsync(cancellationToken) && _frameChannel.Writer.TryWrite(frame);
    }

    /// <summary>
    /// Consumes and reads the next available video frame from the processing channel.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to signal shutdown.</param>
    /// <returns>The latest captured OpenCV Mat frame.</returns>
    public async ValueTask<Mat> DequeueFrameAsync(CancellationToken cancellationToken = default)
    {
        return await _frameChannel.Reader.ReadAsync(cancellationToken);
    }

    /// <summary>
    /// Signals that no more frames will be written to the channel.
    /// </summary>
    public void Complete()
    {
        _frameChannel.Writer.TryComplete();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Complete();
        GC.SuppressFinalize(this);
    }
}
