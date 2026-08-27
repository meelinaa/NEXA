using System;
using NEXA.Domain.Click;
using NEXA.Domain.EarsMute;
using NEXA.Domain.Grab;
using NEXA.Domain.Lock;
using NEXA.Domain.MonitorThrow;
using NEXA.Domain.Mute;
using NEXA.Domain.Scroll;
using NEXA.Domain.TwoHand;
using NEXA.Domain.Undo;
using NEXA.Domain.Volume;
using NEXA.Face;
using NEXA.Hand;
using NEXA.Object;

namespace NEXA.Application;

/// <summary>
/// Handles keyboard input hotkeys to toggle pipeline features, reset virtual test objects, and switch visual HUD telemetry layers.
/// <para>
/// <b>What it is:</b> Application command interpreter mapping keystrokes to domain controller properties.
/// </para>
/// </summary>
public class KeyboardCommandHandler
{
    /// <summary>
    /// Prints the console summary of all interactive keyboard hotkeys.
    /// </summary>
    public void PrintControls()
    {
        Console.WriteLine("\nControls:");
        Console.WriteLine("  [ESC] / [Q] : Exit application");
        Console.WriteLine("  [C]         : Toggle Mouse Navigation & Dwell-Click");
        Console.WriteLine("  [W]         : Toggle Swipe Scrolling");
        Console.WriteLine("  [G]         : Toggle Real Window Grabbing & Pinch-Resizing");
        Console.WriteLine("  [T]         : Toggle 2-Hand Gestures (Maximize/Minimize/Screenshot/PlayPause)");
        Console.WriteLine("  [M]         : Toggle Multi-Monitor Throw (Blade Swipe)");
        Console.WriteLine("  [V]         : Toggle Volume Control (L-Gesture Rotary Dial)");
        Console.WriteLine("  [L]         : Toggle PC Lock Gesture (Open-Fist-Open-Fist)");
        Console.WriteLine("  [U]         : Toggle Undo/Redo Gesture (Peace Wrist-Twist)");
        Console.WriteLine("  [X]         : Toggle Shhh Mute Gesture (Finger to Mouth)");
        Console.WriteLine("  [E]         : Toggle Hear No Evil Gesture (Hands to Ears Sound Mute)");
        Console.WriteLine("  [S]         : Toggle OneEuroFilter Smoothing");
        Console.WriteLine("  [J]         : Toggle Skeleton Joint Nodes");
        Console.WriteLine("  [B]         : Toggle Bounding Box & HUD Tag");
        Console.WriteLine("  [R]         : Reset Virtual Object (Pos & Zoom)");
        Console.WriteLine("  [H]         : Toggle Telemetry HUD Overlay");
        Console.WriteLine("  [F]         : Toggle Face Tracking\n");
    }

    /// <summary>
    /// Processes a single OpenCV ASCII keycode and applies state modifications.
    /// </summary>
    /// <param name="key">The keycode returned from Cv2.WaitKey().</param>
    /// <param name="frameWidth">Current frame width for object reset.</param>
    /// <param name="frameHeight">Current frame height for object reset.</param>
    /// <param name="tracker">Hand tracker instance.</param>
    /// <param name="handRenderer">Hand skeleton visualizer.</param>
    /// <param name="faceRenderer">Face mesh visualizer.</param>
    /// <param name="virtualObject">Virtual test object.</param>
    /// <param name="mouseController">Mouse controller.</param>
    /// <param name="scrollController">Scroll controller.</param>
    /// <param name="windowGrabController">Window grab controller.</param>
    /// <param name="twoHandController">Two-hand controller.</param>
    /// <param name="monitorThrowController">Monitor throw controller.</param>
    /// <param name="volumeController">Volume controller.</param>
    /// <param name="lockController">Lock controller.</param>
    /// <param name="circleUndoController">Circle undo controller.</param>
    /// <param name="shhhMuteController">Shhh mute controller.</param>
    /// <param name="hearNoEvilController">Hear-no-evil controller.</param>
    /// <param name="showHud">Reference boolean for HUD visibility.</param>
    /// <returns><c>false</c> if an exit key was pressed; otherwise, <c>true</c> to continue streaming.</returns>
    public bool ProcessKey(
        int key,
        int frameWidth,
        int frameHeight,
        HandTracker tracker,
        HandMeshRenderer handRenderer,
        FaceMeshRenderer faceRenderer,
        VirtualObjectController virtualObject,
        MouseController mouseController,
        ScrollController scrollController,
        WindowGrabController windowGrabController,
        TwoHandGestureController twoHandController,
        MonitorThrowController monitorThrowController,
        VolumeController volumeController,
        LockSequenceController lockController,
        CircleUndoController circleUndoController,
        ShhhMuteController shhhMuteController,
        HearNoEvilController hearNoEvilController,
        ref bool showHud)
    {
        if (key is 27 or 'q' or 'Q') // ESC or Q
        {
            return false;
        }

        if (key is 'c' or 'C')
        {
            mouseController.Enabled = !mouseController.Enabled;
        }
        else if (key is 'w' or 'W')
        {
            scrollController.Enabled = !scrollController.Enabled;
        }
        else if (key is 'g' or 'G')
        {
            windowGrabController.Enabled = !windowGrabController.Enabled;
        }
        else if (key is 't' or 'T')
        {
            twoHandController.Enabled = !twoHandController.Enabled;
        }
        else if (key is 'm' or 'M')
        {
            monitorThrowController.Enabled = !monitorThrowController.Enabled;
        }
        else if (key is 'v' or 'V')
        {
            volumeController.Enabled = !volumeController.Enabled;
        }
        else if (key is 'l' or 'L')
        {
            lockController.Enabled = !lockController.Enabled;
        }
        else if (key is 'u' or 'U')
        {
            circleUndoController.Enabled = !circleUndoController.Enabled;
        }
        else if (key is 'x' or 'X')
        {
            shhhMuteController.Enabled = !shhhMuteController.Enabled;
        }
        else if (key is 'e' or 'E')
        {
            hearNoEvilController.Enabled = !hearNoEvilController.Enabled;
        }
        else if (key is 'f' or 'F')
        {
            faceRenderer.ShowFaceOverlay = !faceRenderer.ShowFaceOverlay;
            faceRenderer.ShowMeshWidget = !faceRenderer.ShowMeshWidget;
            faceRenderer.ShowHeadBoundingBox = !faceRenderer.ShowHeadBoundingBox;
        }
        else if (key is 's' or 'S')
        {
            tracker.SmoothingEnabled = !tracker.SmoothingEnabled;
        }
        else if (key is 'j' or 'J')
        {
            handRenderer.ShowJoints = !handRenderer.ShowJoints;
        }
        else if (key is 'b' or 'B')
        {
            handRenderer.ShowBoundingBox = !handRenderer.ShowBoundingBox;
        }
        else if (key is 'r' or 'R')
        {
            virtualObject.Reset(frameWidth, frameHeight);
        }
        else if (key is 'h' or 'H')
        {
            showHud = !showHud;
        }

        return true;
    }
}
