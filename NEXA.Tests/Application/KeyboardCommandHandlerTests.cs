using System.Diagnostics;
using NEXA.Adapters.Output;
using NEXA.Application;
using NEXA.Face;
using NEXA.Hand;
using Xunit;

namespace NEXA.Tests.Application;

/// <summary>
/// Comprehensive Right-BICEP unit tests for <see cref="KeyboardCommandHandler"/>.
/// </summary>
public class KeyboardCommandHandlerTests
{
    private readonly KeyboardCommandHandler _handler = new();
    private readonly HandTracker _handTracker = new("dummy_palm.onnx", "dummy_landmark.onnx");
    private readonly HandMeshRenderer _handRenderer = new();
    private readonly FaceMeshRenderer _faceRenderer = new();
    private readonly NexaControllerBundle _controllers;
    private bool _showHud = true;

    public KeyboardCommandHandlerTests()
    {
        Fakes.FakeAudioSink audioSink = new();
        Fakes.FakeInputSink inputSink = new();
        Fakes.FakeScreenshotSink screenshotSink = new();

        _controllers = new NexaControllerBundle(
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
    }

    // [R]IGHT-BICEP: Verifies that hotkey 'C' correctly toggles mouse controller enabled state.
    [Fact]
    public void ProcessKey_WithKeyC_TogglesMouseController()
    {
        // Arrange
        bool initialEnabled = _controllers.Mouse.Enabled;

        // Act
        bool shouldContinue = _handler.ProcessKey(
            'c',
            1280,
            720,
            _handTracker,
            _handRenderer,
            _faceRenderer,
            _controllers,
            ref _showHud);

        // Assert
        Assert.True(shouldContinue);
        Assert.NotEqual(initialEnabled, _controllers.Mouse.Enabled);
    }

    // RIGHT-[B]ICEP: Validates exit keycode boundary conditions (ESC, 'q', 'Q') return false to terminate the loop.
    [Theory]
    [InlineData(27)]
    [InlineData('q')]
    [InlineData('Q')]
    public void ProcessKey_WithExitKeycodes_ReturnsFalseToTerminate(int exitKey)
    {
        // Arrange & Act
        bool shouldContinue = _handler.ProcessKey(
            exitKey,
            1280,
            720,
            _handTracker,
            _handRenderer,
            _faceRenderer,
            _controllers,
            ref _showHud);

        // Assert
        Assert.False(shouldContinue);
    }

    // RIGHT-B[I]CEP: Confirms that toggling a hotkey twice in succession restores the original boolean state.
    [Fact]
    public void ProcessKey_TogglingKeyTwice_RestoresOriginalState()
    {
        // Arrange
        bool originalState = _controllers.Volume.Enabled;

        // Act
        _handler.ProcessKey('v', 1280, 720, _handTracker, _handRenderer, _faceRenderer, _controllers, ref _showHud);
        _handler.ProcessKey('v', 1280, 720, _handTracker, _handRenderer, _faceRenderer, _controllers, ref _showHud);

        // Assert
        Assert.Equal(originalState, _controllers.Volume.Enabled);
    }

    // RIGHT-BI[C]HECK: Cross-checks uppercase and lowercase key equivalents producing identical state transformations.
    [Fact]
    public void ProcessKey_UpperAndLowerCase_ProduceIdenticalStateTransformations()
    {
        // Arrange
        bool initialGrab = _controllers.WindowGrab.Enabled;

        // Act
        _handler.ProcessKey('g', 1280, 720, _handTracker, _handRenderer, _faceRenderer, _controllers, ref _showHud);
        bool afterLower = _controllers.WindowGrab.Enabled;

        _handler.ProcessKey('G', 1280, 720, _handTracker, _handRenderer, _faceRenderer, _controllers, ref _showHud);
        bool afterUpper = _controllers.WindowGrab.Enabled;

        // Assert
        Assert.NotEqual(initialGrab, afterLower);
        Assert.Equal(initialGrab, afterUpper);
    }

    // RIGHT-BIC[E]P: Verifies that unmapped or invalid keycodes do not throw exceptions and continue stream processing.
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(9999)]
    public void ProcessKey_WithUnmappedKeycodes_ContinuesWithoutAlteringState(int unmappedKey)
    {
        // Arrange
        bool initialHud = _showHud;

        // Act
        bool shouldContinue = _handler.ProcessKey(
            unmappedKey,
            1280,
            720,
            _handTracker,
            _handRenderer,
            _faceRenderer,
            _controllers,
            ref _showHud);

        // Assert
        Assert.True(shouldContinue);
        Assert.Equal(initialHud, _showHud);
    }

    // RIGHT-BICE[P]: Ensures 10,000 rapid keystroke evaluations complete well within strict real-time frame budgets (< 50ms).
    [Fact]
    public void ProcessKey_WithRapidKeyStream_ExecutesWithinFrameBudget()
    {
        // Arrange
        Stopwatch sw = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 10000; i++)
        {
            _handler.ProcessKey(
                'w',
                1280,
                720,
                _handTracker,
                _handRenderer,
                _faceRenderer,
                _controllers,
                ref _showHud);
        }
        sw.Stop();

        // Assert
        Assert.True(sw.ElapsedMilliseconds < 50, $"Key processing took {sw.ElapsedMilliseconds}ms for 10k iterations.");
    }
}
