using System.Diagnostics;
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
    private readonly VirtualObjectController _virtualObject = new();
    private readonly MouseController _mouseController = new();
    private readonly ScrollController _scrollController = new();
    private readonly WindowGrabController _windowGrabController = new();
    private readonly TwoHandGestureController _twoHandController = new();
    private readonly MonitorThrowController _monitorThrowController = new();
    private readonly VolumeController _volumeController = new();
    private readonly LockSequenceController _lockController = new();
    private readonly CircleUndoController _circleUndoController = new();
    private readonly ShhhMuteController _shhhMuteController = new();
    private readonly HearNoEvilController _hearNoEvilController = new(new Win32AudioSink());
    private bool _showHud = true;

    // [R]IGHT-BICEP: Verifies that hotkey 'C' correctly toggles mouse controller enabled state.
    [Fact]
    public void ProcessKey_WithKeyC_TogglesMouseController()
    {
        // Arrange
        bool initialEnabled = _mouseController.Enabled;

        // Act
        bool shouldContinue = _handler.ProcessKey(
            'c',
            1280,
            720,
            _handTracker,
            _handRenderer,
            _faceRenderer,
            _virtualObject,
            _mouseController,
            _scrollController,
            _windowGrabController,
            _twoHandController,
            _monitorThrowController,
            _volumeController,
            _lockController,
            _circleUndoController,
            _shhhMuteController,
            _hearNoEvilController,
            ref _showHud);

        // Assert
        Assert.True(shouldContinue);
        Assert.NotEqual(initialEnabled, _mouseController.Enabled);
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
            _virtualObject,
            _mouseController,
            _scrollController,
            _windowGrabController,
            _twoHandController,
            _monitorThrowController,
            _volumeController,
            _lockController,
            _circleUndoController,
            _shhhMuteController,
            _hearNoEvilController,
            ref _showHud);

        // Assert
        Assert.False(shouldContinue);
    }

    // RIGHT-B[I]CEP: Confirms that toggling a hotkey twice in succession restores the original boolean state.
    [Fact]
    public void ProcessKey_TogglingKeyTwice_RestoresOriginalState()
    {
        // Arrange
        bool originalState = _volumeController.Enabled;

        // Act
        _handler.ProcessKey('v', 1280, 720, _handTracker, _handRenderer, _faceRenderer, _virtualObject, _mouseController, _scrollController, _windowGrabController, _twoHandController, _monitorThrowController, _volumeController, _lockController, _circleUndoController, _shhhMuteController, _hearNoEvilController, ref _showHud);
        _handler.ProcessKey('v', 1280, 720, _handTracker, _handRenderer, _faceRenderer, _virtualObject, _mouseController, _scrollController, _windowGrabController, _twoHandController, _monitorThrowController, _volumeController, _lockController, _circleUndoController, _shhhMuteController, _hearNoEvilController, ref _showHud);

        // Assert
        Assert.Equal(originalState, _volumeController.Enabled);
    }

    // RIGHT-BI[C]HECK: Cross-checks uppercase and lowercase key equivalents producing identical state transformations.
    [Fact]
    public void ProcessKey_UpperAndLowerCase_ProduceIdenticalStateTransformations()
    {
        // Arrange
        bool initialGrab = _windowGrabController.Enabled;

        // Act
        _handler.ProcessKey('g', 1280, 720, _handTracker, _handRenderer, _faceRenderer, _virtualObject, _mouseController, _scrollController, _windowGrabController, _twoHandController, _monitorThrowController, _volumeController, _lockController, _circleUndoController, _shhhMuteController, _hearNoEvilController, ref _showHud);
        bool afterLower = _windowGrabController.Enabled;

        _handler.ProcessKey('G', 1280, 720, _handTracker, _handRenderer, _faceRenderer, _virtualObject, _mouseController, _scrollController, _windowGrabController, _twoHandController, _monitorThrowController, _volumeController, _lockController, _circleUndoController, _shhhMuteController, _hearNoEvilController, ref _showHud);
        bool afterUpper = _windowGrabController.Enabled;

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
            _virtualObject,
            _mouseController,
            _scrollController,
            _windowGrabController,
            _twoHandController,
            _monitorThrowController,
            _volumeController,
            _lockController,
            _circleUndoController,
            _shhhMuteController,
            _hearNoEvilController,
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
                _virtualObject,
                _mouseController,
                _scrollController,
                _windowGrabController,
                _twoHandController,
                _monitorThrowController,
                _volumeController,
                _lockController,
                _circleUndoController,
                _shhhMuteController,
                _hearNoEvilController,
                ref _showHud);
        }
        sw.Stop();

        // Assert
        Assert.True(sw.ElapsedMilliseconds < 50, $"Key processing took {sw.ElapsedMilliseconds}ms for 10k iterations.");
    }
}
