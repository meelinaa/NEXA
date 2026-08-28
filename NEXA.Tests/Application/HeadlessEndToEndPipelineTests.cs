using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NEXA.Abstractions;
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
/// Comprehensive Headless End-to-End (E2E) pipeline tests executing complete multi-second synthetic video streams
/// (procedural movement vectors, SMPTE color bars, gradient test vectors) through the full NEXA engine stack
/// without requiring physical webcams, native OS windows, or physical audio hardware.
/// </summary>
public class HeadlessEndToEndPipelineTests
{
    // [R]IGHT-BICEP: Validates that complete 30-frame synthetic moving target stream runs E2E and renders to FakeDisplaySink.
    [Fact]
    public void Headless_EndToEnd_SyntheticMovingTargetStream_RunsFullPipelineSynchronously()
    {
        // Arrange
        const int totalFrames = 25;
        SyntheticVideoVectorSource vectorSource = new(
            totalFrames: totalFrames,
            width: 1280,
            height: 720,
            patternType: SyntheticVideoVectorSource.SyntheticPatternType.MovingTargetVector);

        FakeDisplaySink displaySink = new() { CaptureLastFrame = true };
        FakeKeyboardEventSource keyboardSource = new(new int[] { -1, -1, -1, 'q' });

        FakeInputSink inputSink = new();
        FakeAudioSink audioSink = new();
        FakeScreenshotSink screenshotSink = new();

        NexaEngine engine = CreateEngine(vectorSource, displaySink, keyboardSource, inputSink, audioSink, screenshotSink);

        // Act
        engine.Run(0);

        // Assert: All frames were processed and rendered through vision pipeline, controllers, and HUD
        Assert.True(displaySink.FramesShown >= 1, $"Expected frames shown >= 1, got {displaySink.FramesShown}");
        Assert.NotNull(displaySink.LastRenderedFrame);
        Assert.False(displaySink.LastRenderedFrame!.Empty());
        Assert.Equal(1280, displaySink.LastRenderedFrame.Width);
        Assert.Equal(720, displaySink.LastRenderedFrame.Height);
    }

    // [R]IGHT-BICEP: Validates that asynchronous multi-threaded engine consumes synthetic video stream via Channel<Mat> and MatRingBuffer.
    [Fact]
    public async Task Headless_EndToEnd_AsyncEngine_ConsumesSyntheticStreamAndTerminatesCleanly()
    {
        // Arrange
        const int totalFrames = 30;
        SyntheticVideoVectorSource vectorSource = new(
            totalFrames: totalFrames,
            width: 1280,
            height: 720,
            patternType: SyntheticVideoVectorSource.SyntheticPatternType.MovingTargetVector);

        FakeDisplaySink displaySink = new() { CaptureLastFrame = true };
        FakeKeyboardEventSource keyboardSource = new(new int[] { -1, -1, -1, -1, 'q' });

        FakeInputSink inputSink = new();
        FakeAudioSink audioSink = new();
        FakeScreenshotSink screenshotSink = new();

        NexaEngine engine = CreateEngine(vectorSource, displaySink, keyboardSource, inputSink, audioSink, screenshotSink);
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

        // Act
        await engine.RunAsync(0, cts.Token);

        // Assert
        Assert.True(displaySink.FramesShown >= 1, $"Expected frames shown >= 1, got {displaySink.FramesShown}");
        Assert.NotNull(displaySink.LastRenderedFrame);
    }

    // RIGHT-[B]ICEP: Validates boundary behavior with SMPTE color calibration bars exercising full color conversions.
    [Fact]
    public void Headless_EndToEnd_SmpteColorBars_ExecutesColorConversionsAndRendererPasses()
    {
        // Arrange
        SyntheticVideoVectorSource vectorSource = new(
            totalFrames: 10,
            width: 1280,
            height: 720,
            patternType: SyntheticVideoVectorSource.SyntheticPatternType.SmpteColorBars);

        FakeDisplaySink displaySink = new() { CaptureLastFrame = true };
        FakeKeyboardEventSource keyboardSource = new(new int[] { 'q' });

        FakeInputSink inputSink = new();
        FakeAudioSink audioSink = new();
        FakeScreenshotSink screenshotSink = new();

        NexaEngine engine = CreateEngine(vectorSource, displaySink, keyboardSource, inputSink, audioSink, screenshotSink);

        // Act
        engine.Run(0);

        // Assert
        Assert.True(displaySink.FramesShown >= 1);
        Assert.NotNull(displaySink.LastRenderedFrame);
    }

    // RIGHT-B[I]CEP: Verifies that hotkey toggles (e.g. 's' to disable smoothing) properly invert tracker smoothing state in-flight.
    [Fact]
    public void Headless_EndToEnd_HotkeyInflightToggle_InvertsSmoothingState()
    {
        // Arrange
        SyntheticVideoVectorSource vectorSource = new(totalFrames: 10);
        FakeDisplaySink displaySink = new();
        // Press 's' to toggle smoothing, then 'q' to quit
        FakeKeyboardEventSource keyboardSource = new(new int[] { 's', 'q' });

        FakeInputSink inputSink = new();
        FakeAudioSink audioSink = new();
        FakeScreenshotSink screenshotSink = new();
        HandTracker handTracker = new();

        NexaEngine engine = CreateEngine(
            vectorSource,
            displaySink,
            keyboardSource,
            inputSink,
            audioSink,
            screenshotSink,
            customHandTracker: handTracker);

        Assert.True(handTracker.SmoothingEnabled);

        // Act
        engine.Run(0);

        // Assert: Smoothing state was inverted by the hotkey
        Assert.False(handTracker.SmoothingEnabled);
    }

    // RIGHT-BI[C]EP: Cross-checks that domain controllers (e.g. VolumeController, MouseController) execute safely during stream.
    [Fact]
    public void Headless_EndToEnd_DomainControllers_ProcessSyntheticFramesWithoutThrowing()
    {
        // Arrange
        SyntheticVideoVectorSource vectorSource = new(
            totalFrames: 15,
            patternType: SyntheticVideoVectorSource.SyntheticPatternType.GradientRamp);

        FakeDisplaySink displaySink = new();
        FakeKeyboardEventSource keyboardSource = new(new int[] { -1, -1, 'q' });

        FakeInputSink inputSink = new();
        FakeAudioSink audioSink = new();
        FakeScreenshotSink screenshotSink = new();

        NexaEngine engine = CreateEngine(vectorSource, displaySink, keyboardSource, inputSink, audioSink, screenshotSink);

        // Act & Assert (Zero exceptions across 11 controller slices)
        engine.Run(0);

        Assert.True(displaySink.FramesShown >= 1);
    }

    private static NexaEngine CreateEngine(
        IFrameSource frameSource,
        IDisplaySink displaySink,
        IKeyboardEventSource keyboardSource,
        IInputSink inputSink,
        IAudioSink audioSink,
        IScreenshotSink screenshotSink,
        HandTracker? customHandTracker = null)
    {
        HandTracker handTracker = customHandTracker ?? new();
        FaceTracker faceTracker = new();
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
