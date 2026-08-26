using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using OpenCvSharp;

namespace NEXA.Adapters.Output;

/// <summary>
/// Concrete Windows OS implementation of <see cref="IInputSink"/> utilizing native Win32 user32.dll P/Invoke APIs.
/// <para>
/// <b>What it is:</b> The platform-specific hardware input injection adapter for Windows desktops.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description>Translates virtual cursor positions into Windows system cursor moves via <c>SetCursorPos</c>.</description></item>
/// <item><description>Constructs and dispatches synthesized mouse clicks and mouse wheel rotation events via <c>SendInput</c>.</description></item>
/// <item><description>Relocates OS application windows via <c>SetWindowPos</c> and changes window states via <c>ShowWindow</c> (Maximize, Minimize, Restore).</description></item>
/// <item><description>Transfers application windows across physical and virtual monitors via <c>EnumDisplayMonitors</c> and <c>GetMonitorInfo</c>.</description></item>
/// <item><description>Maintains a centralized <see cref="LastFocusedHwnd"/> and <see cref="LastFocusedTitle"/> context across all features.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Why it is used:</b> Consolidates all low-level native interop declarations and memory marshalling structures into one isolated class.
/// </para>
/// <para>
/// <b>Consequence:</b> Any changes to OS input mechanisms or security requirements are isolated here without touching gesture recognition code.
/// </para>
/// </summary>
public class Win32InputSink : IInputSink
{
    #region Win32 API Imports & Structs

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private const int SM_CXSCREEN = 0; // System metric index for primary monitor width
    private const int SM_CYSCREEN = 1; // System metric index for primary monitor height

    private const uint GA_ROOT = 2; // Retrieves the root window by walking the chain of parent windows

    private const int SW_MAXIMIZE = 3; // Maximizes the specified window
    private const int SW_MINIMIZE = 6; // Minimizes the specified window
    private const int SW_RESTORE = 9;  // Restores the window to its original size and position

    private const uint INPUT_MOUSE = 0;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    #endregion

    /// <inheritdoc/>
    public IntPtr LastFocusedHwnd { get; set; } = IntPtr.Zero;

    /// <inheritdoc/>
    public string LastFocusedTitle { get; set; } = string.Empty;

    /// <inheritdoc/>
    public void MoveCursor(int x, int y)
    {
        SetCursorPos(x, y);
    }

    /// <inheritdoc/>
    public void Click()
    {
        INPUT[] inputs = new INPUT[]
        {
            new() { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTDOWN } },
            new() { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTUP } }
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    /// <inheritdoc/>
    public void SendMediaPlayPause()
    {
        keybd_event(0xB3, 0, 0x0001, UIntPtr.Zero); // VK_MEDIA_PLAY_PAUSE down
        keybd_event(0xB3, 0, 0x0001 | 0x0002, UIntPtr.Zero); // VK_MEDIA_PLAY_PAUSE up
    }

    /// <inheritdoc/>
    public void Scroll(int wheelDelta)
    {
        INPUT[] inputs = new INPUT[]
        {
            new()
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    dwFlags = MOUSEEVENTF_WHEEL,
                    mouseData = unchecked((uint)wheelDelta)
                }
            }
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    /// <inheritdoc/>
    public void MoveWindow(IntPtr hwnd, int x, int y)
    {
        if (hwnd == IntPtr.Zero) 
            return;
        SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    /// <inheritdoc/>
    public void ResizeWindow(IntPtr hwnd, int width, int height)
    {
        if (hwnd == IntPtr.Zero)
            return;
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, width, height, SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    /// <inheritdoc/>
    public void SetWindowRect(IntPtr hwnd, int x, int y, int width, int height)
    {
        if (hwnd == IntPtr.Zero)
            return;
        SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
    }

    /// <inheritdoc/>
    public void BringWindowToForeground(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;
        BringWindowToTop(hwnd);
        SetForegroundWindow(hwnd);
    }

    /// <inheritdoc/>
    public void MaximizeWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) 
            return;
        ShowWindow(hwnd, SW_MAXIMIZE);
    }

    /// <inheritdoc/>
    public void MinimizeWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) 
            return;
        ShowWindow(hwnd, SW_MINIMIZE);
    }

    /// <inheritdoc/>
    public void RestoreWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;
        ShowWindow(hwnd, SW_RESTORE);
    }

    /// <inheritdoc/>
    public (int width, int height) GetScreenResolution()
    {
        int width = GetSystemMetrics(SM_CXSCREEN);
        int height = GetSystemMetrics(SM_CYSCREEN);
        if (width <= 0) width = 1920;
        if (height <= 0) height = 1080;
        return (width, height);
    }

    /// <inheritdoc/>
    public List<Rect> GetAllMonitorBounds()
    {
        List<Rect> monitors = new();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref RECT rc, IntPtr data) =>
        {
            MONITORINFO mi = new();
            mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            if (GetMonitorInfo(hMon, ref mi))
            {
                int w = Math.Max(1, mi.rcMonitor.Right - mi.rcMonitor.Left);
                int h = Math.Max(1, mi.rcMonitor.Bottom - mi.rcMonitor.Top);
                monitors.Add(new Rect(mi.rcMonitor.Left, mi.rcMonitor.Top, w, h));
            }
            return true;
        }, IntPtr.Zero);

        if (monitors.Count == 0)
        {
            (int sw, int sh) = GetScreenResolution();
            monitors.Add(new Rect(0, 0, sw, sh));
        }

        // Sort monitors spatially from left to right along horizontal coordinate
        return monitors.OrderBy(m => m.X).ToList();
    }

    /// <inheritdoc/>
    public bool MoveWindowToAdjacentMonitor(IntPtr hwnd, bool toRight)
    {
        if (hwnd == IntPtr.Zero) return false;

        if (!GetWindowBounds(hwnd, out int winX, out int winY, out int winW, out int winH, out _))
        {
            return false;
        }

        List<Rect> monitors = GetAllMonitorBounds();
        if (monitors.Count <= 1)
        {
            return false; // Multi-monitor transfer requires at least 2 active displays
        }

        int centerX = winX + winW / 2;
        int centerY = winY + winH / 2;

        // Identify source monitor containing the window center
        int sourceIndex = -1;
        for (int i = 0; i < monitors.Count; i++)
        {
            Rect m = monitors[i];
            if (centerX >= m.Left && centerX < m.Right && centerY >= m.Top && centerY < m.Bottom)
            {
                sourceIndex = i;
                break;
            }
        }

        if (sourceIndex < 0)
        {
            // Fallback: pick closest monitor by distance to center
            double minDist = double.MaxValue;
            for (int i = 0; i < monitors.Count; i++)
            {
                Rect m = monitors[i];
                double mcx = m.X + m.Width / 2.0;
                double mcy = m.Y + m.Height / 2.0;
                double d = Math.Pow(centerX - mcx, 2) + Math.Pow(centerY - mcy, 2);
                if (d < minDist)
                {
                    minDist = d;
                    sourceIndex = i;
                }
            }
        }

        int targetIndex = toRight
            ? (sourceIndex + 1) % monitors.Count
            : (sourceIndex - 1 + monitors.Count) % monitors.Count;

        Rect src = monitors[sourceIndex];
        Rect dst = monitors[targetIndex];

        // Maintain relative proportional offsets within monitor workspace
        double rx = (double)(winX - src.X) / src.Width;
        double ry = (double)(winY - src.Y) / src.Height;
        double rw = (double)winW / src.Width;
        double rh = (double)winH / src.Height;

        int newW = Math.Clamp((int)Math.Round(rw * dst.Width), 320, dst.Width);
        int newH = Math.Clamp((int)Math.Round(rh * dst.Height), 240, dst.Height);
        int newX = dst.X + (int)Math.Round(rx * dst.Width);
        int newY = dst.Y + (int)Math.Round(ry * dst.Height);

        newX = Math.Clamp(newX, dst.Left, dst.Right - Math.Min(newW, 120));
        newY = Math.Clamp(newY, dst.Top, dst.Bottom - Math.Min(newH, 120));

        SetWindowRect(hwnd, newX, newY, newW, newH);
        BringWindowToForeground(hwnd);

        return true;
    }

    /// <inheritdoc/>
    public IntPtr GetWindowAt(int screenX, int screenY)
    {
        POINT pt = new() { x = screenX, y = screenY };
        IntPtr hwnd = WindowFromPoint(pt);
        if (hwnd == IntPtr.Zero) return IntPtr.Zero;

        // Resolve child control to top-level root window
        IntPtr rootHwnd = GetAncestor(hwnd, GA_ROOT);
        if (rootHwnd != IntPtr.Zero)
        {
            hwnd = rootHwnd;
        }

        // Filter system desktop and shell windows
        if (hwnd == GetDesktopWindow() || hwnd == GetShellWindow())
        {
            return IntPtr.Zero;
        }

        if (!IsWindowVisible(hwnd))
        {
            return IntPtr.Zero;
        }

        StringBuilder className = new(256);
        GetClassName(hwnd, className, className.Capacity);
        string cls = className.ToString();

        // Skip Windows Shell and Desktop worker surfaces
        if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd")
        {
            return IntPtr.Zero;
        }

        StringBuilder titleBuilder = new(256);
        int titleLen = GetWindowText(hwnd, titleBuilder, titleBuilder.Capacity);
        if (titleLen == 0)
        {
            return IntPtr.Zero; // Ignore background utility windows with empty titles
        }

        return hwnd;
    }

    /// <inheritdoc/>
    public bool GetWindowBounds(IntPtr hwnd, out int x, out int y, out int width, out int height, out string title)
    {
        x = y = width = height = 0;
        title = string.Empty;

        if (hwnd == IntPtr.Zero) return false;

        if (!GetWindowRect(hwnd, out RECT rect))
        {
            return false;
        }

        x = rect.Left;
        y = rect.Top;
        width = Math.Max(1, rect.Right - rect.Left);
        height = Math.Max(1, rect.Bottom - rect.Top);

        StringBuilder sb = new(512);
        GetWindowText(hwnd, sb, sb.Capacity);
        title = NEXA.Common.TextSanitizer.ToSafeAscii(sb.ToString());

        return true;
    }
}
