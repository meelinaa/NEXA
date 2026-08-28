namespace NEXA.Abstractions;

/// <summary>
/// Abstraction contract for polling keyboard events and keystrokes from the GUI event loop.
/// <para>
/// <b>What it is:</b> Interface decoupling OpenCV Cv2.WaitKey polling from the execution engine for unit testability.
/// </para>
/// </summary>
public interface IKeyboardEventSource
{
    /// <summary>
    /// Waits for a keystroke event within the specified timeout delay.
    /// </summary>
    /// <param name="delayMs">Delay in milliseconds to wait for a keypress.</param>
    /// <returns>The ASCII keycode if a key was pressed; otherwise, -1.</returns>
    int WaitKey(int delayMs);
}
