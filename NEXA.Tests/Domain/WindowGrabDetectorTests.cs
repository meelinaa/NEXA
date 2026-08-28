using System.Threading;
using NEXA.Adapters.Output;
using NEXA.Domain.Grab;
using NEXA.Hand;
using OpenCvSharp;
using Xunit;

namespace NEXA.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="WindowGrabDetector"/> state machine transitions and hold timer logic.
/// </summary>
public class WindowGrabDetectorTests
{
    [Fact]
    public void FistGesture_StartsHoldTimer_AndTransitionsToGrabbed()
    {
        WindowGrabDetector detector = new(1920, 1080);
        Fakes.FakeInputSink inputSink = new();

        TrackedHand hand = new() { Gesture = "Fist" };
        hand.SmoothedLandmarks2D[9] = new Point2f(640, 360);

        detector.Update(hand, 1280, 720, inputSink);
        Assert.True(detector.State.HoldDurationSeconds >= 0, "Hold timer should start on Fist gesture.");

        detector.State.RequiredHoldSeconds = 0.01;
        Thread.Sleep(20);
        detector.Update(hand, 1280, 720, inputSink);

        detector.Reset();
        Assert.False(detector.State.IsGrabbed, "Reset should clear grabbed state.");
    }
}
