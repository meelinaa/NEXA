using System;
using System.Runtime.InteropServices;
using System.Text;

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
/// <item><description>Relocates OS application windows smoothly via <c>SetWindowPos</c> and brings them to the foreground via <c>SetForegroundWindow</c>.</description></item>
/// <item><description>Queries top-level window handles at arbitrary coordinates and inspects window titles and bounds.</description></item>
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

    private const int SM_CXSCREEN = 0; // System metric index for primary monitor width
    private const int SM_CYSCREEN = 1; // System metric index for primary monitor height

    private const uint GA_ROOT = 2; // Retrieves the root window by walking the chain of parent windows

    private const uint INPUT_MOUSE = 0;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;

    private const uint SWP_NOSIZE = 0x0001;
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
    public void MoveCursor(int x, int y)
    {
        SetCursorPos(x, y);
    }

    /// <inheritdoc/>
    public void Click()
    {
        var inputs = new INPUT[]
        {
            new() { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTDOWN } },
            new() { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTUP } }
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    /// <inheritdoc/>
    public void Scroll(int wheelDelta)
    {
        var inputs = new INPUT[]
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
        if (hwnd == IntPtr.Zero) return;
        SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    /// <inheritdoc/>
    public void BringWindowToForeground(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        BringWindowToTop(hwnd);
        SetForegroundWindow(hwnd);
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
    public IntPtr GetWindowAt(int screenX, int screenY)
    {
        var pt = new POINT { x = screenX, y = screenY };
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

        var className = new StringBuilder(256);
        GetClassName(hwnd, className, className.Capacity);
        string cls = className.ToString();

        // Skip Windows Shell and Desktop worker surfaces
        if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd")
        {
            return IntPtr.Zero;
        }

        var titleBuilder = new StringBuilder(256);
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

        var sb = new StringBuilder(512);
        GetWindowText(hwnd, sb, sb.Capacity);
        title = sb.ToString();

        return true;
    }
}
