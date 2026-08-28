using System;
using NEXA.Abstractions;

namespace NEXA.Domain.Common;

/// <summary>
/// Thread-safe default implementation of <see cref="IGestureArbitrator"/> managing exclusive gesture ownership.
/// </summary>
public class GestureArbitrator : IGestureArbitrator
{
    private readonly object _lock = new();
    private string? _activeGesture;

    /// <inheritdoc/>
    public string? ActiveGesture
    {
        get
        {
            lock (_lock)
            {
                return _activeGesture;
            }
        }
    }

    /// <inheritdoc/>
    public bool CanExecute(string gestureName)
    {
        if (string.IsNullOrEmpty(gestureName))
        {
            return false;
        }

        lock (_lock)
        {
            return _activeGesture == null || string.Equals(_activeGesture, gestureName, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <inheritdoc/>
    public bool TryAcquire(string gestureName, bool highPriority = false)
    {
        if (string.IsNullOrEmpty(gestureName))
        {
            return false;
        }

        lock (_lock)
        {
            if (_activeGesture == null || string.Equals(_activeGesture, gestureName, StringComparison.OrdinalIgnoreCase))
            {
                _activeGesture = gestureName;
                return true;
            }

            // High priority (e.g. Mouse Pointing) can take over non-modal background gesture locks
            if (highPriority && !IsModalGesture(_activeGesture))
            {
                _activeGesture = gestureName;
                return true;
            }

            return false;
        }
    }

    private static bool IsModalGesture(string gesture) =>
        string.Equals(gesture, "WindowGrab", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(gesture, "TwoHand", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public void Release(string gestureName)
    {
        if (string.IsNullOrEmpty(gestureName))
        {
            return;
        }

        lock (_lock)
        {
            if (string.Equals(_activeGesture, gestureName, StringComparison.OrdinalIgnoreCase))
            {
                _activeGesture = null;
            }
        }
    }

    /// <inheritdoc/>
    public void Reset()
    {
        lock (_lock)
        {
            _activeGesture = null;
        }
    }
}
