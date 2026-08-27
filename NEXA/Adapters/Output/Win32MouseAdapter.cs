using System;
using System.Runtime.InteropServices;
using NEXA.Adapters.Output.Native;

namespace NEXA.Adapters.Output;

/// <summary>
/// Specialized adapter responsible for hardware mouse cursor positioning, discrete clicking, and wheel scrolling.
/// <para>
/// <b>What it is:</b> Dedicated mouse input injector using Win32 <c>SetCursorPos</c> and <c>SendInput</c>.
/// </para>
/// </summary>
public class Win32MouseAdapter
{
    /// <summary>
    /// Sets the physical desktop mouse cursor position.
    /// </summary>
    /// <param name="x">Horizontal monitor pixel coordinate.</param>
    /// <param name="y">Vertical monitor pixel coordinate.</param>
    public void MoveCursor(int x, int y)
    {
        Win32NativeInterop.SetCursorPos(x, y);
    }

    /// <summary>
    /// Simulates a discrete left mouse button down followed immediately by left mouse button up.
    /// </summary>
    public void Click()
    {
        Win32NativeInterop.INPUT[] inputs = new Win32NativeInterop.INPUT[]
        {
            new() { type = Win32NativeInterop.INPUT_MOUSE, mi = new Win32NativeInterop.MOUSEINPUT { dwFlags = Win32NativeInterop.MOUSEEVENTF_LEFTDOWN } },
            new() { type = Win32NativeInterop.INPUT_MOUSE, mi = new Win32NativeInterop.MOUSEINPUT { dwFlags = Win32NativeInterop.MOUSEEVENTF_LEFTUP } }
        };
        Win32NativeInterop.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Win32NativeInterop.INPUT)));
    }

    /// <summary>
    /// Injects a mouse wheel scroll event into the operating system.
    /// </summary>
    /// <param name="wheelDelta">Signed integer magnitude of scroll rotation (standard notch is 120; positive = UP, negative = DOWN).</param>
    public void Scroll(int wheelDelta)
    {
        Win32NativeInterop.INPUT[] inputs = new Win32NativeInterop.INPUT[]
        {
            new()
            {
                type = Win32NativeInterop.INPUT_MOUSE,
                mi = new Win32NativeInterop.MOUSEINPUT
                {
                    dwFlags = Win32NativeInterop.MOUSEEVENTF_WHEEL,
                    mouseData = unchecked((uint)wheelDelta)
                }
            }
        };
        Win32NativeInterop.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Win32NativeInterop.INPUT)));
    }
}
