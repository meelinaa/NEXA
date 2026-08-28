using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NEXA.Application;
using NEXA.Domain.Click;
using NEXA.Domain.EarsMute;
using NEXA.Domain.Grab;
using NEXA.Domain.Lock;
using NEXA.Domain.MonitorThrow;
using NEXA.Domain.Mute;
using NEXA.Domain.Scroll;
using NEXA.Domain.TwoHand;
using NEXA.Domain.Undo;
using NEXA.Domain.Volume;
using NEXA.Face;
using NEXA.Hand;
using NEXA.Object;
using NEXA.Tests.Application;
using NEXA.Tests.Fakes;
using NEXA.UI;
using OpenCvSharp;
using Xunit;

namespace NEXA.Tests.Application;

/// <summary>
/// Comprehensive Right-BICEP unit tests for <see cref="AsyncVisionPipeline"/> validating asynchronous parallel multi-model execution, cancellation, and thread isolation.
/// </summary>
public class AsyncVisionPipelineTests
{
    // [R]IGHT-BICEP: Validates that ProcessAsync runs parallel inference and returns valid tuple results.
    [Fact]
    public async Task ProcessAsync_WithValidFrame_ReturnsInferenceResults()
    {
        // Arrange
        HandTracker handTracker = new();
        FaceTracker faceTracker = new();
        AsyncVisionPipeline pipeline = new(handTracker, faceTracker);
        using Mat dummyFrame = new(720, 1280, MatType.CV_8UC3, new Scalar(50, 50, 50));

        // Act
        (List<TrackedHand> hands, TrackedFace? face) = await pipeline.ProcessAsync(dummyFrame);

        // Assert
        Assert.NotNull(hands);
    }

    // RIGHT-[B]ICEP: Validates boundary handling with empty/disposed frames returning safely without exceptions.
    [Fact]
    public async Task ProcessAsync_WithEmptyFrame_ReturnsSafeEmptyResults()
    {
        // Arrange
        HandTracker handTracker = new();
        FaceTracker faceTracker = new();
        AsyncVisionPipeline pipeline = new(handTracker, faceTracker);
        using Mat emptyFrame = new();

        // Act
        (List<TrackedHand> hands, TrackedFace? face) = await pipeline.ProcessAsync(emptyFrame);

        // Assert
        Assert.NotNull(hands);
        Assert.Empty(hands);
        Assert.Null(face);
    }

    // RIGHT-B[I]CEP: Confirms cancellation tokens immediately halt asynchronous parallel pipeline execution.
    [Fact]
    public async Task ProcessAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        HandTracker handTracker = new();
        FaceTracker faceTracker = new();
        AsyncVisionPipeline pipeline = new(handTracker, faceTracker);
        using Mat dummyFrame = new(720, 1280, MatType.CV_8UC3, new Scalar(50, 50, 50));
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await pipeline.ProcessAsync(dummyFrame, cts.Token);
        });
    }

    // RIGHT-BICE[P]: Validates that NexaEngine.RunAsync processes frames decoupled via Channel<Mat> and exits cleanly on key 'q'.
    [Fact]
    public async Task RunAsync_WithExitKey_ProcessesFramesDecoupledViaChannelAndTerminatesCleanly()
    {
        // Arrange
        FakeFrameSource frameSource = new(frameCountToSupply: 3);
        FakeDisplaySink displaySink = new();
        FakeKeyboardEventSource keyboardSource = new(new int[] { -1, -1, 'q' });

        HandTracker handTracker = new();
        FaceTracker faceTracker = new();
        FakeInputSink inputSink = new();
        FakeAudioSink audioSink = new();
        FakeScreenshotSink screenshotSink = new();
        HandMeshRenderer handRenderer = new();
        FaceMeshRenderer faceRenderer = new();
        HudRenderer hudRenderer = new();
        KeyboardCommandHandler commandHandler = new();

        NexaControllerBundle controllers = new(
            mouse: new(inputSink),
            scroll: new(inputSink),
            windowGrab: new(inputSink),
            twoHand: new(inputSink, screenshotSink),
            monitorThrow: new(inputSink),
            volume: new(audioSink),
            lockSeq: new(inputSink),
            circleUndo: new(inputSink),
            shhhMute: new(audioSink),
            hearNoEvil: new(audioSink),
            virtualObject: new());

        NexaEngine engine = new(
            handTracker,
            faceTracker,
            inputSink,
            audioSink,
            screenshotSink,
            handRenderer,
            faceRenderer,
            controllers,
            hudRenderer,
            commandHandler,
            frameSource,
            displaySink,
            keyboardSource);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

        // Act
        await engine.RunAsync(0, cts.Token);

        // Assert
        Assert.True(displaySink.FramesShown >= 1, $"Expected at least 1 frame shown, got {displaySink.FramesShown}.");
        Assert.True(frameSource.IsOpenCalled);
    }

    // [R]IGHT-BICEP: Validates that WarmUpAsync completes successfully without exceptions.
    [Fact]
    public async Task WarmUpAsync_ExecutesAndCompletesSuccessfully()
    {
        // Arrange
        HandTracker handTracker = new();
        FaceTracker faceTracker = new();
        AsyncVisionPipeline pipeline = new(handTracker, faceTracker);

        // Act & Assert (Should complete cleanly)
        await pipeline.WarmUpAsync();
    }

    // RIGHT-B[I]CEP: Confirms cancellation tokens immediately halt WarmUpAsync.
    [Fact]
    public async Task WarmUpAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        HandTracker handTracker = new();
        FaceTracker faceTracker = new();
        AsyncVisionPipeline pipeline = new(handTracker, faceTracker);
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await pipeline.WarmUpAsync(cts.Token);
        });
    }
}
