using NEXA.Abstractions;

namespace NEXA.Tests.Fakes;

/// <summary>
/// In-memory test double for <see cref="IAudioSink"/> simulating speaker and microphone volume and mute states without COM WASAPI interop.
/// </summary>
public class FakeAudioSink : IAudioSink
{
    private float _volume = 0.5f;
    private bool _isMuted = false;
    private bool _isMicMuted = false;

    public int VolumeSetCount { get; private set; } = 0;
    public int MuteToggleCount { get; private set; } = 0;
    public int MicMuteToggleCount { get; private set; } = 0;

    public float GetMasterVolume() => _volume;

    public void SetMasterVolume(float volumeLevel)
    {
        _volume = volumeLevel;
        VolumeSetCount++;
    }

    public void SetMute(bool isMuted)
    {
        _isMuted = isMuted;
    }

    public bool IsMuted() => _isMuted;

    public void ToggleMute()
    {
        _isMuted = !_isMuted;
        MuteToggleCount++;
    }

    public void SetMicrophoneMute(bool isMuted)
    {
        _isMicMuted = isMuted;
    }

    public bool IsMicrophoneMuted() => _isMicMuted;

    public void ToggleMicrophoneMute()
    {
        _isMicMuted = !_isMicMuted;
        MicMuteToggleCount++;
    }
}
