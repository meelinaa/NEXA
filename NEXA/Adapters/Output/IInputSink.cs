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
    /// Brings the specified native OS window to the top of the Z-order and sets it as the foreground window.
    /// </summary>
    /// <param name="hwnd">Native Win32 window handle pointer (HWND).</param>
    void BringWindowToForeground(IntPtr hwnd);

    /// <summary>
    /// Queries the primary display resolution of the active desktop.
    /// </summary>
    /// <returns>A tuple containing (width, height) in physical pixels.</returns>
    (int width, int height) GetScreenResolution();

    /// <summary>
    /// Identifies the top-level application window located at the given desktop screen coordinate.
    /// </summary>
    /// <param name="screenX">Horizontal screen pixel coordinate.</param>
    /// <param name="screenY">Vertical screen pixel coordinate.</param>
    /// <returns>The top-level window handle (HWND), or <see cref="IntPtr.Zero"/> if none is found or if it is a desktop/system window.</returns>
    IntPtr GetWindowAt(int screenX, int screenY);

    /// <summary>
    /// Queries the bounding rectangle and window title for a given window handle.
    /// </summary>
    /// <param name="hwnd">Window handle pointer (HWND).</param>
    /// <param name="x">Left pixel coordinate on the desktop.</param>
    /// <param name="y">Top pixel coordinate on the desktop.</param>
    /// <param name="width">Width of the window in pixels.</param>
    /// <param name="height">Height of the window in pixels.</param>
    /// <param name="title">Cached title text of the window.</param>
    /// <returns><c>true</c> if valid window metrics and title were retrieved; otherwise, <c>false</c>.</returns>
    bool GetWindowBounds(IntPtr hwnd, out int x, out int y, out int width, out int height, out string title);
}
