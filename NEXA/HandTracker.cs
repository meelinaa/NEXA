using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenCvSharp;

namespace NEXA;

public class TrackedHand
{
    public HandLandmarkResult RawResult { get; set; } = new();
    public Point2f[] SmoothedLandmarks2D { get; set; } = new Point2f[21];
    public Point3f[] SmoothedLandmarks3D { get; set; } = new Point3f[21];
    public string Gesture { get; set; } = "Unknown";
    public string Handedness => RawResult.Handedness;
    public float Confidence => RawResult.Confidence;
    public Rect2f BoundingBox => RawResult.BoundingBox;
    public double Distance(int idx1, int idx2)
    {
        var p1 = SmoothedLandmarks2D[idx1];
        var p2 = SmoothedLandmarks2D[idx2];
        float dx = p1.X - p2.X;
        float dy = p1.Y - p2.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

public class HandTracker : IDisposable
{
    private readonly PalmDetector _palmDetector;
    private readonly HandLandmarkEstimator _landmarkEstimator;
    private readonly Dictionary<int, (OneEuroFilter x, OneEuroFilter y, OneEuroFilter z)> _filters = new();
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public bool SmoothingEnabled { get; set; } = true; // Fine-tuned low-latency filter enabled by default

    public HandTracker(string palmModelPath, string landmarkModelPath)
    {
        _palmDetector = new PalmDetector(palmModelPath, scoreThreshold: 0.50f, nmsThreshold: 0.3f);
        _landmarkEstimator = new HandLandmarkEstimator(landmarkModelPath, confThreshold: 0.60f);

        for (int i = 0; i < 21; i++)
        {
            // minCutoff = 1.2 removes still-hand jitter; beta = 0.05 gives zero-lag response during motion
            _filters[i] = (
                new OneEuroFilter(freq: 30, minCutoff: 1.2, beta: 0.05),
                new OneEuroFilter(freq: 30, minCutoff: 1.2, beta: 0.05),
                new OneEuroFilter(freq: 30, minCutoff: 1.2, beta: 0.05)
            );
        }
    }

    public List<TrackedHand> ProcessFrame(Mat frame)
    {
        var hands = new List<TrackedHand>();
        var palms = _palmDetector.Detect(frame);
        double timestamp = _stopwatch.Elapsed.TotalSeconds;

        foreach (var palm in palms)
        {
            var landmarkResult = _landmarkEstimator.Estimate(frame, palm);
            if (landmarkResult == null) continue;

            var tracked = new TrackedHand
            {
                RawResult = landmarkResult,
                SmoothedLandmarks2D = new Point2f[21],
                SmoothedLandmarks3D = new Point3f[21]
            };

            for (int i = 0; i < 21; i++)
            {
                var pt = landmarkResult.Landmarks[i];
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

            tracked.Gesture = RecognizeGesture(tracked);
            hands.Add(tracked);
        }

        if (palms.Count == 0)
        {
            foreach (var filter in _filters.Values)
            {
                filter.x.Reset();
                filter.y.Reset();
                filter.z.Reset();
            }
        }

        return hands;
    }

    private static string RecognizeGesture(TrackedHand hand)
    {
        var lm = hand.SmoothedLandmarks2D;
        double palmSize = hand.Distance(0, 9); // Wrist to middle finger MCP
        if (palmSize <= 1.0) return "Hand";

        // Check extended fingers
        // Finger is extended if tip is further from wrist than PIP joint
        bool thumbExt = hand.Distance(4, 0) > hand.Distance(2, 0) * 1.15;
        bool indexExt = hand.Distance(8, 0) > hand.Distance(6, 0) * 1.15;
        bool middleExt = hand.Distance(12, 0) > hand.Distance(10, 0) * 1.15;
        bool ringExt = hand.Distance(16, 0) > hand.Distance(14, 0) * 1.15;
        bool pinkyExt = hand.Distance(20, 0) > hand.Distance(18, 0) * 1.15;

        // Finger gaps
        double indexMiddleGap = hand.Distance(8, 12);
        double middleRingGap = hand.Distance(12, 16);
        double ringPinkyGap = hand.Distance(16, 20);

        bool spockSplit = middleRingGap > indexMiddleGap * 1.6 && middleRingGap > ringPinkyGap * 1.6;

        // 1. Spock (Vulcan Salute)
        if (thumbExt && indexExt && middleExt && ringExt && pinkyExt && spockSplit)
        {
            return "Spock";
        }

        // 2. Fist (Faust: Alle 4 Finger eingeklappt, Daumen liegt über den Fingern)
        // Bei unklarer Haltung (alle 4 Finger eingerollt) wird immer vorrangig Faust gewählt!
        bool allFourFingersFolded = !indexExt && !middleExt && !ringExt && !pinkyExt;
        double indexCurlDist = hand.Distance(8, 0); // Zeigefingerspitze zum Handgelenk

        if (allFourFingersFolded)
        {
            // Wenn alle 4 Finger gebeugt sind -> Faust
            return "Fist";
        }

        // 3. L-Sign (Daumen und Zeigefinger gestreckt, restliche 3 Finger gefaltet)
        double thumbIndexDist = hand.Distance(4, 8);
        if (thumbExt && indexExt && !middleExt && !ringExt && !pinkyExt && thumbIndexDist > palmSize * 0.75)
        {
            return "L";
        }

        // 4. Pinch Closed & Zoom (Mittel-, Ring-, kleiner Finger gefaltet, Zeigefinger und Daumen aktiv)
        if (!middleExt && !ringExt && !pinkyExt)
        {
            if (thumbIndexDist < palmSize * 0.25)
            {
                return "Pinch Closed";
            }
            return "Zoom (L <-> Pinch)";
        }

        // 5. Pinch mit offener Hand (restliche Finger gestreckt oder neutral)
        if (thumbIndexDist < palmSize * 0.25)
        {
            return "Pinch";
        }

        // 6. Open Hand (Alle 5 Finger gestreckt)
        if (thumbExt && indexExt && middleExt && ringExt && pinkyExt)
        {
            return "Open Palm";
        }

        // 7. Victory / Peace (Index + Middle extended, others closed)
        if (indexExt && middleExt && !ringExt && !pinkyExt)
        {
            return "Peace";
        }

        // 8. Pointing (Index extended, others closed)
        if (indexExt && !middleExt && !ringExt && !pinkyExt)
        {
            return "Pointing";
        }

        // Thumbs Up (Thumb extended up, other fingers folded)
        if (thumbExt && !indexExt && !middleExt && !ringExt && !pinkyExt)
        {
            // Check if thumb tip is above wrist
            if (lm[4].Y < lm[0].Y)
            {
                return "Thumbs Up";
            }
        }

        // Rock / Spider-Man (Thumb, Index, Pinky extended, Middle & Ring folded)
        if (indexExt && pinkyExt && !middleExt && !ringExt)
        {
            return "Rock";
        }

        return "Tracking";
    }

    public void Dispose()
    {
        _palmDetector.Dispose();
        _landmarkEstimator.Dispose();
        GC.SuppressFinalize(this);
    }
}
