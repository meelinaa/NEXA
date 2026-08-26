using System;

namespace NEXA.Adapters.Output;

/// <summary>
/// Output port abstraction interface for operating system input injection and window manipulation.
/// <para>
/// <b>What it is:</b> A decoupled contract defining all physical hardware and OS commands that NEXA can issue.
/// </para>
/// <para>
/// <b>What it does:</b> Provides unified methods for scrolling, cursor positioning, left-clicking, window relocation, and querying monitor metrics.
/// </para>
/// <para>
/// <b>Why it is used:</b> Shields domain logic and high-level controllers from platform-specific Win32 P/Invoke APIs, enabling mockability and automated unit testing.
/// </para>
/// <para>
/// <b>Consequence:</b> Any operating system adapter (Windows, Linux, macOS, or automated test mock) can be seamlessly plugged in.
/// </para>
/// </summary>
public interface IInputSink
{
    /// <summary>
    /// Injects a mouse wheel scroll event into the operating system.
    /// </summary>
    /// <param name="wheelDelta">Signed integer magnitude of scroll rotation (standard notch is 120; positive = UP, negative = DOWN).</param>
    void Scroll(int wheelDelta);

    /// <summary>
    /// Sets the physical desktop mouse cursor position.
    /// </summary>
    /// <param name="x">Horizontal monitor pixel coordinate.</param>
    /// <param name="y">Vertical monitor pixel coordinate.</param>
    void MoveCursor(int x, int y);

    /// <summary>
    /// Simulates a discrete left mouse button down followed immediately by left mouse button up.
    /// </summary>
    void Click();

    /// <summary>
    /// Relocates a native OS window to the specified desktop coordinates without altering its size or Z-order.
    /// </summary>
    /// <param name="hwnd">Native Win32 window handle pointer (HWND).</param>
    /// <param name="x">Target horizontal desktop coordinate.</param>
    /// <param name="y">Target vertical desktop coordinate.</param>
    void MoveWindow(IntPtr hwnd, int x, int y);

    /// <summary>
    /// Queries the primary display resolution of the active desktop.
    /// </summary>
    /// <returns>A tuple containing (width, height) in physical pixels.</returns>
    (int width, int height) GetScreenResolution();
}
