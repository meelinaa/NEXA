using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NEXA.Application;
using OpenCvSharp;
using Xunit;

namespace NEXA.Tests.Application;

/// <summary>
/// Comprehensive Right-BICEP unit tests for <see cref="FrameProcessingPipeline"/> validating asynchronous producer-consumer streaming.
/// </summary>
public class FrameProcessingPipelineTests
{
    // [R]IGHT-BICEP: Verifies standard sequential frame production and consumption through the bounded channel.
    [Fact]
    public async Task EnqueueAndDequeue_WithValidFrame_DeliversSameFrameInstance()
    {
        // Arrange
        using FrameProcessingPipeline pipeline = new(capacity: 1);
        using Mat originalFrame = new(100, 100, MatType.CV_8UC3, new Scalar(255, 0, 0));

        // Act
        bool enqueued = await pipeline.EnqueueFrameAsync(originalFrame);
        using Mat dequeuedFrame = await pipeline.DequeueFrameAsync();

        // Assert
        Assert.True(enqueued);
        Assert.Equal(originalFrame.Width, dequeuedFrame.Width);
        Assert.Equal(originalFrame.Height, dequeuedFrame.Height);
    }

    // RIGHT-[B]OUNDARY: Validates that passing 0 or negative capacity boundary values defaults safely to capacity 1.
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Constructor_WithNonPositiveCapacity_ClampsToMinimumCapacityOne(int invalidCapacity)
    {
        // Arrange & Act
        using FrameProcessingPipeline pipeline = new(invalidCapacity);

        // Assert
        Assert.Equal(1, pipeline.Capacity);
    }

    // RIGHT-B[I]CEP: Confirms that cancelling a waiting dequeue operation aborts immediately via OperationCanceledException.
    [Fact]
    public async Task DequeueFrameAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        using FrameProcessingPipeline pipeline = new(capacity: 1);
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await pipeline.DequeueFrameAsync(cts.Token);
        });
    }

    // RIGHT-BI[C]HECK: Cross-checks DropOldest policy ensuring that when buffer overflows, the consumer receives the freshest frame.
    [Fact]
    public async Task EnqueueFrameAsync_WhenBufferFull_DropsOldestAndDeliversFreshestFrame()
    {
        // Arrange
        using FrameProcessingPipeline pipeline = new(capacity: 1);
        using Mat frame1 = new(10, 10, MatType.CV_8UC1, new Scalar(10));
        using Mat frame2 = new(20, 20, MatType.CV_8UC1, new Scalar(20));

        // Act: Enqueue 2 frames into a capacity=1 channel
        await pipeline.EnqueueFrameAsync(frame1);
        await pipeline.EnqueueFrameAsync(frame2);

        using Mat latestFrame = await pipeline.DequeueFrameAsync();

        // Assert: Oldest frame (10x10) was dropped, freshest frame (20x20) is delivered
        Assert.Equal(20, latestFrame.Width);
    }

    // RIGHT-BIC[E]P: Verifies that calling Complete on the pipeline completes the channel gracefully without throwing exceptions.
    [Fact]
    public void Complete_WhenCalledMultipleTimes_ExecutesSafelyWithoutException()
    {
        // Arrange
        using FrameProcessingPipeline pipeline = new(capacity: 1);

        // Act & Assert
        pipeline.Complete();
        pipeline.Complete();
    }

    // RIGHT-BICE[P]: Ensures 10,000 rapid asynchronous channel push-pull cycles execute well within performance limits (< 100ms).
    [Fact]
    public async Task Pipeline_HighThroughputEnqueueDequeue_ExecutesWithinPerformanceBudget()
    {
        // Arrange
        using FrameProcessingPipeline pipeline = new(capacity: 100);
        using Mat testMat = new(10, 10, MatType.CV_8UC1);
        Stopwatch sw = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 10000; i++)
        {
            await pipeline.EnqueueFrameAsync(testMat);
            using Mat _ = await pipeline.DequeueFrameAsync();
        }
        sw.Stop();

        // Assert
        Assert.True(sw.ElapsedMilliseconds < 100, $"10k channel cycles took {sw.ElapsedMilliseconds}ms.");
    }
}
