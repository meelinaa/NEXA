using System.Collections.Generic;
using NEXA.Face;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Abstractions;

/// <summary>
/// Context object encapsulating current frame telemetry, computer vision inference results, and viewport dimensions.
/// <para>
/// <b>What it is:</b> An immutable parameter aggregate passed to <see cref="IFrameProcessor"/> pipeline components.
/// </para>
/// </summary>
public class FrameContext
{
    /// <summary>
    /// Gets the current camera frame (BGR Mat).
    /// </summary>
    public Mat Frame { get; }

    /// <summary>
    /// Gets the active list of tracked hands detected in the current frame.
    /// </summary>
    public List<TrackedHand> TrackedHands { get; }

    /// <summary>
    /// Gets the primary tracked hand (first detected hand), or <c>null</c> if no hands are visible.
    /// </summary>
    public TrackedHand? PrimaryHand { get; }

    /// <summary>
    /// Gets the primary tracked face detected in the current frame, or <c>null</c> if no face is detected.
    /// </summary>
    public TrackedFace? PrimaryFace { get; }

    /// <summary>
    /// Gets the horizontal width of the camera frame in pixels.
    /// </summary>
    public int FrameWidth => Frame.Width;

    /// <summary>
    /// Gets the vertical height of the camera frame in pixels.
    /// </summary>
    public int FrameHeight => Frame.Height;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameContext"/> class.
    /// </summary>
    public FrameContext(Mat frame, List<TrackedHand> trackedHands, TrackedFace? primaryFace)
    {
        Frame = frame;
        TrackedHands = trackedHands;
        PrimaryHand = trackedHands.Count > 0 ? trackedHands[0] : null;
        PrimaryFace = primaryFace;
    }
}
