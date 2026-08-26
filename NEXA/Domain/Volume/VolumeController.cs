using System;
using NEXA.Adapters.Output;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Domain.Volume;

/// <summary>
/// Application adapter orchestrating continuous system master volume adjustments and holographic AR rotary dial rendering.
/// <para>
/// <b>What it is:</b> The controller managing audio level manipulation via hand tilt.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="bullet">
/// <item><description>Evaluates hand orientation via <see cref="VolumeDetector"/>.</description></item>
/// <item><description>Updates system master volume via <see cref="IAudioSink.SetMasterVolume"/>.</description></item>
/// <item><description>Renders a glowing AR circular dial arc, rotary pointer, and volume level badge around the hand.</description></item>
/// </list>
/// </para>
/// </summary>
public class VolumeController
{
    /// <summary>
    /// The audio adapter used to query and adjust Windows system volume.
    /// </summary>
    private readonly IAudioSink _audioSink;

    /// <summary>
    /// The core domain detector evaluating L-gesture posture and angular tilt.
    /// </summary>
    public VolumeDetector Detector { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether volume control is enabled.
    /// </summary>
    public bool Enabled
    {
        get => Detector.Enabled;
        set => Detector.Enabled = value;
    }

    /// <summary>
    /// Gets the internal volume state machine.
    /// </summary>
    public VolumeState State => Detector.State;

    /// <summary>
    /// Initializes a new instance of the <see cref="VolumeController"/> class.
    /// </summary>
    /// <param name="audioSink">The audio adapter for OS master volume (defaults to <see cref="Win32AudioSink"/> if null).</param>
    public VolumeController(IAudioSink? audioSink = null)
    {
        _audioSink = audioSink ?? new Win32AudioSink();
    }

    /// <summary>
    /// Evaluates tracked hand orientation and applies continuous volume adjustments to the OS.
    /// </summary>
    /// <param name="hand">The tracked hand instance.</param>
    public void Update(TrackedHand? hand)
    {
        (bool isActive, float targetVolume) = Detector.Update(hand, _audioSink);
        if (isActive)
        {
            _audioSink.SetMasterVolume(targetVolume);
        }
    }

    /// <summary>
    /// Renders augmented-reality rotary gauge visuals and live volume percentages onto the camera frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    public void RenderFeedback(Mat frame)
    {
        if (!State.IsActive)
            return;

        Point center = new((int)Math.Round(State.DialCenter.X), (int)Math.Round(State.DialCenter.Y));
        int radius = 46;

        // 1. Background Arc Track (Dark Slate)
        Cv2.Circle(frame, center, radius, new Scalar(40, 45, 60), 4, LineTypes.AntiAlias);

        // 2. Active Volume Level Arc (Cyan / Lime Green)
        float volPercent = Math.Clamp(State.SmoothedVolume, 0.0f, 1.0f);
        int sweepAngle = (int)(volPercent * 360);

        Scalar volColor = volPercent > 0.70f
            ? new Scalar(0, 220, 255) // Gold / Orange for high volume
            : new Scalar(0, 255, 120); // Neon Green for safe volume

        Cv2.Ellipse(frame, center, new Size(radius, radius), -90, 0, sweepAngle, volColor, 5, LineTypes.AntiAlias);

        // 3. Rotary Pointer Line toward Index Tip
        Point indexPt = new((int)Math.Round(State.IndexTip.X), (int)Math.Round(State.IndexTip.Y));
        Cv2.Line(frame, center, indexPt, new Scalar(255, 255, 255), 2, LineTypes.AntiAlias);
        Cv2.Circle(frame, indexPt, 5, volColor, -1, LineTypes.AntiAlias);

        // 4. Floating HUD Tag: Volume Percentage & Tilt Delta
        int volInt = (int)Math.Round(volPercent * 100);
        string sign = State.AngleDelta >= 0 ? "+" : "";
        string tagText = $"VOL: {volInt}% ({sign}{State.AngleDelta:F0} deg)";

        Point tagPos = new(center.X + radius + 10, center.Y + 6);
        Cv2.PutText(frame, tagText, tagPos, HersheyFonts.HersheySimplex, 0.46, volColor, 1, LineTypes.AntiAlias);
    }
}
