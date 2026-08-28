using System;
using System.Collections.Generic;
using NEXA.Abstractions;

namespace NEXA.Tests.Fakes;

/// <summary>
/// In-memory test double for <see cref="IKeyboardEventSource"/> injecting deterministic keyboard sequences.
/// </summary>
public class FakeKeyboardEventSource : IKeyboardEventSource
{
    private readonly Queue<int> _keyQueue;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeKeyboardEventSource"/> class with a predefined key sequence.
    /// </summary>
    /// <param name="keySequence">Array of keys to return sequentially upon calls to <see cref="WaitKey(int)"/>.</param>
    public FakeKeyboardEventSource(IEnumerable<int> keySequence)
    {
        _keyQueue = new Queue<int>(keySequence ?? Array.Empty<int>());
    }

    /// <summary>
    /// Enqueues an additional key event into the sequence.
    /// </summary>
    /// <param name="key">The keycode to enqueue.</param>
    public void EnqueueKey(int key)
    {
        _keyQueue.Enqueue(key);
    }

    /// <inheritdoc/>
    public int WaitKey(int delay = 1)
    {
        if (_keyQueue.TryDequeue(out int key))
        {
            return key;
        }

        return -1; // No key pressed
    }
}
