using System;
using System.Collections.Generic;
using System.Diagnostics;
using NEXA.Detector;
using NEXA.Filter;
using OpenCvSharp;

namespace NEXA.Hand;

/// <summary>
/// End-to-end hand tracking pipeline orchestrator.
/// <para>
/// <b>What it is:</b> The main pipeline coordinator that bridges the Stage 1 <see cref="PalmDetector"/>,
/// the Stage 2 <see cref="HandLandmarkEstimator"/>, the temporal <see cref="OneEuroFilter"/> smoothing array,
/// and delegates gesture classification to <see cref="HandGestureClassifier"/>.
/// </para>
/// </summary>
public class HandTracker : IDisposable
{
    private readonly PalmDetector _palmDetector;
    private readonly HandLandmarkEstimator _landmarkEstimator;
    private readonly Dictionary<int, (OneEuroFilter x, OneEuroFilter y, OneEuroFilter z)> _filters = new();
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    /// <summary>
    /// Gets or sets a value indicating whether OneEuroFilter smoothing is active. Default is <c>true</c>.
    /// </summary>
    public bool SmoothingEnabled { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="HandTracker"/> class, initializing models and filter banks.
    /// </summary>
    /// <param name="palmModelPath">Path to palm_detection.onnx model.</param>
    /// <param name="landmarkModelPath">Path to handpose_estimation.onnx model.</param>
    public HandTracker(string palmModelPath, string landmarkModelPath)
    {
        _palmDetector = new PalmDetector(palmModelPath, scoreThreshold: 0.50f, nmsThreshold: 0.3f);
        _landmarkEstimator = new HandLandmarkEstimator(landmarkModelPath, confThreshold: 0.60f);

        for (int i = 0; i < 21; i++)
        {
            // minCutoff = 1.2 aggressively removes still-hand camera noise;
            // beta = 0.05 ensures rapid lag-free response during fast pointer movement
            _filters[i] = (
                new OneEuroFilter(freq: 30, minCutoff: 1.2, beta: 0.05),
                new OneEuroFilter(freq: 30, minCutoff: 1.2, beta: 0.05),
                new OneEuroFilter(freq: 30, minCutoff: 1.2, beta: 0.05)
            );
        }
    }

    /// <summary>
    /// Processes a single camera video frame through the complete hand tracking pipeline.
    /// </summary>
    /// <param name="frame">The raw camera image frame (BGR Mat).</param>
    /// <returns>A list of tracked hand instances containing smoothed landmarks and classified gestures.</returns>
    public List<TrackedHand> ProcessFrame(Mat frame)
    {
        List<TrackedHand> hands = new();
        List<PalmDetectionResult> palms = _palmDetector.Detect(frame);
        double timestamp = _stopwatch.Elapsed.TotalSeconds;

        foreach (PalmDetectionResult palm in palms)
        {
            HandLandmarkResult? landmarkResult = _landmarkEstimator.Estimate(frame, palm);
            if (landmarkResult == null)
            {
                continue;
            }

            TrackedHand tracked = new()
            {
                RawResult = landmarkResult,
                SmoothedLandmarks2D = new Point2f[21],
                SmoothedLandmarks3D = new Point3f[21]
            };

            // Apply 1€ adaptive filtering to all 21 joints
            for (int i = 0; i < 21; i++)
            {
                Point3f pt = landmarkResult.Landmarks[i];
                if (SmoothingEnabled)
                {
                    float sx = (float)_filters[i].x.Filter(pt.X, timestamp);
                    float sy = (float)_filters[i].y.Filter(pt.Y, timestamp);
                    float sz = (float)_filters[i].z.Filter(pt.Z, timestamp);
                    tracked.SmoothedLandmarks2D[i] = new Point2f(sx, sy);
                    tracked.SmoothedLandmarks3D[i] = new Point3f(sx, sy, sz);
                }
                else
                {
                    tracked.SmoothedLandmarks2D[i] = landmarkResult.Landmarks2D[i];
                    tracked.SmoothedLandmarks3D[i] = pt;
                }
            }

            // Classify hand pose gesture via specialized anatomical classifier
            tracked.Gesture = HandGestureClassifier.Classify(tracked);
            hands.Add(tracked);
        }

        // Reset filter states when no hands are in the scene to prevent interpolation drift
        if (palms.Count == 0)
        {
            foreach ((OneEuroFilter x, OneEuroFilter y, OneEuroFilter z) in _filters.Values)
            {
                x.Reset();
                y.Reset();
                z.Reset();
            }
        }

        return hands;
    }

    /// <summary>
    /// Releases the ONNX Runtime sessions for both palm and landmark models.
    /// </summary>
    public void Dispose()
    {
        _palmDetector.Dispose();
        _landmarkEstimator.Dispose();
        GC.SuppressFinalize(this);
    }
}
