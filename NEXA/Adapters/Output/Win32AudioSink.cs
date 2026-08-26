using System;
using System.Runtime.InteropServices;

namespace NEXA.Adapters.Output;

/// <summary>
/// Concrete Windows OS implementation of <see cref="IAudioSink"/> utilizing native Windows Core Audio COM Interop APIs.
/// <para>
/// <b>What it is:</b> The platform-specific master volume controller for Windows 10/11 desktops.
/// </para>
/// <para>
/// <b>What it does:</b> Accesses the default audio playback endpoint via <c>MMDeviceEnumerator</c> and adjusts volume via <c>IAudioEndpointVolume</c>.
/// </para>
/// <para>
/// <b>Why it is used:</b> Provides zero-dependency, low-latency, stepless master volume control without third-party NuGet packages.
/// </para>
/// </summary>
public class Win32AudioSink : IAudioSink
{
    private IAudioEndpointVolume? _endpointVolume = null;
    private IAudioEndpointVolume? _micEndpointVolume = null;
    private float _cachedVolume = 0.5f;
    private bool _cachedMute = false;
    private bool _cachedMicMute = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="Win32AudioSink"/> class and activates the default audio endpoint volume interface.
    /// </summary>
    public Win32AudioSink()
    {
        TryInitializeAudioEndpoint();
    }

    private void TryInitializeAudioEndpoint()
    {
        try
        {
            MMDeviceEnumeratorComObject enumeratorObj = new();
            IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)enumeratorObj;

            // 1. Output Speakers: eRender = 0, eMultimedia = 1
            enumerator.GetDefaultAudioEndpoint(0, 1, out IMMDevice device);
            if (device != null)
            {
                Guid IID_IAudioEndpointVolume = typeof(IAudioEndpointVolume).GUID;
                device.Activate(ref IID_IAudioEndpointVolume, 23, IntPtr.Zero, out object volumeObj);
                _endpointVolume = volumeObj as IAudioEndpointVolume;

                if (_endpointVolume != null)
                {
                    _endpointVolume.GetMasterVolumeLevelScalar(out _cachedVolume);
                    _endpointVolume.GetMute(out _cachedMute);
                }
            }

            // 2. Input Microphone: eCapture = 1, eMultimedia = 1
            enumerator.GetDefaultAudioEndpoint(1, 1, out IMMDevice micDevice);
            if (micDevice != null)
            {
                Guid IID_IAudioEndpointVolume = typeof(IAudioEndpointVolume).GUID;
                micDevice.Activate(ref IID_IAudioEndpointVolume, 23, IntPtr.Zero, out object micVolumeObj);
                _micEndpointVolume = micVolumeObj as IAudioEndpointVolume;

                if (_micEndpointVolume != null)
                {
                    _micEndpointVolume.GetMute(out _cachedMicMute);
                }
            }
        }
        catch
        {
            // Graceful fallback in environments without physical audio hardware (e.g. CI/virtual test runners)
            _endpointVolume = null;
            _micEndpointVolume = null;
        }
    }

    /// <inheritdoc/>
    public float GetMasterVolume()
    {
        if (_endpointVolume != null)
        {
            try
            {
                _endpointVolume.GetMasterVolumeLevelScalar(out float level);
                _cachedVolume = level;
                return level;
            }
            catch
            {
                TryInitializeAudioEndpoint();
            }
        }
        return _cachedVolume;
    }

    /// <inheritdoc/>
    public void SetMasterVolume(float volumeLevel)
    {
        float clamped = Math.Clamp(volumeLevel, 0.0f, 1.0f);
        _cachedVolume = clamped;

        if (_endpointVolume != null)
        {
            try
            {
                Guid emptyContext = Guid.Empty;
                _endpointVolume.SetMasterVolumeLevelScalar(clamped, ref emptyContext);
            }
            catch
            {
                TryInitializeAudioEndpoint();
            }
        }
    }

    /// <inheritdoc/>
    public void SetMute(bool isMuted)
    {
        _cachedMute = isMuted;
        if (_endpointVolume != null)
        {
            try
            {
                Guid emptyContext = Guid.Empty;
                _endpointVolume.SetMute(isMuted, ref emptyContext);
            }
            catch
            {
                TryInitializeAudioEndpoint();
            }
        }
    }

    /// <inheritdoc/>
    public bool IsMuted()
    {
        if (_endpointVolume != null)
        {
            try
            {
                _endpointVolume.GetMute(out bool isMuted);
                _cachedMute = isMuted;
                return isMuted;
            }
            catch
            {
                TryInitializeAudioEndpoint();
            }
        }
        return _cachedMute;
    }

    /// <inheritdoc/>
    public void ToggleMute()
    {
        bool current = IsMuted();
        SetMute(!current);
    }

    /// <inheritdoc/>
    public void SetMicrophoneMute(bool isMuted)
    {
        _cachedMicMute = isMuted;
        if (_micEndpointVolume != null)
        {
            try
            {
                Guid emptyContext = Guid.Empty;
                _micEndpointVolume.SetMute(isMuted, ref emptyContext);
            }
            catch
            {
                TryInitializeAudioEndpoint();
            }
        }
    }

    /// <inheritdoc/>
    public bool IsMicrophoneMuted()
    {
        if (_micEndpointVolume != null)
        {
            try
            {
                _micEndpointVolume.GetMute(out bool isMuted);
                _cachedMicMute = isMuted;
                return isMuted;
            }
            catch
            {
                TryInitializeAudioEndpoint();
            }
        }
        return _cachedMicMute;
    }

    /// <inheritdoc/>
    public void ToggleMicrophoneMute()
    {
        bool current = IsMicrophoneMuted();
        SetMicrophoneMute(!current);
    }

    #region Windows Core Audio COM Interfaces

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject
    {
    }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid id, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
    }

    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr notify);
        int UnregisterControlChangeNotify(IntPtr notify);
        int GetChannelCount(out int channelCount);
        int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);
        int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
        int GetMasterVolumeLevel(out float levelDb);
        int GetMasterVolumeLevelScalar(out float level);
        int SetChannelVolumeLevel(int channelNumber, float levelDb, ref Guid eventContext);
        int SetChannelVolumeLevelScalar(int channelNumber, float level, ref Guid eventContext);
        int GetChannelVolumeLevel(int channelNumber, out float levelDb);
        int GetChannelVolumeLevelScalar(int channelNumber, out float level);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool isMuted, ref Guid eventContext);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool isMuted);
        int GetVolumeStepInfo(out int step, out int stepCount);
        int VolumeStepUp(ref Guid eventContext);
        int VolumeStepDown(ref Guid eventContext);
        int QueryHardwareSupport(out int hardwareSupportMask);
        int GetVolumeRange(out float volumeMinDb, out float volumeMaxDb, out float volumeIncrementDb);
    }

    #endregion
}
