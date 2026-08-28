namespace NEXA.Abstractions;

/// <summary>
/// Abstraction sink for system master audio output volume adjustments, speaker muting, and microphone hardware input muting.
/// <para>
/// <b>What it is:</b> Interface decoupling audio endpoint management from Windows CoreAudio WASAPI COM interop.
/// </para>
/// </summary>
public interface IAudioSink
{
    /// <summary>
    /// Gets the current system master volume scalar level.
    /// </summary>
    /// <returns>Normalized float between 0.0 (silent) and 1.0 (100% maximum volume).</returns>
    float GetMasterVolume();

    /// <summary>
    /// Sets the system master volume scalar level.
    /// </summary>
    /// <param name="volumeLevel">Normalized float between 0.0 (silent) and 1.0 (100% maximum volume).</param>
    void SetMasterVolume(float volumeLevel);

    /// <summary>
    /// Sets the master audio mute state.
    /// </summary>
    /// <param name="isMuted"><c>true</c> to mute audio output; otherwise, <c>false</c>.</param>
    void SetMute(bool isMuted);

    /// <summary>
    /// Queries whether the master audio output is currently muted.
    /// </summary>
    /// <returns><c>true</c> if muted; otherwise, <c>false</c>.</returns>
    bool IsMuted();

    /// <summary>
    /// Toggles the system master audio mute state.
    /// </summary>
    void ToggleMute();

    /// <summary>
    /// Sets the master microphone input mute state.
    /// </summary>
    /// <param name="isMuted"><c>true</c> to mute microphone input; otherwise, <c>false</c>.</param>
    void SetMicrophoneMute(bool isMuted);

    /// <summary>
    /// Queries whether the master microphone input is currently muted.
    /// </summary>
    /// <returns><c>true</c> if microphone is muted; otherwise, <c>false</c>.</returns>
    bool IsMicrophoneMuted();

    /// <summary>
    /// Toggles the master microphone input mute state.
    /// </summary>
    void ToggleMicrophoneMute();
}
