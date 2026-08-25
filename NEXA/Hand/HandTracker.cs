using NEXA.Detector;
using NEXA.Filter;
using OpenCvSharp;
using System.Diagnostics;

namespace NEXA.Hand;

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

        // 1. Finger Extension Checks (Spitzen weiter weg vom Knöchel als Mittelgelenke)
        bool indexExt = hand.Distance(8, 0) > hand.Distance(6, 0) * 1.12 && hand.Distance(8, 5) > hand.Distance(6, 5) * 1.25;
        bool middleExt = hand.Distance(12, 0) > hand.Distance(10, 0) * 1.12 && hand.Distance(12, 9) > hand.Distance(10, 9) * 1.25;
        bool ringExt = hand.Distance(16, 0) > hand.Distance(14, 0) * 1.12 && hand.Distance(16, 13) > hand.Distance(14, 13) * 1.25;
        bool pinkyExt = hand.Distance(20, 0) > hand.Distance(18, 0) * 1.12 && hand.Distance(20, 17) > hand.Distance(18, 17) * 1.25;

        // 2. Thumb Extension Check (Daumen ist weit von den Fingerknöcheln abgespreizt und gestreckt)
        double thumbToKnuckle5 = hand.Distance(4, 5); // Abstand Daumenspitze zu Zeigefingerknöchel
        double thumbToKnuckle9 = hand.Distance(4, 9); // Abstand Daumenspitze zu Mittelfingerknöchel
        double thumbTipToWrist = hand.Distance(4, 0);

        bool thumbStretchedOut = thumbToKnuckle5 > palmSize * 0.58 && thumbToKnuckle9 > palmSize * 0.68;
        bool thumbWideL = thumbToKnuckle5 > palmSize * 0.72 && thumbToKnuckle9 > palmSize * 0.85;

        // Finger gaps
        double indexMiddleGap = hand.Distance(8, 12);
        double middleRingGap = hand.Distance(12, 16);
        double ringPinkyGap = hand.Distance(16, 20);

        bool spockSplit = middleRingGap > indexMiddleGap * 1.5 && middleRingGap > ringPinkyGap * 1.5;

        // --- GESTENERKENNUNG ---

        // A. Spock (Vulcan Salute)
        if (thumbStretchedOut && indexExt && middleExt && ringExt && pinkyExt && spockSplit)
        {
            return "Spock";
        }

        // B. Open Palm (Alle 5 Finger gestreckt)
        if (indexExt && middleExt && ringExt && pinkyExt && (thumbStretchedOut || thumbToKnuckle5 > palmSize * 0.45))
        {
            return "Open Palm";
        }

        // C. 4 Finger eingeklappt (Index, Mittel, Ring, Pinky sind alle gefaltet)
        if (!indexExt && !middleExt && !ringExt && !pinkyExt)
        {
            // Thumbs Up: Daumen ist nach oben aufgerichtet, weit weg von den gefalteten Fingern
            double thumbToIndexTip = hand.Distance(4, 8);
            bool thumbPointsUp = lm[4].Y < (lm[2].Y - palmSize * 0.15); // Spitze deutlich höher als Knöchel

            if (thumbStretchedOut && thumbToIndexTip > palmSize * 0.50 && thumbPointsUp)
            {
                return "Thumbs Up";
            }

            // Ansonsten: Faust (Daumen liegt über den Fingern oder an der Seite)
            return "Fist";
        }

        // D. Peace / Victory (Zeige- und Mittelfinger gestreckt)
        if (indexExt && middleExt && !ringExt && !pinkyExt)
        {
            return "Peace";
        }

        // E. Rock / Spider-Man (Zeigefinger und kleiner Finger gestreckt)
        if (indexExt && pinkyExt && !middleExt && !ringExt)
        {
            return "Rock";
        }

        // F. Pinch-Prüfung (Daumenspitze und Zeigefingerspitze berühren sich)
        double thumbIndexDist = hand.Distance(4, 8);
        if (thumbIndexDist < palmSize * 0.25)
        {
            if (!middleExt && !ringExt && !pinkyExt)
            {
                return "Pinch Closed";
            }
            return "Pinch";
        }

        // G. Nur Zeigefinger aktiv (Mittel, Ring, Pinky gefaltet)
        if (indexExt && !middleExt && !ringExt && !pinkyExt)
        {
            // L-Sign: Daumen ist absichtlich weit im 90°-Winkel abgespreizt
            if (thumbWideL)
            {
                return "L";
            }

            // Normales Pointing (Daumen eingeklappt oder neutral am Zeigefinger anliegend)
            return "Pointing";
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
