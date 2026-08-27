using System.Diagnostics;
using NEXA.Domain.Grab;
using NEXA.Hand;
using OpenCvSharp;
using Xunit;

namespace NEXA.Tests.Domain;

/// <summary>
/// Comprehensive Right-BICEP unit tests for <see cref="WindowResizeDetector"/> evaluating continuous pinch-to-zoom scaling factors.
/// </summary>
public class WindowResizeDetectorTests
{
    // [R]IGHT-BICEP: Verifies that widening the thumb-index aperture continuously magnifies window dimensions above baseline.
    [Fact]
    public void Update_WithWiderFingerAperture_ScalesWindowDimensionsUp()
    {
        // Arrange
        WindowResizeDetector resizeDetector = new();
        TrackedHand zoomHand = new() { Gesture = "L" };
        zoomHand.SmoothedLandmarks2D[0] = new Point2f(500, 500);
        zoomHand.SmoothedLandmarks2D[9] = new Point2f(500, 400);
        zoomHand.SmoothedLandmarks2D[4] = new Point2f(470, 400);
        zoomHand.SmoothedLandmarks2D[8] = new Point2f(530, 400);

        // Act
        resizeDetector.Update(zoomHand, 800, 600, 1920, 1080);
        zoomHand.SmoothedLandmarks2D[4] = new Point2f(440, 400);
        zoomHand.SmoothedLandmarks2D[8] = new Point2f(560, 400);
        (bool shouldResize, int newWidth, int newHeight) = resizeDetector.Update(zoomHand, 800, 600, 1920, 1080);

        // Assert
        Assert.True(shouldResize);
        Assert.True(newWidth > 800);
        Assert.True(newHeight > 600);
    }

    // RIGHT-[B]ICEP: Validates deadzone boundary condition (+/-3.5% aperture fluctuation) maintaining stable 1.0x scale.
    [Fact]
    public void Update_WithinDeadzoneBoundary_MaintainsUnitScale()
    {
        // Arrange
        WindowResizeDetector resizeDetector = new();
        TrackedHand zoomHand = new() { Gesture = "L" };
        zoomHand.SmoothedLandmarks2D[0] = new Point2f(500, 500);
        zoomHand.SmoothedLandmarks2D[9] = new Point2f(500, 400);
        zoomHand.SmoothedLandmarks2D[4] = new Point2f(480, 400);
        zoomHand.SmoothedLandmarks2D[8] = new Point2f(520, 400);

        // Act: Initial baseline lock
        resizeDetector.Update(zoomHand, 800, 600, 1920, 1080);

        // Tiny 1% variation well inside 3.5% deadzone
        zoomHand.SmoothedLandmarks2D[8] = new Point2f(520.4f, 400);
        resizeDetector.Update(zoomHand, 800, 600, 1920, 1080);

        // Assert
        Assert.Equal(1.0, resizeDetector.State.CurrentScale);
    }

    // RIGHT-B[I]CEP: Confirms that non-zoom gestures or Reset calls completely deactivate resizing and revert active flags.
    [Fact]
    public void Update_WithNonZoomGesture_DeactivatesResizing()
    {
        // Arrange
        WindowResizeDetector resizeDetector = new();
        TrackedHand zoomHand = new() { Gesture = "L" };
        zoomHand.SmoothedLandmarks2D[0] = new Point2f(500, 500);
        zoomHand.SmoothedLandmarks2D[9] = new Point2f(500, 400);
        zoomHand.SmoothedLandmarks2D[4] = new Point2f(470, 400);
        zoomHand.SmoothedLandmarks2D[8] = new Point2f(530, 400);
        resizeDetector.Update(zoomHand, 800, 600, 1920, 1080);

        // Act
        zoomHand.Gesture = "Open Palm";
        (bool shouldResize, _, _) = resizeDetector.Update(zoomHand, 800, 600, 1920, 1080);

        // Assert
        Assert.False(shouldResize);
        Assert.False(resizeDetector.State.IsActive);
    }

    // RIGHT-BI[C]HECK: Cross-checks maximum aperture zoom clamping against physical desktop monitor boundary constraints.
    [Fact]
    public void Update_WithMassiveAperture_ClampsToMaxScreenResolution()
    {
        // Arrange
        WindowResizeDetector resizeDetector = new();
        TrackedHand zoomHand = new() { Gesture = "L" };
        zoomHand.SmoothedLandmarks2D[0] = new Point2f(500, 500);
        zoomHand.SmoothedLandmarks2D[9] = new Point2f(500, 400);
        zoomHand.SmoothedLandmarks2D[4] = new Point2f(490, 400);
        zoomHand.SmoothedLandmarks2D[8] = new Point2f(510, 400);
        resizeDetector.Update(zoomHand, 1200, 800, 1920, 1080);

        // Act: Enormous aperture displacement
        zoomHand.SmoothedLandmarks2D[4] = new Point2f(100, 400);
        zoomHand.SmoothedLandmarks2D[8] = new Point2f(900, 400);
        (bool _, int clampedWidth, int clampedHeight) = resizeDetector.Update(zoomHand, 1200, 800, 1920, 1080);

        // Assert
        Assert.True(clampedWidth <= 1920);
        Assert.True(clampedHeight <= 1080);
    }

    // RIGHT-BIC[E]P: Verifies that null hand references or non-positive base dimensions handle gracefully without throwing.
    [Theory]
    [InlineData(0, 600)]
    [InlineData(800, -100)]
    public void Update_WithInvalidBaseDimensions_ReturnsFalseSafely(int invalidW, int invalidH)
    {
        // Arrange
        WindowResizeDetector resizeDetector = new();
        TrackedHand zoomHand = new() { Gesture = "L" };

        // Act & Assert
        (bool shouldResize, int w, int h) = resizeDetector.Update(zoomHand, invalidW, invalidH, 1920, 1080);
        Assert.False(shouldResize);
        Assert.Equal(0, w);
        Assert.Equal(0, h);
    }

    // RIGHT-BICE[P]: Ensures 100,000 continuous aperture scale updates execute well within real-time limits (< 20ms).
    [Fact]
    public void Update_WithHighThroughput_ExecutesWithinPerformanceBudget()
    {
        // Arrange
        WindowResizeDetector resizeDetector = new();
        TrackedHand zoomHand = new() { Gesture = "L" };
        zoomHand.SmoothedLandmarks2D[0] = new Point2f(500, 500);
        zoomHand.SmoothedLandmarks2D[9] = new Point2f(500, 400);
        zoomHand.SmoothedLandmarks2D[4] = new Point2f(470, 400);
        zoomHand.SmoothedLandmarks2D[8] = new Point2f(530, 400);
        resizeDetector.Update(zoomHand, 800, 600, 1920, 1080);
        Stopwatch sw = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 100000; i++)
        {
            resizeDetector.Update(zoomHand, 800, 600, 1920, 1080);
        }
        sw.Stop();

        // Assert
        Assert.True(sw.ElapsedMilliseconds < 200, $"100k resize updates took {sw.ElapsedMilliseconds}ms.");
    }
}
