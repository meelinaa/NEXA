using OpenCvSharp;
using System.Diagnostics;

namespace NEXA.Object;

/// <summary>
/// State model for gesture-based object grabbing, holding, and spatial dragging.
/// <para>
/// <b>What it is:</b> The state machine tracking clench-fist hold durations and relative spatial offset locks.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description>Measures continuous clenched-fist duration using a dedicated stopwatch.</description></item>
/// <item><description>Activates grab state only after holding a steady fist for the required threshold (<see cref="RequiredHoldTime"/>).</description></item>
/// <item><description>Locks the relative offset between the palm center and target object to support natural relative dragging.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Why it is used:</b> Prevents transient momentary fist gestures from accidentally snatching or dislocating objects.
/// </para>
/// <para>
/// <b>Consequence:</b> Gives the user intentional control over when an object is grabbed and where it is moved.
/// </para>
/// </summary>
public class GrabState
{
    /// <summary>
    /// Gets or sets a value indicating whether the virtual object is actively locked and being dragged by the hand.
    /// </summary>
    public bool Active { get; internal set; } = false;

    /// <summary>
    /// Current duration in seconds that the fist gesture has been continuously maintained.
    /// </summary>
    public double HoldDurationSeconds { get; internal set; } = 0.0;

    /// <summary>
    /// Required continuous hold duration in seconds (2.0s) before grab engagement activates.
    /// </summary>
    public double RequiredHoldTime { get; internal set; } = 2.0;

    /// <summary>
    /// The fixed spatial offset vector (PalmX - ObjectX, PalmY - ObjectY) recorded at the moment of grab activation.
    /// </summary>
    public (double X, double Y) HandOffsetToObject { get; internal set; }

    /// <summary>
    /// Most recent 2D palm center position recorded during the gesture.
    /// </summary>
    public Point2f LastPalmCenter { get; internal set; }

    /// <summary>
    /// High-precision stopwatch measuring continuous fist duration.
    /// </summary>
    public readonly Stopwatch FistTimer = new();
}
