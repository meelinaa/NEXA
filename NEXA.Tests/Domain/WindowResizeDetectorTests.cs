using NEXA.Domain.Grab;
using NEXA.Hand;
using OpenCvSharp;
using Xunit;

namespace NEXA.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="WindowResizeDetector"/> evaluating continuous pinch-to-zoom scaling factors.
/// </summary>
public class WindowResizeDetectorTests
{
    [Fact]
    public void PinchGesture_ScalesWindowDimensions()
    {
        WindowResizeDetector testResize = new();
        TrackedHand zoomHand = new() { Gesture = "L" };
        zoomHand.SmoothedLandmarks2D[0] = new Point2f(500, 500);
        zoomHand.SmoothedLandmarks2D[9] = new Point2f(500, 400);
        zoomHand.SmoothedLandmarks2D[4] = new Point2f(470, 400);
        zoomHand.SmoothedLandmarks2D[8] = new Point2f(530, 400);

        (bool shouldResize1, int winW1, int winH1) = testResize.Update(zoomHand, 800, 600, 1920, 1080);
        Assert.True(testResize.State.IsActive, "WindowResizeDetector should be active on L gesture.");

        zoomHand.SmoothedLandmarks2D[4] = new Point2f(440, 400);
        zoomHand.SmoothedLandmarks2D[8] = new Point2f(560, 400);
        (bool shouldResize2, int winW2, int winH2) = testResize.Update(zoomHand, 800, 600, 1920, 1080);

        Assert.True(shouldResize2, "Should trigger resize on wider aperture.");
        Assert.True(winW2 > 800, "Target window width should scale up.");
    }
}
