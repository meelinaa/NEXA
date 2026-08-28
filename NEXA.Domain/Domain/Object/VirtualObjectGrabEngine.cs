using System;
using OpenCvSharp;

namespace NEXA.Object;

/// <summary>
/// Domain engine managing fist hold timing, relative offset locking, and dragging translation for 2D virtual test objects.
/// <para>
/// <b>What it is:</b> Spatial grab and drag calculator for virtual target objects.
/// </para>
/// </summary>
public class VirtualObjectGrabEngine
{
    /// <summary>
    /// Gets the internal grab state machine.
    /// </summary>
    public GrabState State { get; } = new();

    /// <summary>
    /// Evaluates fist posture and updates the position of the target virtual object.
    /// </summary>
    /// <param name="target">The virtual target object being translated.</param>
    /// <param name="palmCenter">Current geometric palm center in camera coordinates.</param>
    /// <param name="gesture">The classified gesture string.</param>
    /// <param name="frameWidth">Camera frame width in pixels.</param>
    /// <param name="frameHeight">Camera frame height in pixels.</param>
    public void UpdateGrab(
        TestObject target,
        Point2f palmCenter,
        string gesture,
        int frameWidth,
        int frameHeight)
    {
        bool isFist = gesture == "Fist";
        State.LastPalmCenter = palmCenter;

        if (isFist)
        {
            if (!State.FistTimer.IsRunning)
            {
                State.FistTimer.Restart();
            }

            State.HoldDurationSeconds = State.FistTimer.Elapsed.TotalSeconds;

            // Activate grab only after holding continuous fist for the required hold time (2.0s)
            if (!State.Active && State.HoldDurationSeconds >= State.RequiredHoldTime)
            {
                State.Active = true;
                // Lock relative offset so object does not snap abruptly to palm center
                State.HandOffsetToObject = (palmCenter.X - target.X, palmCenter.Y - target.Y);
            }
        }
        else
        {
            if (State.FistTimer.IsRunning)
            {
                State.FistTimer.Reset();
            }

            State.HoldDurationSeconds = 0;

            if (State.Active)
            {
                State.Active = false;
            }
        }

        // Translate object if actively grabbed
        if (State.Active)
        {
            double newX = palmCenter.X - State.HandOffsetToObject.X;
            double newY = palmCenter.Y - State.HandOffsetToObject.Y;

            int margin = 50;
            target.X = Math.Clamp(newX, margin, frameWidth - margin);
            target.Y = Math.Clamp(newY, margin, frameHeight - margin);
        }
    }

    /// <summary>
    /// Resets transient timers and active grab state when hand tracking is lost.
    /// </summary>
    public void HandleNoHand()
    {
        if (State.FistTimer.IsRunning)
        {
            State.FistTimer.Reset();
        }
        State.HoldDurationSeconds = 0;
        State.Active = false;
    }

    /// <summary>
    /// Resets the grab state machine back to defaults.
    /// </summary>
    public void Reset()
    {
        State.Active = false;
        State.FistTimer.Reset();
        State.HoldDurationSeconds = 0;
    }
}
