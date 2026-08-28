using System;
using System.Threading;
using NEXA.Adapters.Output;
using NEXA.Domain.MonitorThrow;
using NEXA.Hand;
using OpenCvSharp;
using Xunit;

namespace NEXA.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="MonitorThrowDetector"/> evaluating edge-on hand posture and cross-display swipe kinematics.
/// </summary>
public class MonitorThrowDetectorTests
{
    [Fact]
    public void EdgeOnHand_SwipedRight_TriggersMonitorThrow()
    {
        MonitorThrowDetector testThrowDetector = new();
        Fakes.FakeInputSink inputSink = new();
        inputSink.LastFocusedHwnd = new IntPtr(12345);

        TrackedHand bladeHand = new() { Gesture = "Open Palm" };
        bladeHand.SmoothedLandmarks2D[0] = new Point2f(500, 500);
        bladeHand.SmoothedLandmarks2D[9] = new Point2f(500, 400);
        bladeHand.SmoothedLandmarks2D[5] = new Point2f(515, 410);
        bladeHand.SmoothedLandmarks2D[17] = new Point2f(535, 410);

        testThrowDetector.Update(bladeHand, inputSink);
        Thread.Sleep(30);
        bladeHand.SmoothedLandmarks2D[9] = new Point2f(540, 400);
        testThrowDetector.Update(bladeHand, inputSink);
        Thread.Sleep(30);
        bladeHand.SmoothedLandmarks2D[9] = new Point2f(590, 400);
        MonitorThrowDecision? throwDecision = testThrowDetector.Update(bladeHand, inputSink);

        Assert.NotNull(throwDecision);
        Assert.Equal(MonitorThrowDirection.Right, throwDecision.Direction);
    }
}
