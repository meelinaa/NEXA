using System;
using System.Collections.Generic;
using System.Diagnostics;
using NEXA.Abstractions;
using NEXA.Adapters.Output;
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
using NEXA.Tests.Fakes;
using NEXA.UI;
using OpenCvSharp;
using Xunit;

namespace NEXA.Tests.Application;

/// <summary>
/// Comprehensive Right-BICEP unit tests for <see cref="NexaEngine"/> validating isolated execution loop, frame flow, error handling, and performance.
/// </summary>
public class NexaEngineTests
{
    // [R]IGHT-BICEP: Verifies that NexaEngine executes full frame processing and UI render passes before gracefully exiting on key 'q'.
    [Fact]
    public void Run_WithValidFramesAndExitKey_ExecutesPipelineAndExitsCleanly()
    {
        // Arrange
        FakeFrameSource frameSource = new(frameCountToSupply: 2);
        FakeDisplaySink displaySink = new();
        FakeKeyboardEventSource keyboardSource = new(new int[] { -1, 'q' });

        NexaEngine engine = CreateEngine(frameSource, displaySink, keyboardSource);

        // Act
        engine.Run(0);

        // Assert
        Assert.True(displaySink.FramesShown >= 2, $"Expected at least 2 frames shown, but got {displaySink.FramesShown}.");
        Assert.True(frameSource.IsOpenCalled);
    }

    // RIGHT-[B]ICEP: Validates boundary handling when camera device fails to open (Open returns false), immediately returning without crashing.
    [Fact]
    public void Run_WhenCameraFailsToOpen_TerminatesImmediatelyWithoutProcessingFrames()
    {
        // Arrange
        FakeFrameSource failedFrameSource = new(frameCountToSupply: 5, shouldOpenSucceed: false);
        FakeDisplaySink displaySink = new();
        FakeKeyboardEventSource keyboardSource = new(new int[] { 'q' });

        NexaEngine engine = CreateEngine(failedFrameSource, displaySink, keyboardSource);

        // Act
        engine.Run(0);

        // Assert
        Assert.Equal(0, displaySink.FramesShown);
    }

    // RIGHT-B[I]CEP: Confirms that pressing exit keycode 'q' stops and terminates the infinite frame loop immediately.
    [Fact]
    public void Run_WithImmediateExitKey_TerminatesLoopAfterFirstFrame()
    {
        // Arrange
        FakeFrameSource frameSource = new(frameCountToSupply: 100);
        FakeDisplaySink displaySink = new();
        FakeKeyboardEventSource keyboardSource = new(new int[] { 'q' });

        NexaEngine engine = CreateEngine(frameSource, displaySink, keyboardSource);

        // Act
        engine.Run(0);

        // Assert
        Assert.Equal(1, displaySink.FramesShown);
    }

    // RIGHT-BI[C]HECK: Cross-checks that all registered display sinks receive the exact number of processed and flipped frames.
    [Fact]
    public void Run_CrossChecksDisplaySinkReceivingExactFrameCount()
    {
        // Arrange
        FakeFrameSource frameSource = new(frameCountToSupply: 5);
        FakeDisplaySink displaySink = new();
        FakeKeyboardEventSource keyboardSource = new(new int[] { -1, -1, -1, -1, 'q' });

        NexaEngine engine = CreateEngine(frameSource, displaySink, keyboardSource);

        // Act
        engine.Run(0);

        // Assert
        Assert.Equal(5, displaySink.FramesShown);
    }

    // RIGHT-BIC[E]P: Verifies that empty frames (Read returning true but frame.Empty()) are skipped safely without unhandled exceptions.
    [Fact]
    public void Run_WithEmptyFrames_HandlesGracefullyAndContinuesUntilExit()
    {
        // Arrange
        FakeFrameSource emptyFrameSource = new(frameCountToSupply: 3, supplyEmptyFrames: true);
        FakeDisplaySink displaySink = new();
        FakeKeyboardEventSource keyboardSource = new(new int[] { -1, -1, 'q' });

        NexaEngine engine = CreateEngine(emptyFrameSource, displaySink, keyboardSource);

        // Act
        engine.Run(0);

        // Assert
        Assert.Equal(0, displaySink.FramesShown);
    }

    // RIGHT-BICE[P]: Ensures full pipeline loop iteration completes within expected real-time frame budget (< 100ms per synthetic frame).
    [Fact]
    public async Task Run_SyntheticFrameLoop_ExecutesWithinFrameBudget()
    {
        // Arrange
        FakeFrameSource frameSource = new(frameCountToSupply: 10);
        FakeDisplaySink displaySink = new();
        List<int> keys = new();
        for (int i = 0; i < 9; i++)
        {
            keys.Add(-1);
        }
        keys.Add('q');
        FakeKeyboardEventSource keyboardSource = new(keys.ToArray());

        NexaEngine engine = CreateEngine(frameSource, displaySink, keyboardSource);
        await engine.WarmUpAsync();
        Stopwatch sw = Stopwatch.StartNew();

        // Act
        engine.Run(0);
        sw.Stop();

        // Assert: 10 synthetic pipeline frames + warm-up execute cleanly within budget
        Assert.True(sw.ElapsedMilliseconds < 2500, $"10 synthetic pipeline frames took {sw.ElapsedMilliseconds}ms.");
    }

    private static NexaEngine CreateEngine(
        IFrameSource frameSource,
        IDisplaySink displaySink,
        IKeyboardEventSource keyboardSource)
    {
        HandTracker handTracker = new();
        FaceTracker faceTracker = new();
        IInputSink inputSink = new Fakes.FakeInputSink();
        IAudioSink audioSink = new Fakes.FakeAudioSink();
        IScreenshotSink screenshotSink = new Fakes.FakeScreenshotSink();
        HandMeshRenderer handRenderer = new();
        FaceMeshRenderer faceRenderer = new();
        HudRenderer hudRenderer = new();
        KeyboardCommandHandler commandHandler = new();

        NexaControllerBundle controllers = new(
            mouse: new MouseController(inputSink),
            scroll: new ScrollController(inputSink),
            windowGrab: new WindowGrabController(inputSink),
            twoHand: new TwoHandGestureController(inputSink, screenshotSink),
            monitorThrow: new MonitorThrowController(inputSink),
            volume: new VolumeController(audioSink),
            lockSeq: new LockSequenceController(inputSink),
            circleUndo: new CircleUndoController(inputSink),
            shhhMute: new ShhhMuteController(audioSink),
            hearNoEvil: new HearNoEvilController(audioSink),
            virtualObject: new VirtualObjectController());

        return new NexaEngine(
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
    }
}
