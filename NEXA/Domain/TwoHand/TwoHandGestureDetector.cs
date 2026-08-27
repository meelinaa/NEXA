using System;
using System.Collections.Generic;
using NEXA.Abstractions;
using NEXA.Hand;

namespace NEXA.Domain.TwoHand;

/// <summary>
/// Domain-level composite coordinator delegating two-hand Maximize, Minimize, Camera-Frame Screenshot, and Clap / Prayer Play/Pause gesture detection.
/// <para>
/// <b>What it is:</b> A multi-hand spatial gesture analyzer coordinating window sizing, screen capture, and media playback interactions.
/// </para>
/// </summary>
public class TwoHandGestureDetector
{
    private readonly ClapPlayPauseDetector _clapDetector;
    private readonly CameraFrameScreenshotDetector _screenshotDetector;
    private readonly TwoHandMaximizeDetector _maximizeDetector;
    private readonly TwoHandMinimizeDetector _minimizeDetector;

    /// <summary>
    /// Gets the internal state machine tracking timing, touch anchors, velocity queues, and framing rectangles.
    /// </summary>
    public TwoHandGestureState State { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether two-hand gesture detection is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="TwoHandGestureDetector"/> class.
    /// </summary>
    /// <param name="clapDetector">Optional custom clap detector.</param>
    /// <param name="screenshotDetector">Optional custom screenshot detector.</param>
    /// <param name="maximizeDetector">Optional custom maximize detector.</param>
    /// <param name="minimizeDetector">Optional custom minimize detector.</param>
    public TwoHandGestureDetector(
        ClapPlayPauseDetector? clapDetector = null,
        CameraFrameScreenshotDetector? screenshotDetector = null,
        TwoHandMaximizeDetector? maximizeDetector = null,
        TwoHandMinimizeDetector? minimizeDetector = null)
    {
        _clapDetector = clapDetector ?? new ClapPlayPauseDetector();
        _screenshotDetector = screenshotDetector ?? new CameraFrameScreenshotDetector();
        _maximizeDetector = maximizeDetector ?? new TwoHandMaximizeDetector();
        _minimizeDetector = minimizeDetector ?? new TwoHandMinimizeDetector();
    }

    /// <summary>
    /// Notifies the detector that a fist-grab gesture was released, restarting the 3.0-second active window.
    /// </summary>
    public void NotifyFistReleased()
    {
        State.LastFistReleaseTime = DateTime.Now;
    }

    /// <summary>
    /// Evaluates tracked hands for the current frame to detect Play/Pause, Screenshot, Maximize, or Minimize gestures.
    /// </summary>
    /// <param name="hands">The list of active tracked hands.</param>
    /// <param name="inputSink">The output adapter holding the focused window handle.</param>
    /// <returns>A <see cref="TwoHandGestureDecision"/> if an action was triggered; otherwise, <c>null</c>.</returns>
    public TwoHandGestureDecision? Update(List<TrackedHand>? hands, IInputSink inputSink)
    {
        if (!Enabled || hands == null || hands.Count != 2)
        {
            State.IsCameraFrameActive = false;
            State.ScreenshotHoldTimer.Reset();
            State.ScreenshotHoldDurationSeconds = 0.0;
            State.ScreenshotHoldProgress = 0.0;
            State.ConsecutiveClapFrames = 0;
            ResetTouchState();
            State.DownwardHistory.Clear();
            return null;
        }

        TrackedHand hand1 = hands[0];
        TrackedHand hand2 = hands[1];

        double palmSize1 = hand1.Distance(0, 9);
        double palmSize2 = hand2.Distance(0, 9);
        double avgPalmSize = Math.Max(20.0, (palmSize1 + palmSize2) / 2.0);

        // 1. Gesture: Clap / Prayer Play/Pause (👏 / 🤲)
        if (_clapDetector.Update(hand1, hand2, State, avgPalmSize))
        {
            ResetTouchState();
            State.DownwardHistory.Clear();
            return new TwoHandGestureDecision(TwoHandAction.PlayPause, IntPtr.Zero);
        }

        // 2. Gesture: Camera-Frame Screenshot (Dual "L" Hands + 2.0s Hold)
        if (_screenshotDetector.Update(hand1, hand2, State, avgPalmSize))
        {
            ResetTouchState();
            State.DownwardHistory.Clear();
            return new TwoHandGestureDecision(TwoHandAction.Screenshot, IntPtr.Zero, State.LastCapturedFrameRect);
        }

        // Gating for window actions (Maximize / Minimize)
        if (!State.IsWindowActive || State.InCooldown)
        {
            ResetTouchState();
            State.DownwardHistory.Clear();
            return null;
        }

        IntPtr targetHwnd = inputSink.LastFocusedHwnd;
        if (targetHwnd == IntPtr.Zero)
        {
            return null;
        }

        // 3. Gesture: Maximize (Touch -> Horizontal Expansion Apart)
        if (_maximizeDetector.Update(hand1, hand2, State, avgPalmSize))
        {
            State.DownwardHistory.Clear();
            return new TwoHandGestureDecision(TwoHandAction.Maximize, targetHwnd);
        }

        // 4. Gesture: Minimize (Dual Synchronous Fast Downward Swipe)
        if (_minimizeDetector.Update(hand1, hand2, State, avgPalmSize))
        {
            ResetTouchState();
            return new TwoHandGestureDecision(TwoHandAction.Minimize, targetHwnd);
        }

        return null;
    }

    /// <summary>
    /// Resets active index-touch tracking metrics.
    /// </summary>
    public void ResetTouchState()
    {
        TwoHandMaximizeDetector.ResetTouchState(State);
    }
}
