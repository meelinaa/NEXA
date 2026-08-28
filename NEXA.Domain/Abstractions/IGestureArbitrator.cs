namespace NEXA.Abstractions;

/// <summary>
/// Domain contract enforcing global gesture mutual exclusion across all frame processors.
/// Ensures that when one gesture interaction is active (e.g. window grab/resize, volume dial, screenshot framing),
/// all competing gestures are suppressed.
/// </summary>
public interface IGestureArbitrator
{
    /// <summary>
    /// Gets the unique identifier of the currently active gesture owner, or <c>null</c> if idle.
    /// </summary>
    string? ActiveGesture { get; }

    /// <summary>
    /// Checks whether the specified gesture can proceed with evaluation or execution.
    /// Returns <c>true</c> if no other gesture is active, or if the caller already owns the active lock.
    /// </summary>
    /// <param name="gestureName">The unique identifier of the gesture.</param>
    /// <returns><c>true</c> if allowed; otherwise, <c>false</c>.</returns>
    bool CanExecute(string gestureName);

    /// <summary>
    /// Attempts to acquire or maintain exclusive execution rights for the specified gesture.
    /// </summary>
    /// <param name="gestureName">The unique identifier of the gesture.</param>
    /// <param name="highPriority">If true, allows overriding non-modal locks (e.g. mouse pointing taking top priority).</param>
    /// <returns><c>true</c> if successfully acquired or maintained; otherwise, <c>false</c>.</returns>
    bool TryAcquire(string gestureName, bool highPriority = false);

    /// <summary>
    /// Releases the active gesture lock if it is currently owned by the specified gesture.
    /// </summary>
    /// <param name="gestureName">The unique identifier of the gesture.</param>
    void Release(string gestureName);

    /// <summary>
    /// Forcefully resets the active gesture lock to idle.
    /// </summary>
    void Reset();
}
