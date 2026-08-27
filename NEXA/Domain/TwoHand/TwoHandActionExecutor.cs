using System;
using System.IO;
using NEXA.Adapters.Output;

namespace NEXA.Domain.TwoHand;

/// <summary>
/// Domain executor executing physical OS window sizing, desktop screenshot captures, and media playback actions triggered by two-hand gestures.
/// <para>
/// <b>What it is:</b> Hardware and OS command dispatcher for two-hand gesture decisions.
/// </para>
/// </summary>
public class TwoHandActionExecutor
{
    private readonly IInputSink _inputSink;
    private readonly IScreenshotSink _screenshotSink;
    private readonly int _screenWidth;
    private readonly int _screenHeight;

    /// <summary>
    /// Gets or sets the target output directory for saving screenshot PNG files.
    /// </summary>
    public string OutputDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        "NEXA-Screenshots"
    );

    /// <summary>
    /// Initializes a new instance of the <see cref="TwoHandActionExecutor"/> class.
    /// </summary>
    /// <param name="inputSink">The output adapter for OS inputs.</param>
    /// <param name="screenshotSink">The output adapter for desktop screen capture.</param>
    public TwoHandActionExecutor(
        IInputSink? inputSink = null,
        IScreenshotSink? screenshotSink = null)
    {
        _inputSink = inputSink ?? new Win32InputSink();
        _screenshotSink = screenshotSink ?? new Win32ScreenshotSink();

        (int w, int h) = _inputSink.GetScreenResolution();
        _screenWidth = w > 0 ? w : 1920;
        _screenHeight = h > 0 ? h : 1080;
    }

    /// <summary>
    /// Executes the OS action specified in the given gesture decision.
    /// </summary>
    /// <param name="decision">The decision returned by the gesture detector.</param>
    /// <param name="state">The two-hand gesture state container for storing output file paths.</param>
    public void Execute(TwoHandGestureDecision decision, TwoHandGestureState state)
    {
        if (decision.Action == TwoHandAction.Maximize)
        {
            _inputSink.MaximizeWindow(decision.TargetHwnd);
        }
        else if (decision.Action == TwoHandAction.Minimize)
        {
            _inputSink.MinimizeWindow(decision.TargetHwnd);
        }
        else if (decision.Action == TwoHandAction.Screenshot)
        {
            _screenshotSink.CaptureScreenRegion(0, 0, _screenWidth, _screenHeight, OutputDirectory, out string savedFilePath);
            state.LastSavedFilePath = savedFilePath;
        }
        else if (decision.Action == TwoHandAction.PlayPause)
        {
            _inputSink.SendMediaPlayPause();
        }
    }
}
