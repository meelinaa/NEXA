using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace NEXA.Adapters.Output;

/// <summary>
/// Facade implementing <see cref="IInputSink"/> by delegating input injection, window manipulation, keyboard commands, and display topology to specialized adapters.
/// <para>
/// <b>What it is:</b> Unified entry point for native Windows OS integration adhering to the Facade and Composite patterns.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description>Delegates cursor movement, clicks, and scrolling to <see cref="Win32MouseAdapter"/>.</description></item>
/// <item><description>Delegates shortcuts, media controls, and security locking to <see cref="Win32KeyboardAdapter"/>.</description></item>
/// <item><description>Delegates window spatial positioning, resizing, and hit-testing to <see cref="Win32WindowAdapter"/>.</description></item>
/// <item><description>Delegates display enumeration and multi-monitor translocation to <see cref="Win32MonitorAdapter"/>.</description></item>
/// <item><description>Maintains centralized <see cref="LastFocusedHwnd"/> and <see cref="LastFocusedTitle"/> focus tracking state.</description></item>
/// </list>
/// </para>
/// </summary>
public class Win32InputSink : IInputSink
{
    private readonly Win32MouseAdapter _mouse;
    private readonly Win32KeyboardAdapter _keyboard;
    private readonly Win32WindowAdapter _window;
    private readonly Win32MonitorAdapter _monitor;

    /// <inheritdoc/>
    public IntPtr LastFocusedHwnd { get; set; } = IntPtr.Zero;

    /// <inheritdoc/>
    public string LastFocusedTitle { get; set; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="Win32InputSink"/> facade class.
    /// </summary>
    /// <param name="mouse">Optional custom mouse adapter.</param>
    /// <param name="keyboard">Optional custom keyboard adapter.</param>
    /// <param name="window">Optional custom window adapter.</param>
    /// <param name="monitor">Optional custom monitor adapter.</param>
    public Win32InputSink(
        Win32MouseAdapter? mouse = null,
        Win32KeyboardAdapter? keyboard = null,
        Win32WindowAdapter? window = null,
        Win32MonitorAdapter? monitor = null)
    {
        _mouse = mouse ?? new Win32MouseAdapter();
        _keyboard = keyboard ?? new Win32KeyboardAdapter();
        _window = window ?? new Win32WindowAdapter();
        _monitor = monitor ?? new Win32MonitorAdapter();
    }

    /// <inheritdoc/>
    public void MoveCursor(int x, int y)
    {
        _mouse.MoveCursor(x, y);
    }

    /// <inheritdoc/>
    public void Click()
    {
        _mouse.Click();
    }

    /// <inheritdoc/>
    public void Scroll(int wheelDelta)
    {
        _mouse.Scroll(wheelDelta);
    }

    /// <inheritdoc/>
    public void SendMediaPlayPause()
    {
        _keyboard.SendMediaPlayPause();
    }

    /// <inheritdoc/>
    public void LockWorkstation()
    {
        _keyboard.LockWorkstation();
    }

    /// <inheritdoc/>
    public void SendUndo()
    {
        _keyboard.SendUndo();
    }

    /// <inheritdoc/>
    public void SendRedo()
    {
        _keyboard.SendRedo();
    }

    /// <inheritdoc/>
    public void MoveWindow(IntPtr hwnd, int x, int y)
    {
        _window.MoveWindow(hwnd, x, y);
    }

    /// <inheritdoc/>
    public void ResizeWindow(IntPtr hwnd, int width, int height)
    {
        _window.ResizeWindow(hwnd, width, height);
    }

    /// <inheritdoc/>
    public void SetWindowRect(IntPtr hwnd, int x, int y, int width, int height)
    {
        _window.SetWindowRect(hwnd, x, y, width, height);
    }

    /// <inheritdoc/>
    public void BringWindowToForeground(IntPtr hwnd)
    {
        _window.BringWindowToForeground(hwnd);
    }

    /// <inheritdoc/>
    public void MaximizeWindow(IntPtr hwnd)
    {
        _window.MaximizeWindow(hwnd);
    }

    /// <inheritdoc/>
    public void MinimizeWindow(IntPtr hwnd)
    {
        _window.MinimizeWindow(hwnd);
    }

    /// <inheritdoc/>
    public void RestoreWindow(IntPtr hwnd)
    {
        _window.RestoreWindow(hwnd);
    }

    /// <inheritdoc/>
    public (int width, int height) GetScreenResolution()
    {
        return _monitor.GetScreenResolution();
    }

    /// <inheritdoc/>
    public List<Rect> GetAllMonitorBounds()
    {
        return _monitor.GetAllMonitorBounds();
    }

    /// <inheritdoc/>
    public bool MoveWindowToAdjacentMonitor(IntPtr hwnd, bool toRight)
    {
        return _monitor.MoveWindowToAdjacentMonitor(hwnd, toRight, _window);
    }

    /// <inheritdoc/>
    public IntPtr GetWindowAt(int screenX, int screenY)
    {
        return _window.GetWindowAt(screenX, screenY);
    }

    /// <inheritdoc/>
    public bool GetWindowBounds(IntPtr hwnd, out int x, out int y, out int width, out int height, out string title)
    {
        return _window.GetWindowBounds(hwnd, out x, out y, out width, out height, out title);
    }
}
