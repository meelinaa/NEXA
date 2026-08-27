using System.Threading;
using NEXA.Domain.Click;
using NEXA.Hand;
using OpenCvSharp;
using Xunit;

namespace NEXA.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="DwellClickDetector"/> evaluating pointing posture and dwell hover clicking.
/// </summary>
public class DwellClickDetectorTests
{
    [Fact]
    public void PointingGesture_DwellHold_TriggersClick()
    {
        DwellClickDetector testDwell = new(1920, 1080);
        testDwell.DwellState.RequiredDwellSeconds = 0.01;

        TrackedHand pointHand = new() { Gesture = "Pointing" };
        pointHand.SmoothedLandmarks2D[8] = new Point2f(640, 360);

        testDwell.Update(pointHand, 1280, 720);
        Thread.Sleep(20);
        (int? _, int? _, bool didClick) = testDwell.Update(pointHand, 1280, 720);

        Assert.True(didClick, "DwellClick should trigger after hold duration.");
    }
}
