namespace NEXA.Abstractions;

/// <summary>
/// Domain processing contract for controllers that evaluate real-time frame telemetry and render augmented reality visual feedback.
/// <para>
/// <b>What it is:</b> An abstraction decoupling <see cref="NEXA.Application.NexaEngine"/> from specific gesture domain controllers,
/// enabling the Open/Closed Principle for extensible frame processing pipelines.
/// </para>
/// </summary>
public interface IFrameProcessor
{
    /// <summary>
    /// Evaluates vision detections from the frame context, updates the internal gesture state machine, and triggers operating system commands.
    /// </summary>
    /// <param name="context">The frame context containing the current camera frame, tracked hands, and face telemetry.</param>
    void Process(FrameContext context);

    /// <summary>
    /// Draws augmented reality overlays, holographic visual widgets, and HUD feedback onto the camera frame.
    /// </summary>
    /// <param name="context">The frame context containing the current camera frame, tracked hands, and face telemetry.</param>
    void Render(FrameContext context);
}
