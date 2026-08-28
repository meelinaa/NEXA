using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NEXA.Abstractions;
using NEXA.Face;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Application;

/// <summary>
/// Asynchronous multi-model computer vision pipeline executing hand tracking and facial landmark estimation in parallel.
/// <para>
/// <b>What it is:</b> An application service running deep-learning inference across multiple background tasks with <see cref="Task.WhenAll"/>.
/// </para>
/// </summary>
public class AsyncVisionPipeline : IVisionPipeline
{
    private readonly HandTracker _handTracker;
    private readonly FaceTracker _faceTracker;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncVisionPipeline"/> class.
    /// </summary>
    /// <param name="handTracker">The hand tracking pipeline instance.</param>
    /// <param name="faceTracker">The face tracking pipeline instance.</param>
    public AsyncVisionPipeline(HandTracker handTracker, FaceTracker faceTracker)
    {
        _handTracker = handTracker ?? throw new ArgumentNullException(nameof(handTracker));
        _faceTracker = faceTracker ?? throw new ArgumentNullException(nameof(faceTracker));
    }

    /// <inheritdoc/>
    public async Task<(List<TrackedHand> Hands, TrackedFace? Face)> ProcessAsync(
        Mat frame,
        CancellationToken cancellationToken = default)
    {
        if (frame == null || frame.Empty())
        {
            return (new List<TrackedHand>(), null);
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Run Hand and Face inference in parallel on the ThreadPool
        Task<List<TrackedHand>> handTask = Task.Run(() => _handTracker.ProcessFrame(frame), cancellationToken);
        Task<TrackedFace?> faceTask = Task.Run(() => _faceTracker.ProcessFrame(frame), cancellationToken);

        await Task.WhenAll(handTask, faceTask).ConfigureAwait(false);

        return (await handTask.ConfigureAwait(false), await faceTask.ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async Task WarmUpAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Task handWarmUp = Task.Run(() => _handTracker.WarmUp(), cancellationToken);
        Task faceWarmUp = Task.Run(() => _faceTracker.WarmUp(), cancellationToken);

        await Task.WhenAll(handWarmUp, faceWarmUp).ConfigureAwait(false);
    }
}
