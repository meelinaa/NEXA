using System;
using System.Text;
using NEXA.Adapters.Output.Native;
using NEXA.Common;

namespace NEXA.Adapters.Output;

/// <summary>
/// Specialized adapter responsible for Windows desktop application window spatial relocation, resizing, state transitions, and hit-testing.
/// <para>
/// <b>What it is:</b> Dedicated window manager executing Win32 <c>SetWindowPos</c>, <c>ShowWindow</c>, <c>WindowFromPoint</c>, and metrics extraction.
/// </para>
/// </summary>
public class Win32WindowAdapter
{
    /// <summary>
    /// Relocates a native OS window to the specified desktop coordinates without altering its size or Z-order.
    /// </summary>
    /// <param name="hwnd">Native Win32 window handle pointer (HWND).</param>
    /// <param name="x">Target horizontal desktop coordinate.</param>
    /// <param name="y">Target vertical desktop coordinate.</param>
    public void MoveWindow(IntPtr hwnd, int x, int y)
    {
        if (hwnd == IntPtr.Zero)
            return;
        Win32NativeInterop.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0,
            Win32NativeInterop.SWP_NOSIZE | Win32NativeInterop.SWP_NOZORDER | Win32NativeInterop.SWP_NOACTIVATE);
    }

    /// <summary>
    /// Resizes a native OS window to the specified dimensions without altering its desktop position or Z-order.
    /// </summary>
    /// <param name="hwnd">Native Win32 window handle pointer (HWND).</param>
    /// <param name="width">Target window width in pixels.</param>
    /// <param name="height">Target window height in pixels.</param>
    public void ResizeWindow(IntPtr hwnd, int width, int height)
    {
        if (hwnd == IntPtr.Zero)
            return;
        Win32NativeInterop.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, width, height,
            Win32NativeInterop.SWP_NOMOVE | Win32NativeInterop.SWP_NOZORDER | Win32NativeInterop.SWP_NOACTIVATE);
    }

    /// <summary>
    /// Atomically moves and resizes a native OS window in a single system call without altering its Z-order.
    /// </summary>
    /// <param name="hwnd">Native Win32 window handle pointer (HWND).</param>
    /// <param name="x">Target horizontal desktop coordinate.</param>
    /// <param name="y">Target vertical desktop coordinate.</param>
    /// <param name="width">Target window width in pixels.</param>
    /// <param name="height">Target window height in pixels.</param>
    public void SetWindowRect(IntPtr hwnd, int x, int y, int width, int height)
    {
        if (hwnd == IntPtr.Zero)
            return;
        Win32NativeInterop.SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height,
            Win32NativeInterop.SWP_NOZORDER | Win32NativeInterop.SWP_NOACTIVATE);
    }

    /// <summary>
    /// Brings the specified native OS window to the top of the Z-order and sets it as the foreground window.
    /// </summary>
    /// <param name="hwnd">Native Win32 window handle pointer (HWND).</param>
    public void BringWindowToForeground(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;
        Win32NativeInterop.BringWindowToTop(hwnd);
        Win32NativeInterop.SetForegroundWindow(hwnd);
    }

    /// <summary>
    /// Maximizes the specified native OS application window to fill the monitor display.
    /// </summary>
    /// <param name="hwnd">Native Win32 window handle pointer (HWND).</param>
    public void MaximizeWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;
        Win32NativeInterop.ShowWindow(hwnd, Win32NativeInterop.SW_MAXIMIZE);
    }

    /// <summary>
    /// Minimizes the specified native OS application window to the taskbar.
    /// </summary>
    /// <param name="hwnd">Native Win32 window handle pointer (HWND).</param>
    public void MinimizeWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;
        Win32NativeInterop.ShowWindow(hwnd, Win32NativeInterop.SW_MINIMIZE);
    }

    /// <summary>
    /// Restores the specified native OS application window to its normal un-maximized dimensions.
    /// </summary>
    /// <param name="hwnd">Native Win32 window handle pointer (HWND).</param>
    public void RestoreWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;
        Win32NativeInterop.ShowWindow(hwnd, Win32NativeInterop.SW_RESTORE);
    }

    /// <summary>
    /// Identifies the top-level application window located at the given desktop screen coordinate.
    /// </summary>
    /// <param name="screenX">Horizontal screen pixel coordinate.</param>
    /// <param name="screenY">Vertical screen pixel coordinate.</param>
    /// <returns>The top-level window handle (HWND), or <see cref="IntPtr.Zero"/> if none is found.</returns>
    public IntPtr GetWindowAt(int screenX, int screenY)
    {
        Win32NativeInterop.POINT pt = new() { x = screenX, y = screenY };
        IntPtr hwnd = Win32NativeInterop.WindowFromPoint(pt);
        if (hwnd == IntPtr.Zero)
            return IntPtr.Zero;

        // Resolve child control to top-level root window
        IntPtr rootHwnd = Win32NativeInterop.GetAncestor(hwnd, Win32NativeInterop.GA_ROOT);
        if (rootHwnd != IntPtr.Zero)
        {
            hwnd = rootHwnd;
        }

        // Filter system desktop and shell windows
        if (hwnd == Win32NativeInterop.GetDesktopWindow() || hwnd == Win32NativeInterop.GetShellWindow())
        {
            return IntPtr.Zero;
        }

        if (!Win32NativeInterop.IsWindowVisible(hwnd))
        {
            return IntPtr.Zero;
        }

        StringBuilder className = new(256);
        Win32NativeInterop.GetClassName(hwnd, className, className.Capacity);
        string cls = className.ToString();

        // Skip Windows Shell and Desktop worker surfaces
        if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
        {
            return IntPtr.Zero;
        }

        StringBuilder titleBuilder = new(256);
        int titleLen = Win32NativeInterop.GetWindowText(hwnd, titleBuilder, titleBuilder.Capacity);
        if (titleLen == 0)
        {
            return IntPtr.Zero; // Ignore background utility windows with empty titles
        }

        return hwnd;
    }

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
    public bool GetWindowBounds(IntPtr hwnd, out int x, out int y, out int width, out int height, out string title)
    {
        x = y = width = height = 0;
        title = string.Empty;

        if (hwnd == IntPtr.Zero)
            return false;

        if (!Win32NativeInterop.GetWindowRect(hwnd, out Win32NativeInterop.RECT rect))
        {
            return false;
        }

        x = rect.Left;
        y = rect.Top;
        width = Math.Max(1, rect.Right - rect.Left);
        height = Math.Max(1, rect.Bottom - rect.Top);

        StringBuilder sb = new(512);
        Win32NativeInterop.GetWindowText(hwnd, sb, sb.Capacity);
        title = TextSanitizer.ToSafeAscii(sb.ToString());

        return true;
    }
}
