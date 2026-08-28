using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using NEXA.Adapters.Output.Native;
using OpenCvSharp;

namespace NEXA.Adapters.Output;

/// <summary>
/// Specialized adapter responsible for multi-monitor spatial enumeration, display topology resolution, and cross-monitor window translocation.
/// <para>
/// <b>What it is:</b> Dedicated display adapter wrapping <c>EnumDisplayMonitors</c> and <c>GetMonitorInfo</c>.
/// </para>
/// </summary>
public class Win32MonitorAdapter
{
    /// <summary>
    /// Queries the primary display resolution of the active desktop.
    /// </summary>
    /// <returns>A tuple containing (width, height) in physical pixels.</returns>
    public (int width, int height) GetScreenResolution()
    {
        int width = Win32NativeInterop.GetSystemMetrics(Win32NativeInterop.SM_CXSCREEN);
        int height = Win32NativeInterop.GetSystemMetrics(Win32NativeInterop.SM_CYSCREEN);
        if (width <= 0) width = 1920;
        if (height <= 0) height = 1080;
        return (width, height);
    }

    /// <summary>
    /// Enumerates the desktop pixel bounds of all active physical and virtual display monitors.
    /// </summary>
    /// <returns>A list of bounding rectangles in virtual desktop screen space, sorted left-to-right.</returns>
    public List<Rect> GetAllMonitorBounds()
    {
        List<Rect> monitors = new();

        Win32NativeInterop.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref Win32NativeInterop.RECT rc, IntPtr data) =>
        {
            Win32NativeInterop.MONITORINFO mi = new();
            mi.cbSize = Marshal.SizeOf(typeof(Win32NativeInterop.MONITORINFO));
            if (Win32NativeInterop.GetMonitorInfo(hMon, ref mi))
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

    /// <summary>
    /// Translocates an application window to the next physically adjacent desktop monitor in the specified horizontal direction.
    /// </summary>
    /// <param name="hwnd">Native Win32 window handle pointer (HWND).</param>
    /// <param name="toRight"><c>true</c> to move to the monitor to the right; <c>false</c> to move to the left.</param>
    /// <param name="windowAdapter">Window adapter for reading bounds and updating position.</param>
    /// <returns><c>true</c> if successfully transferred to a different display; otherwise, <c>false</c>.</returns>
    public bool MoveWindowToAdjacentMonitor(IntPtr hwnd, bool toRight, Win32WindowAdapter windowAdapter)
    {
        if (hwnd == IntPtr.Zero || windowAdapter == null)
            return false;

        if (!windowAdapter.GetWindowBounds(hwnd, out int winX, out int winY, out int winW, out int winH, out _))
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

        windowAdapter.SetWindowRect(hwnd, newX, newY, newW, newH);
        windowAdapter.BringWindowToForeground(hwnd);

        return true;
    }
}
