using System;
using System.Diagnostics;
using System.Threading;
using NEXA.Abstractions;
using NEXA.Adapters.Output;
using NEXA.Domain.Grab;
using NEXA.Hand;
using OpenCvSharp;
using Xunit;

namespace NEXA.Tests.Domain;

/// <summary>
/// Comprehensive Right-BICEP unit tests for <see cref="WindowSnapEngine"/> 8-zone docking calculations.
/// </summary>
public class WindowSnapEngineTests
{
    // [R]IGHT-BICEP: Verifies that dragging a window to the top-left screen corner engages TopLeftCorner 1/4 screen snap docking.
    [Fact]
    public void Update_WhenMovedToTopLeftCorner_SnapsToQuarterScreenTopLeft()
    {
        // Arrange
        WindowGrabDetector detector = new(1920, 1080);
        IInputSink inputSink = new Win32InputSink();

        detector.State.IsGrabbed = true;
        detector.State.TargetHwnd = new IntPtr(999);
        detector.State.InitialWindowBounds = new Rect(400, 300, 960, 540);
        detector.State.PreSnapBounds = new Rect(400, 300, 960, 540);
        detector.State.InitialHandScreenX = 500;
        detector.State.InitialHandScreenY = 400;

        TrackedHand cornerHand = new() { Gesture = "Fist" };
        cornerHand.SmoothedLandmarks2D[9] = new Point2f(1280 * 0.15f, 720 * 0.15f);

        // Act
        detector.Update(cornerHand, 1280, 720, inputSink);

        // Assert
        Assert.Equal(WindowSnapType.TopLeftCorner, detector.State.ActiveSnap);
        Assert.Equal(960, detector.State.SnapBounds.Width);
        Assert.Equal(540, detector.State.SnapBounds.Height);
    }

    // RIGHT-[B]ICEP: Validates threshold boundary condition where hand coordinate is on the exact snap margin boundary.
    [Fact]
    public void ProcessSnapping_ExactThresholdBoundary_EngagesSnap()
    {
        // Arrange
        WindowSnapEngine engine = new(1920, 1080);
        WindowGrabState state = new();

        // Act
        engine.ProcessSnapping(state, 20, 540, 20, 540, 960, 540, out bool shouldReanchor, out _, out _);

        // Assert
        Assert.True(state.IsSnapped);
        Assert.Equal(WindowSnapType.LeftHalf, state.ActiveSnap);
    }

    // RIGHT-B[I]CEP: Confirms that moving the hand away from snap edge after lock duration fully inverts and un-docks back to floating state.
    [Fact]
    public void SnapToCorner_AndUndock_RevertsToPreSnapFloatingState()
    {
        // Arrange
        WindowGrabDetector detector = new(1920, 1080);
        IInputSink inputSink = new Win32InputSink();

        detector.State.IsGrabbed = true;
        detector.State.TargetHwnd = new IntPtr(999);
        detector.State.InitialWindowBounds = new Rect(400, 300, 960, 540);
        detector.State.PreSnapBounds = new Rect(400, 300, 960, 540);
        detector.State.InitialHandScreenX = 500;
        detector.State.InitialHandScreenY = 400;

        TrackedHand hand = new() { Gesture = "Fist" };
        hand.SmoothedLandmarks2D[9] = new Point2f(1280 * 0.15f, 720 * 0.15f);
        detector.Update(hand, 1280, 720, inputSink);

        // Act
        Thread.Sleep(310);
        hand.SmoothedLandmarks2D[9] = new Point2f(1280 * 0.50f, 720 * 0.50f);
        detector.Update(hand, 1280, 720, inputSink);

        // Assert
        Assert.Equal(WindowSnapType.None, detector.State.ActiveSnap);
        Assert.False(detector.State.IsSnapped);
    }

    // RIGHT-BI[C]HECK: Cross-checks right-half snap bounds calculating exact 50% split width and full screen height.
    [Fact]
    public void ProcessSnapping_RightHalf_CalculatesExactHalfScreenDimensions()
    {
        // Arrange
        WindowSnapEngine engine = new(2560, 1440);
        WindowGrabState state = new();

        // Act
        engine.ProcessSnapping(state, 2550, 720, 2550, 720, 1000, 600, out _, out _, out _);

        // Assert
        Assert.Equal(WindowSnapType.RightHalf, state.ActiveSnap);
        Assert.Equal(1280, state.SnapBounds.X);
        Assert.Equal(0, state.SnapBounds.Y);
        Assert.Equal(1280, state.SnapBounds.Width);
        Assert.Equal(1440, state.SnapBounds.Height);
    }

    // RIGHT-BIC[E]P: Verifies that passing uninitialized or zero window dimensions does not throw exceptions.
    [Fact]
    public void ProcessSnapping_WithZeroDimensions_HandlesSafelyWithoutException()
    {
        // Arrange
        WindowSnapEngine engine = new(1920, 1080);
        WindowGrabState state = new();

        // Act & Assert
        engine.ProcessSnapping(state, 0, 0, 0, 0, 0, 0, out bool shouldReanchor, out int reanchoredX, out int reanchoredY);
        Assert.True(state.IsSnapped);
    }

    // RIGHT-BICE[P]: Ensures 50,000 snap zone evaluations complete well within the real-time frame budget (< 15ms).
    [Fact]
    public void ProcessSnapping_WithHighFrequencyCalls_ExecutesWithinPerformanceBudget()
    {
        // Arrange
        WindowSnapEngine engine = new(1920, 1080);
        WindowGrabState state = new();
        Stopwatch sw = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 50000; i++)
        {
            engine.ProcessSnapping(state, i % 1920, i % 1080, i % 1920, i % 1080, 960, 540, out _, out _, out _);
        }
        sw.Stop();

        // Assert
        Assert.True(sw.ElapsedMilliseconds < 15, $"50k snap evaluations took {sw.ElapsedMilliseconds}ms.");
    }
}
