using System;
using System.Threading;
using NEXA.Adapters.Output;
using NEXA.Domain.Grab;
using NEXA.Hand;
using OpenCvSharp;
using Xunit;

namespace NEXA.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="WindowSnapEngine"/> and 8-zone docking / un-docking calculations.
/// </summary>
public class WindowSnapEngineTests
{
    [Fact]
    public void SnapToCorner_AndUndock_TransitionsCorrectly()
    {
        WindowGrabDetector testSnapDetector = new(1920, 1080);
        Win32InputSink inputSink = new();

        testSnapDetector.State.IsGrabbed = true;
        testSnapDetector.State.TargetHwnd = new IntPtr(999);
        testSnapDetector.State.InitialWindowBounds = new Rect(400, 300, 960, 540);
        testSnapDetector.State.PreSnapBounds = new Rect(400, 300, 960, 540);
        testSnapDetector.State.InitialHandScreenX = 500;
        testSnapDetector.State.InitialHandScreenY = 400;

        TrackedHand edgeHand = new() { Gesture = "Fist" };
        edgeHand.SmoothedLandmarks2D[9] = new Point2f(1280 * 0.15f, 720 * 0.15f);

        testSnapDetector.Update(edgeHand, 1280, 720, inputSink);
        Assert.Equal(WindowSnapType.TopLeftCorner, testSnapDetector.State.ActiveSnap);
        Assert.Equal(1920 / 2, testSnapDetector.State.SnapBounds.Width);
        Assert.Equal(1080 / 2, testSnapDetector.State.SnapBounds.Height);

        Thread.Sleep(310);
        edgeHand.SmoothedLandmarks2D[9] = new Point2f(1280 * 0.45f, 720 * 0.45f);
        testSnapDetector.Update(edgeHand, 1280, 720, inputSink);

        Assert.Equal(WindowSnapType.None, testSnapDetector.State.ActiveSnap);
    }
}
