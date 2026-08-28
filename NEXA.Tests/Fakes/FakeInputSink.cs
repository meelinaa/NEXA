using System;
using System.Collections.Generic;
using NEXA.Abstractions;
using OpenCvSharp;

namespace NEXA.Tests.Fakes;

/// <summary>
/// In-memory test double for <see cref="IInputSink"/> simulating OS input injection, window management, and monitor queries without Win32 P/Invoke dependencies.
/// </summary>
public class FakeInputSink : IInputSink
{
    public int CursorX { get; private set; } = 0;
    public int CursorY { get; private set; } = 0;
    public int ClickCount { get; private set; } = 0;
    public int TotalScrollDelta { get; private set; } = 0;
    public bool IsWorkstationLocked { get; private set; } = false;
    public int MediaPlayPauseCount { get; private set; } = 0;
    public int UndoCount { get; private set; } = 0;
    public int RedoCount { get; private set; } = 0;

    public IntPtr LastFocusedHwnd { get; set; } = (IntPtr)0x1000;
    public string LastFocusedTitle { get; set; } = "Test Window";

    public int ScreenWidth { get; set; } = 1920;
    public int ScreenHeight { get; set; } = 1080;

    public List<Rect> MonitorBounds { get; set; } = new()
    {
        new Rect(0, 0, 1920, 1080),
        new Rect(1920, 0, 1920, 1080)
    };

    public Dictionary<IntPtr, (Rect Bounds, string Title)> Windows { get; } = new()
    {
        [(IntPtr)0x1000] = (new Rect(100, 100, 800, 600), "Test Window")
    };

    public void MoveCursor(int x, int y)
    {
        CursorX = x;
        CursorY = y;
    }

    public void Click()
    {
        ClickCount++;
    }

    public void Scroll(int wheelDelta)
    {
        TotalScrollDelta += wheelDelta;
    }

    public void LockWorkstation()
    {
        IsWorkstationLocked = true;
    }

    public void SendMediaPlayPause()
    {
        MediaPlayPauseCount++;
    }

    public void SendUndo()
    {
        UndoCount++;
    }

    public void SendRedo()
    {
        RedoCount++;
    }

    public void MoveWindow(IntPtr hwnd, int x, int y)
    {
        if (Windows.TryGetValue(hwnd, out var win))
        {
            Windows[hwnd] = (new Rect(x, y, win.Bounds.Width, win.Bounds.Height), win.Title);
        }
        else
        {
            Windows[hwnd] = (new Rect(x, y, 800, 600), "Dynamic Window");
        }
    }

    public void ResizeWindow(IntPtr hwnd, int width, int height)
    {
        if (Windows.TryGetValue(hwnd, out var win))
        {
            Windows[hwnd] = (new Rect(win.Bounds.X, win.Bounds.Y, width, height), win.Title);
        }
        else
        {
            Windows[hwnd] = (new Rect(0, 0, width, height), "Dynamic Window");
        }
    }

    public void SetWindowRect(IntPtr hwnd, int x, int y, int width, int height)
    {
        string title = Windows.TryGetValue(hwnd, out var win) ? win.Title : "Dynamic Window";
        Windows[hwnd] = (new Rect(x, y, width, height), title);
    }

    public void BringWindowToForeground(IntPtr hwnd)
    {
        LastFocusedHwnd = hwnd;
        if (Windows.TryGetValue(hwnd, out var win))
        {
            LastFocusedTitle = win.Title;
        }
    }

    public void MaximizeWindow(IntPtr hwnd)
    {
        SetWindowRect(hwnd, 0, 0, ScreenWidth, ScreenHeight);
    }

    public void MinimizeWindow(IntPtr hwnd)
    {
        SetWindowRect(hwnd, -32000, -32000, 0, 0);
    }

    public void RestoreWindow(IntPtr hwnd)
    {
        SetWindowRect(hwnd, 100, 100, 800, 600);
    }

    public IntPtr GetWindowAt(int screenX, int screenY)
    {
        return LastFocusedHwnd;
    }

    public bool GetWindowBounds(IntPtr hwnd, out int x, out int y, out int width, out int height, out string title)
    {
        if (Windows.TryGetValue(hwnd, out var win))
        {
            x = win.Bounds.X;
            y = win.Bounds.Y;
            width = win.Bounds.Width;
            height = win.Bounds.Height;
            title = win.Title;
            return true;
        }

        x = 100;
        y = 100;
        width = 800;
        height = 600;
        title = "Mock Window";
        return true;
    }

    public (int width, int height) GetScreenResolution()
    {
        return (ScreenWidth, ScreenHeight);
    }

    public List<Rect> GetAllMonitorBounds()
    {
        return new List<Rect>(MonitorBounds);
    }

    public bool MoveWindowToAdjacentMonitor(IntPtr hwnd, bool moveRight)
    {
        return true;
    }
}
