using System;
using NEXA.Abstractions;
using OpenCvSharp;
using Serilog;

namespace NEXA.Adapters.Capture;

/// <summary>
/// Operating modes for camera video source selection.
/// </summary>
public enum CameraSourceMode
{
    /// <summary>
    /// Local USB Webcam directly connected to the host PC.
    /// </summary>
    LocalWebcam,

    /// <summary>
    /// Wireless Smartphone Camera or Remote WebRTC/MJPEG Stream.
    /// </summary>
    SmartphoneStream
}

/// <summary>
/// Composite and switchable frame source orchestrating both local hardware webcams and wireless smartphone streaming sources,
/// enabling dynamic hot-switching between inputs at runtime.
/// </summary>
public class SwitchableFrameSource : IFrameSource
{
    private static readonly ILogger Logger = Log.ForContext<SwitchableFrameSource>();

    private readonly OpenCvFrameSource _webcamSource;
    private readonly RemoteStreamFrameSource _remoteSource;
    private CameraSourceMode _currentMode = CameraSourceMode.LocalWebcam;
    private int _currentWebcamIndex = 0;
    private bool _isOpened = false;

    /// <summary>
    /// Gets the currently active camera source mode.
    /// </summary>
    public CameraSourceMode CurrentMode => _currentMode;

    /// <summary>
    /// Gets the inner local USB webcam frame source.
    /// </summary>
    public OpenCvFrameSource WebcamSource => _webcamSource;

    /// <summary>
    /// Gets the inner remote / smartphone frame source.
    /// </summary>
    public RemoteStreamFrameSource RemoteSource => _remoteSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="SwitchableFrameSource"/> class.
    /// </summary>
    /// <param name="webcamSource">Optional custom local webcam source.</param>
    /// <param name="remoteSource">Optional custom remote stream source.</param>
    /// <param name="initialMode">The initial camera mode to activate (default: LocalWebcam).</param>
    public SwitchableFrameSource(
        OpenCvFrameSource? webcamSource = null,
        RemoteStreamFrameSource? remoteSource = null,
        CameraSourceMode initialMode = CameraSourceMode.LocalWebcam)
    {
        _webcamSource = webcamSource ?? new OpenCvFrameSource();
        _remoteSource = remoteSource ?? new RemoteStreamFrameSource();
        _currentMode = initialMode;
    }

    /// <inheritdoc/>
    public bool Open(int index)
    {
        _currentWebcamIndex = index;
        _isOpened = true;

        if (_currentMode == CameraSourceMode.LocalWebcam)
        {
            Logger.Information("Opening local USB Webcam at index {Index}", index);
            return _webcamSource.Open(index);
        }
        else
        {
            Logger.Information("Starting remote smartphone camera receiver...");
            return _remoteSource.Open(index);
        }
    }

    /// <summary>
    /// Switches the active video input source dynamically between local USB webcam and smartphone camera.
    /// </summary>
    /// <param name="newMode">The target mode to switch to.</param>
    /// <returns><c>true</c> if switched successfully; otherwise, <c>false</c>.</returns>
    public bool SwitchMode(CameraSourceMode newMode)
    {
        if (_currentMode == newMode && _isOpened)
        {
            return true;
        }

        Logger.Information("Switching camera mode from {OldMode} to {NewMode}", _currentMode, newMode);
        _currentMode = newMode;

        if (newMode == CameraSourceMode.LocalWebcam)
        {
            return _webcamSource.Open(_currentWebcamIndex);
        }
        else
        {
            return _remoteSource.StartEmbeddedReceiver(8080);
        }
    }

    /// <summary>
    /// Connects to a custom remote video stream URL (RTSP, HTTP, MJPEG, WebRTC stream feed).
    /// </summary>
    /// <param name="streamUrl">The network stream URL.</param>
    /// <returns><c>true</c> if stream opened; otherwise, <c>false</c>.</returns>
    public bool ConnectToRemoteStream(string streamUrl)
    {
        _currentMode = CameraSourceMode.SmartphoneStream;
        return _remoteSource.OpenStreamUrl(streamUrl);
    }

    /// <inheritdoc/>
    public bool IsOpened()
    {
        return _currentMode switch
        {
            CameraSourceMode.LocalWebcam => _webcamSource.IsOpened(),
            CameraSourceMode.SmartphoneStream => _remoteSource.IsOpened(),
            _ => false
        };
    }

    /// <inheritdoc/>
    public bool Read(Mat image)
    {
        return _currentMode switch
        {
            CameraSourceMode.LocalWebcam => _webcamSource.Read(image),
            CameraSourceMode.SmartphoneStream => _remoteSource.Read(image),
            _ => false
        };
    }

    /// <inheritdoc/>
    public bool Set(VideoCaptureProperties property, double value)
    {
        return _currentMode switch
        {
            CameraSourceMode.LocalWebcam => _webcamSource.Set(property, value),
            CameraSourceMode.SmartphoneStream => _remoteSource.Set(property, value),
            _ => true
        };
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _webcamSource.Dispose();
        _remoteSource.Dispose();
        _isOpened = false;
        GC.SuppressFinalize(this);
    }
}
