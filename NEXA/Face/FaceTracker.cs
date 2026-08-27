using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NEXA.Detector;
using NEXA.Filter;
using OpenCvSharp;

namespace NEXA.Face;

/// <summary>
/// Two-stage deep-learning Face Tracker based on Google MediaPipe BlazeFace and FaceMesh running via ONNX Runtime.
/// <para>
/// <b>What it is:</b> 100% pure ONNX facial tracking engine providing sub-millisecond face bounding boxes and 468 3D landmark points.
/// </para>
/// </summary>
public class FaceTracker : IDisposable
{
    private readonly BlazeFaceDetector? _blazeFaceDetector;
    private readonly FaceLandmarkEstimator? _landmarkEstimator;

    private readonly OneEuroFilter2D _boxPosFilter = new(30.0, 1.2, 0.005);
    private readonly OneEuroFilter2D _boxSizeFilter = new(30.0, 1.2, 0.005);
    private readonly OneEuroFilter2D[] _landmarkFilters = new OneEuroFilter2D[468];

    /// <summary>
    /// Gets or sets a value indicating whether face tracking is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="FaceTracker"/> class.
    /// </summary>
    public FaceTracker()
    {
        for (int i = 0; i < 468; i++)
        {
            _landmarkFilters[i] = new OneEuroFilter2D(30.0, 1.5, 0.007);
        }

        string bzPath = FindModelPath("blazeface.onnx");
        if (!string.IsNullOrEmpty(bzPath) && File.Exists(bzPath))
        {
            _blazeFaceDetector = new BlazeFaceDetector(bzPath, scoreThreshold: 0.60f, nmsThreshold: 0.30f);
            Console.WriteLine($"[FaceTracker] BlazeFace ONNX Detector loaded from: {bzPath}");
        }

        string fmPath = FindModelPath("face_mesh.onnx");
        if (!string.IsNullOrEmpty(fmPath) && File.Exists(fmPath))
        {
            _landmarkEstimator = new FaceLandmarkEstimator(fmPath, confThreshold: 0.50f);
            Console.WriteLine($"[FaceTracker] MediaPipe FaceMesh ONNX Estimator loaded from: {fmPath}");
        }
    }

    private static string FindModelPath(string fileName)
    {
        string[] searchPaths =
        [
            Path.Combine(AppContext.BaseDirectory, "models", fileName),
            Path.Combine("models", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "models", fileName)
        ];

        foreach (string path in searchPaths)
        {
            if (File.Exists(path))
                return path;
        }

        return string.Empty;
    }

    /// <summary>
    /// Analyzes the camera frame to detect human faces and regress all 468 landmark tracking points.
    /// </summary>
    /// <param name="frame">The camera BGR frame.</param>
    /// <returns>A <see cref="TrackedFace"/> instance if detected; otherwise, <c>null</c>.</returns>
    public TrackedFace? ProcessFrame(Mat frame)
    {
        if (!Enabled || frame == null || frame.Empty() || _blazeFaceDetector == null || _landmarkEstimator == null)
            return null;

        double timestamp = DateTime.Now.Ticks / (double)TimeSpan.TicksPerSecond;

        // 1. Stage 1: Detect face bounding boxes & orientation keypoints via BlazeFace SSD
        List<BlazeFaceDetectionResult> faceDetections = _blazeFaceDetector.Detect(frame);
        if (faceDetections.Count == 0)
            return null;

        // Select the primary (largest) face
        BlazeFaceDetectionResult primaryFace = faceDetections
            .OrderByDescending(f => f.Box.Width * f.Box.Height)
            .First();

        // 2. Stage 2: Regress 468 facial landmarks from the cropped/aligned face patch
        Point2f[]? rawLandmarks = _landmarkEstimator.Estimate(frame, primaryFace, out float confidence);
        if (rawLandmarks == null || rawLandmarks.Length < 468)
            return null;

        // 3. Apply Isotropic 2D OneEuroFilter smoothing to all 468 landmark points
        Point2f[] smoothedLandmarks = new Point2f[468];
        for (int i = 0; i < 468; i++)
        {
            smoothedLandmarks[i] = _landmarkFilters[i].Filter(rawLandmarks[i], timestamp);
        }

        // 4. Smooth the Head Bounding Box
        Rect2d rawBox = primaryFace.Box;
        Point2f smoothedPos = _boxPosFilter.Filter(new Point2f((float)rawBox.X, (float)rawBox.Y), timestamp);
        Point2f smoothedSize = _boxSizeFilter.Filter(new Point2f((float)rawBox.Width, (float)rawBox.Height), timestamp);
        Rect2f smoothedBox = new(smoothedPos.X, smoothedPos.Y, smoothedSize.X, smoothedSize.Y);

        // 5. Derive Anatomical Keypoints from 468-point MediaPipe Index Standard
        // Top Lip: 0, Bottom Lip: 17
        Point2f topLip = smoothedLandmarks[0];
        Point2f bottomLip = smoothedLandmarks[17];
        Point2f mouthCenter = new((topLip.X + bottomLip.X) / 2.0f, (topLip.Y + bottomLip.Y) / 2.0f);

        // Mouth Corners: Right: 61, Left: 291
        Point2f rightCorner = smoothedLandmarks[61];
        Point2f leftCorner = smoothedLandmarks[291];
        double mouthWidth = Math.Sqrt(Math.Pow(leftCorner.X - rightCorner.X, 2) + Math.Pow(leftCorner.Y - rightCorner.Y, 2));
        float mouthRadius = (float)Math.Max(35.0, mouthWidth * 0.70);

        Point2f leftEye = smoothedLandmarks[386];  // Left pupil / eye center
        Point2f rightEye = smoothedLandmarks[159]; // Right pupil / eye center
        Point2f noseTip = smoothedLandmarks[1];    // Nose tip

        return new TrackedFace
        {
            BoundingBox = smoothedBox,
            Landmarks = smoothedLandmarks,
            MouthCenter = mouthCenter,
            MouthRadius = mouthRadius,
            LeftEye = leftEye,
            RightEye = rightEye,
            NoseTip = noseTip,
            Confidence = confidence
        };
    }

    /// <summary>
    /// Disposes ONNX detector and estimator resources.
    /// </summary>
    public void Dispose()
    {
        _blazeFaceDetector?.Dispose();
        _landmarkEstimator?.Dispose();
        GC.SuppressFinalize(this);
    }
}
