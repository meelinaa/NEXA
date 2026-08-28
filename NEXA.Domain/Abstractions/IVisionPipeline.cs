using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NEXA.Face;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Abstractions;

/// <summary>
/// Abstraction contract for asynchronous computer vision inference pipelines (hand tracking, face tracking, and pose analysis).
/// <para>
/// <b>What it is:</b> An interface decoupling the frame capture loop from parallel multi-model deep learning inference.
/// </para>
/// </summary>
public interface IVisionPipeline
{
    /// <summary>
    /// Executes parallel deep learning model inference (Hands and Face) asynchronously over the provided frame.
    /// </summary>
    /// <param name="frame">The raw or mirrored camera image frame (BGR Mat).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A tuple containing the tracked hand instances and the primary tracked face.</returns>
    Task<(List<TrackedHand> Hands, TrackedFace? Face)> ProcessAsync(Mat frame, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pre-warms all underlying ONNX deep-learning models (Hands and Face) and compiles DirectML shaders in parallel.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task WarmUpAsync(CancellationToken cancellationToken = default);
}
