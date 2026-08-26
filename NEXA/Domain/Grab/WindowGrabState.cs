using System;
using System.Diagnostics;
using OpenCvSharp;

namespace NEXA.Domain.Grab;

/// <summary>
/// State model for real OS window grabbing, hold tracking, delta dragging, and edge snap docking.
/// <para>
/// <b>What it is:</b> The state machine memory model for <see cref="WindowGrabDetector"/>.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description>Tracks continuous fist hold duration toward the 2.0s engagement threshold.</description></item>
/// <item><description>Holds the captured window handle (HWND), cached window title, and initial desktop bounds.</description></item>
/// <item><description>Maintains initial and current hand screen coordinates for delta translation calculations.</description></item>
/// <item><description>Tracks edge docking state (Snap Left, Snap Right, Snap Top), lock timer (300ms), and pre-snap restoration geometry.</description></item>
/// <item><description>Provides a 120ms time-based release debounce to prevent accidental dropouts during camera tracking flutter.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Why it is used:</b> Encapsulates window manipulation state cleanly in the Domain layer.
/// </para>
/// <para>
/// <b>Consequence:</b> Provides stable, jitter-free multi-frame window dragging and docking.
/// </para>
/// </summary>
public class WindowGrabState
{
    /// <summary>
    /// Gets or sets a value indicating whether a desktop window is actively grabbed and following hand motion.
    /// </summary>
    public bool IsGrabbed { get; set; } = false;

    /// <summary>
    /// Duration in seconds that the fist gesture has been continuously maintained.
    /// </summary>
    public double HoldDurationSeconds { get; set; } = 0.0;

    /// <summary>
    /// Required continuous hold duration in seconds (2.0s) before grab engagement activates.
    /// </summary>
    public double RequiredHoldSeconds { get; set; } = 2.0;

    /// <summary>
    /// The native window handle (HWND) of the currently grabbed window.
    /// </summary>
    public IntPtr TargetHwnd { get; set; } = IntPtr.Zero;

    /// <summary>
    /// The cached title text of the grabbed window, queried strictly once on grab start.
    /// </summary>
    public string CachedWindowTitle { get; set; } = string.Empty;

    /// <summary>
    /// The initial desktop rectangle (X, Y, Width, Height) of the window when the grab engaged.
    /// </summary>
    public Rect InitialWindowBounds { get; set; }

    /// <summary>
    /// The initial horizontal desktop screen coordinate of the hand when the grab engaged.
    /// </summary>
    public int InitialHandScreenX { get; set; }

    /// <summary>
    /// The initial vertical desktop screen coordinate of the hand when the grab engaged.
    /// </summary>
    public int InitialHandScreenY { get; set; }

    /// <summary>
    /// The calculated current target desktop X coordinate for the window.
    /// </summary>
    public int CurrentTargetX { get; set; }

    /// <summary>
    /// The calculated current target desktop Y coordinate for the window.
    /// </summary>
    public int CurrentTargetY { get; set; }

    /// <summary>
    /// The current edge snap docking alignment of the grabbed window.
    /// </summary>
    public WindowSnapType ActiveSnap { get; set; } = WindowSnapType.None;

    /// <summary>
    /// Indicates whether the window is currently docked to an edge.
    /// </summary>
    public bool IsSnapped => ActiveSnap != WindowSnapType.None;

    /// <summary>
    /// The original window geometry preserved prior to the first snap action for un-dock restoration.
    /// </summary>
    public Rect PreSnapBounds { get; set; }

    /// <summary>
    /// The computed desktop boundary rectangle applied during the active snap state.
    /// </summary>
    public Rect SnapBounds { get; set; }

    /// <summary>
    /// Most recent 2D palm center position recorded during tracking.
    /// </summary>
    public Point2f LastPalmCenter { get; set; }

    /// <summary>
    /// High-resolution stopwatch measuring continuous fist hold time.
    /// </summary>
    public Stopwatch HoldTimer { get; } = new();

    /// <summary>
    /// High-resolution stopwatch providing time-based release debounce tolerance.
    /// </summary>
    public Stopwatch ReleaseTimer { get; } = new();

    /// <summary>
    /// High-resolution stopwatch enforcing a 300ms latch lock immediately upon edge snap docking.
    /// </summary>
    public Stopwatch SnapLockTimer { get; } = new();

    /// <summary>
    /// Minimum duration (300ms) that a window remains firmly locked in snap dock before un-dock pull away is evaluated.
    /// </summary>
    public static readonly TimeSpan SnapLockDuration = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Time-based tolerance (120ms) before confirming gesture release.
    /// </summary>
    public static readonly TimeSpan ReleaseTolerance = TimeSpan.FromMilliseconds(120);
}
