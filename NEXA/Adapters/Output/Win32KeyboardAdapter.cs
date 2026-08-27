using System;
using NEXA.Adapters.Output.Native;

namespace NEXA.Adapters.Output;

/// <summary>
/// Specialized adapter responsible for Windows shortcut key synthesis, media playback control, and workstation security lock.
/// <para>
/// <b>What it is:</b> Dedicated keyboard event generator using Win32 <c>keybd_event</c> and <c>LockWorkStation</c>.
/// </para>
/// </summary>
public class Win32KeyboardAdapter
{
    /// <summary>
    /// Injects a global Windows hardware media key event (VK_MEDIA_PLAY_PAUSE = 0xB3) to toggle audio/video playback.
    /// </summary>
    public void SendMediaPlayPause()
    {
        Win32NativeInterop.keybd_event(0xB3, 0, 0x0001, UIntPtr.Zero); // VK_MEDIA_PLAY_PAUSE down
        Win32NativeInterop.keybd_event(0xB3, 0, 0x0001 | 0x0002, UIntPtr.Zero); // VK_MEDIA_PLAY_PAUSE up
    }

    /// <summary>
    /// Locks the Windows desktop workstation immediately (equivalent to Win + L).
    /// </summary>
    public void LockWorkstation()
    {
        Win32NativeInterop.LockWorkStation();
    }

    /// <summary>
    /// Injects an Undo shortcut into the active application (Ctrl + Z).
    /// </summary>
    public void SendUndo()
    {
        Win32NativeInterop.keybd_event(0x11, 0, 0, UIntPtr.Zero); // VK_CONTROL down
        Win32NativeInterop.keybd_event(0x5A, 0, 0, UIntPtr.Zero); // 'Z' down
        Win32NativeInterop.keybd_event(0x5A, 0, 0x0002, UIntPtr.Zero); // 'Z' up
        Win32NativeInterop.keybd_event(0x11, 0, 0x0002, UIntPtr.Zero); // VK_CONTROL up
    }

    /// <summary>
    /// Injects a Redo shortcut into the active application (Ctrl + Y).
    /// </summary>
    public void SendRedo()
    {
        Win32NativeInterop.keybd_event(0x11, 0, 0, UIntPtr.Zero); // VK_CONTROL down
        Win32NativeInterop.keybd_event(0x59, 0, 0, UIntPtr.Zero); // 'Y' down
        Win32NativeInterop.keybd_event(0x59, 0, 0x0002, UIntPtr.Zero); // 'Y' up
        Win32NativeInterop.keybd_event(0x11, 0, 0x0002, UIntPtr.Zero); // VK_CONTROL up
    }
}
