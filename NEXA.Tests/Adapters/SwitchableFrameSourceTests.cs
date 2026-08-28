using System;
using NEXA.Adapters.Capture;
using OpenCvSharp;
using Xunit;

namespace NEXA.Tests.Adapters;

/// <summary>
/// Right-BICEP unit tests for <see cref="SwitchableFrameSource"/> validating dynamic mode switching between local USB webcam and remote smartphone stream.
/// </summary>
public class SwitchableFrameSourceTests : IDisposable
{
    private readonly SwitchableFrameSource _switchableSource;

    public SwitchableFrameSourceTests()
    {
        _switchableSource = new SwitchableFrameSource(initialMode: CameraSourceMode.LocalWebcam);
    }

    public void Dispose()
    {
        _switchableSource.Dispose();
    }

    // [R]IGHT-BICEP: Default mode is LocalWebcam.
    [Fact]
    public void Constructor_DefaultMode_IsLocalWebcam()
    {
        Assert.Equal(CameraSourceMode.LocalWebcam, _switchableSource.CurrentMode);
        Assert.NotNull(_switchableSource.WebcamSource);
        Assert.NotNull(_switchableSource.RemoteSource);
    }

    // RIGHT-B[I]CEP: Inverting mode to SmartphoneStream switches active provider and receiver state.
    [Fact]
    public void SwitchMode_ToSmartphoneStream_ActivatesRemoteReceiver()
    {
        // Act
        bool switched = _switchableSource.SwitchMode(CameraSourceMode.SmartphoneStream);

        // Assert
        Assert.True(switched);
        Assert.Equal(CameraSourceMode.SmartphoneStream, _switchableSource.CurrentMode);
        Assert.True(_switchableSource.RemoteSource.IsReceiverRunning);
    }

    // RIGHT-BI[C]EP: Ingesting frame into remote source reads out through SwitchableFrameSource when in SmartphoneStream mode.
    [Fact]
    public void Read_InSmartphoneMode_ReadsFromRemoteSource()
    {
        // Arrange
        _switchableSource.SwitchMode(CameraSourceMode.SmartphoneStream);
        using Mat synthetic = new(480, 640, MatType.CV_8UC3, new Scalar(100, 200, 50));
        Cv2.ImEncode(".jpg", synthetic, out byte[] jpgBytes);
        _switchableSource.RemoteSource.IngestEncodedFrame(jpgBytes);

        // Act
        using Mat dest = new();
        bool readSuccess = _switchableSource.Read(dest);

        // Assert
        Assert.True(readSuccess);
        Assert.False(dest.Empty());
        Assert.Equal(480, dest.Rows);
        Assert.Equal(640, dest.Cols);
    }

    // RIGHT-[B]OUNDARY: Switching to same mode returns true immediately without re-initializing.
    [Fact]
    public void SwitchMode_SameMode_ReturnsTrue()
    {
        _switchableSource.Open(0);
        bool result = _switchableSource.SwitchMode(CameraSourceMode.LocalWebcam);
        Assert.True(result);
    }
}
