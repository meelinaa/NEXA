using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace NEXA.Abstractions;

/// <summary>
/// Output abstraction contract for injecting synthesized mouse, keyboard, window, and monitor actions into the operating system.
/// <para>
/// <b>What it is:</b> The unified abstraction decoupling NEXA domain interaction controllers from low-level OS platform API interop.
/// </para>
/// </summary>
public interface IInputSink
{
    /// <summary>
    /// Repositions the hardware mouse cursor to the specified absolute desktop pixel coordinates.
    /// </summary>
    void MoveCursor(int x, int y);

    /// <summary>
    /// Dispatches a synthetic left mouse button down and up event at the current pointer position.
    /// </summary>
    void Click();

    /// <summary>
    /// Simulates physical vertical mouse wheel scrolling.
    /// </summary>
    void Scroll(int wheelDelta);

    /// <summary>
    /// Immediately triggers the standard Windows Workstation Lock (equivalent to Win+L).
    /// </summary>
    void LockWorkstation();

    /// <summary>
    /// Simulates the standard Windows Media Play/Pause keyboard hotkey.
    /// </summary>
    void SendMediaPlayPause();

    /// <summary>
    /// Dispatches synthetic Undo keyboard shortcut (Ctrl+Z).
    /// </summary>
    void SendUndo();

    /// <summary>
    /// Dispatches synthetic Redo keyboard shortcut (Ctrl+Y).
    /// </summary>
    void SendRedo();

    /// <summary>
    /// Repositions a top-level native window to the specified desktop coordinates.
    /// </summary>
    void MoveWindow(IntPtr hwnd, int x, int y);

    /// <summary>
    /// Resizes a top-level native window to the specified width and height.
    /// </summary>
    void ResizeWindow(IntPtr hwnd, int width, int height);

    /// <summary>
    /// Sets both position and dimensions of a top-level native window in a single atomic OS operation.
    /// </summary>
    void SetWindowRect(IntPtr hwnd, int x, int y, int width, int height);

    /// <summary>
    /// Brings a top-level native window to the foreground and activates it.
    /// </summary>
    void BringWindowToForeground(IntPtr hwnd);

    /// <summary>
    /// Maximizes a top-level native window.
    /// </summary>
    void MaximizeWindow(IntPtr hwnd);

    /// <summary>
    /// Minimizes a top-level native window to the taskbar.
    /// </summary>
    void MinimizeWindow(IntPtr hwnd);

    /// <summary>
    /// Restores a maximized or minimized native window back to its normal floating state.
    /// </summary>
    void RestoreWindow(IntPtr hwnd);

    /// <summary>
    /// Retrieves the native window handle (HWND) located at the specified desktop pixel coordinates.
    /// </summary>
    IntPtr GetWindowAt(int screenX, int screenY);

    /// <summary>
    /// Retrieves the window bounds and window title for a given native window handle.
    /// </summary>
    bool GetWindowBounds(IntPtr hwnd, out int x, out int y, out int width, out int height, out string title);

    /// <summary>
    /// Gets or sets the cached handle of the most recently focused target window.
    /// </summary>
    IntPtr LastFocusedHwnd { get; set; }

    /// <summary>
    /// Gets or sets the cached title of the most recently focused target window.
    /// </summary>
    string LastFocusedTitle { get; set; }

    /// <summary>
    /// Gets the primary screen resolution (width, height) in pixels.
    /// </summary>
    (int width, int height) GetScreenResolution();

    /// <summary>
    /// Gets the bounding rectangles of all currently attached and enabled physical desktop displays.
    /// </summary>
    List<Rect> GetAllMonitorBounds();

    /// <summary>
    /// Relocates a window to an adjacent monitor to the right or left.
    /// </summary>
    bool MoveWindowToAdjacentMonitor(IntPtr hwnd, bool moveRight);
}
