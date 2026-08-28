using System;
using NEXA.Abstractions;
using OpenCvSharp;

namespace NEXA.Tests.Fakes;

/// <summary>
/// Synthetic procedural video stream source generating deterministic test patterns (SMPTE bars, moving geometric objects, simulated hand trajectory vectors)
/// for headless end-to-end testing of the NEXA engine without physical cameras.
/// </summary>
public class SyntheticVideoVectorSource : IFrameSource
{
    private readonly int _totalFrames;
    private readonly int _width;
    private readonly int _height;
    private readonly SyntheticPatternType _patternType;
    private int _currentFrame = 0;

    /// <summary>
    /// Supported synthetic pattern generators.
    /// </summary>
    public enum SyntheticPatternType
    {
        /// <summary>
        /// SMPTE color bars for video pipeline calibration and color conversion checks.
        /// </summary>
        SmpteColorBars,

        /// <summary>
        /// Moving geometric target simulating hand movement across the screen.
        /// </summary>
        MovingTargetVector,

        /// <summary>
        /// Simulated YUV/grayscale gradient ramp.
        /// </summary>
        GradientRamp
    }

    /// <summary>
    /// Gets the current frame index being produced.
    /// </summary>
    public int CurrentFrameIndex => _currentFrame;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyntheticVideoVectorSource"/> class.
    /// </summary>
    /// <param name="totalFrames">Total number of frames to stream before reporting EOF.</param>
    /// <param name="width">Frame width in pixels (default: 1280).</param>
    /// <param name="height">Frame height in pixels (default: 720).</param>
    /// <param name="patternType">Type of synthetic procedural pattern to generate.</param>
    public SyntheticVideoVectorSource(
        int totalFrames = 30,
        int width = 1280,
        int height = 720,
        SyntheticPatternType patternType = SyntheticPatternType.MovingTargetVector)
    {
        _totalFrames = totalFrames;
        _width = width;
        _height = height;
        _patternType = patternType;
    }

    /// <inheritdoc/>
    public bool Open(int index) => true;

    /// <inheritdoc/>
    public bool IsOpened() => true;

    /// <inheritdoc/>
    public bool Read(Mat image)
    {
        if (_currentFrame >= _totalFrames)
        {
            return false;
        }

        _currentFrame++;

        using Mat generated = GeneratePattern(_currentFrame, _width, _height, _patternType);
        generated.CopyTo(image);
        return true;
    }

    /// <inheritdoc/>
    public bool Set(VideoCaptureProperties property, double value) => true;

    /// <summary>
    /// Generates a single synthetic test vector frame.
    /// </summary>
    public static Mat GeneratePattern(int frameIndex, int width, int height, SyntheticPatternType patternType)
    {
        Mat mat = new(height, width, MatType.CV_8UC3, new Scalar(20, 20, 20));

        switch (patternType)
        {
            case SyntheticPatternType.SmpteColorBars:
                DrawSmpteBars(mat, width, height);
                break;

            case SyntheticPatternType.MovingTargetVector:
                DrawMovingTarget(mat, frameIndex, width, height);
                break;

            case SyntheticPatternType.GradientRamp:
                DrawGradientRamp(mat, width, height);
                break;
        }

        // Draw frame index timestamp counter overlay
        Cv2.PutText(mat, $"TEST VECTOR FRAME #{frameIndex:D5}", new Point(30, 40),
            HersheyFonts.HersheySimplex, 0.7, Scalar.White, 2, LineTypes.AntiAlias);

        return mat;
    }

    private static void DrawSmpteBars(Mat mat, int width, int height)
    {
        Scalar[] colors =
        [
            new Scalar(255, 255, 255), // White
            new Scalar(0, 255, 255),   // Yellow
            new Scalar(255, 255, 0),   // Cyan
            new Scalar(0, 255, 0),     // Green
            new Scalar(255, 0, 255),   // Magenta
            new Scalar(0, 0, 255),     // Red
            new Scalar(255, 0, 0)      // Blue
        ];

        int barWidth = width / colors.Length;
        for (int i = 0; i < colors.Length; i++)
        {
            int x1 = i * barWidth;
            int x2 = (i == colors.Length - 1) ? width : x1 + barWidth;
            Cv2.Rectangle(mat, new Rect(x1, 0, x2 - x1, height), colors[i], -1);
        }
    }

    private static void DrawMovingTarget(Mat mat, int frameIndex, int width, int height)
    {
        // Compute moving sinusoidal orbital trajectory simulating realistic hand movement
        double t = frameIndex * 0.1;
        int cx = (int)(width / 2.0 + Math.Cos(t) * (width * 0.3));
        int cy = (int)(height / 2.0 + Math.Sin(t) * (height * 0.3));

        // Background subtle grid
        for (int x = 0; x < width; x += 80)
        {
            Cv2.Line(mat, new Point(x, 0), new Point(x, height), new Scalar(40, 40, 40), 1);
        }
        for (int y = 0; y < height; y += 80)
        {
            Cv2.Line(mat, new Point(0, y), new Point(width, y), new Scalar(40, 40, 40), 1);
        }

        // Draw animated target representing synthetic hand region
        Cv2.Circle(mat, new Point(cx, cy), 60, new Scalar(0, 165, 255), -1, LineTypes.AntiAlias); // Orange palm
        Cv2.Circle(mat, new Point(cx, cy - 80), 20, new Scalar(0, 255, 0), -1, LineTypes.AntiAlias); // Green fingertip
        Cv2.Line(mat, new Point(cx, cy), new Point(cx, cy - 80), new Scalar(0, 255, 255), 4);
    }

    private static void DrawGradientRamp(Mat mat, int width, int height)
    {
        for (int x = 0; x < width; x++)
        {
            byte val = (byte)(x * 255 / width);
            Cv2.Line(mat, new Point(x, 0), new Point(x, height), new Scalar(val, val, val), 1);
        }
    }

    /// <inheritdoc/>
    public void Dispose() { }
}
