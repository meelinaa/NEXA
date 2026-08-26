using System.Runtime.InteropServices;

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
/// <item><description>Relocates OS application windows smoothly via <c>SetWindowPos</c>.</description></item>
/// <item><description>Reads primary display width and height via <c>GetSystemMetrics</c>.</description></item>
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

    private const int SM_CXSCREEN = 0; // System metric index for primary monitor width
    private const int SM_CYSCREEN = 1; // System metric index for primary monitor height

    private const uint INPUT_MOUSE = 0;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

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
        INPUT[] inputs =
        [
            new() { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTDOWN } },
            new() { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTUP } }
        ];
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    /// <inheritdoc/>
    public void Scroll(int wheelDelta)
    {
        INPUT[] inputs =
        [
            new()
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    dwFlags = MOUSEEVENTF_WHEEL,
                    mouseData = unchecked((uint)wheelDelta)
                }
            }
        ];
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
    public (int width, int height) GetScreenResolution()
    {
        int width = GetSystemMetrics(SM_CXSCREEN);
        int height = GetSystemMetrics(SM_CYSCREEN);
        if (width <= 0)
            width = 1920;
        if (height <= 0)
            height = 1080;
        return (width, height);
    }
}
