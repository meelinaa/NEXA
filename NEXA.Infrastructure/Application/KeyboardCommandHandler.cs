using System;
using NEXA.Face;
using NEXA.Hand;

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
    /// <param name="controllers">Aggregate bundle of all gesture-domain controllers.</param>
    /// <param name="showHud">Reference boolean for HUD visibility.</param>
    /// <returns><c>false</c> if an exit key was pressed; otherwise, <c>true</c> to continue streaming.</returns>
    public bool ProcessKey(
        int key,
        int frameWidth,
        int frameHeight,
        HandTracker tracker,
        HandMeshRenderer handRenderer,
        FaceMeshRenderer faceRenderer,
        NexaControllerBundle controllers,
        ref bool showHud)
    {
        if (key is 27 or 'q' or 'Q') // ESC or Q
        {
            return false;
        }

        if (key is 'c' or 'C')
        {
            controllers.Mouse.Enabled = !controllers.Mouse.Enabled;
        }
        else if (key is 'w' or 'W')
        {
            controllers.Scroll.Enabled = !controllers.Scroll.Enabled;
        }
        else if (key is 'g' or 'G')
        {
            controllers.WindowGrab.Enabled = !controllers.WindowGrab.Enabled;
        }
        else if (key is 't' or 'T')
        {
            controllers.TwoHand.Enabled = !controllers.TwoHand.Enabled;
        }
        else if (key is 'm' or 'M')
        {
            controllers.MonitorThrow.Enabled = !controllers.MonitorThrow.Enabled;
        }
        else if (key is 'v' or 'V')
        {
            controllers.Volume.Enabled = !controllers.Volume.Enabled;
        }
        else if (key is 'l' or 'L')
        {
            controllers.Lock.Enabled = !controllers.Lock.Enabled;
        }
        else if (key is 'u' or 'U')
        {
            controllers.CircleUndo.Enabled = !controllers.CircleUndo.Enabled;
        }
        else if (key is 'x' or 'X')
        {
            controllers.ShhhMute.Enabled = !controllers.ShhhMute.Enabled;
        }
        else if (key is 'e' or 'E')
        {
            controllers.HearNoEvil.Enabled = !controllers.HearNoEvil.Enabled;
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
            controllers.VirtualObject.Reset(frameWidth, frameHeight);
        }
        else if (key is 'h' or 'H')
        {
            showHud = !showHud;
        }

        return true;
    }
}
