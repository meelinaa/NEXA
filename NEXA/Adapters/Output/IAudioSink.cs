namespace NEXA.Adapters.Output;

/// <summary>
/// Output port abstraction interface for querying and controlling Windows system master audio volume and mute states.
/// <para>
/// <b>What it is:</b> A decoupled contract defining operating system master audio hardware control.
/// </para>
/// <para>
/// <b>What it does:</b> Provides unified methods for reading and adjusting master audio scalar levels [0.0 to 1.0].
/// </para>
/// <para>
/// <b>Why it is used:</b> Isolates native Windows Core Audio COM Interop from domain-level rotary angle calculations, enabling clean unit testing and mockability.
/// </para>
/// </summary>
public interface IAudioSink
{
    /// <summary>
    /// Retrieves the current system master volume scalar level.
    /// </summary>
    /// <returns>A normalized float between 0.0 (silent) and 1.0 (100% maximum volume).</returns>
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
