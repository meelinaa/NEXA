using NEXA.Detector;
using NEXA.Filter;
using OpenCvSharp;
using System.Diagnostics;

namespace NEXA.Hand;

/// <summary>
/// End-to-end hand tracking pipeline orchestrator.
/// <para>
/// <b>What it is:</b> The main pipeline coordinator that bridges the Stage 1 <see cref="PalmDetector"/>,
/// the Stage 2 <see cref="HandLandmarkEstimator"/>, the temporal <see cref="OneEuroFilter"/> smoothing array,
/// and heuristic anatomical gesture classification.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="number">
/// <item><description>Executes <see cref="PalmDetector.Detect"/> on the incoming full camera frame to find all visible palm candidates.</description></item>
/// <item><description>For each detected palm, runs <see cref="HandLandmarkEstimator.Estimate"/> to regress 21 fine-grained 3D finger landmarks.</description></item>
/// <item><description>Passes every joint coordinate through a dedicated bank of 21 3D <see cref="OneEuroFilter"/> instances to eliminate noise and jitter.</description></item>
/// <item><description>Analyzes skeletal joint angles, finger extensions, and knuckle gaps via <see cref="RecognizeGesture"/> to classify hand poses in real-time.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Why it is used:</b> Encapsulates the entire machine learning inference, temporal signal processing, and pose estimation logic behind a clean, single-method call <see cref="ProcessFrame"/>.
/// </para>
/// <para>
/// <b>Consequence:</b> Returns a clean, ready-to-use list of <see cref="TrackedHand"/> objects every frame for application controllers.
/// </para>
/// </summary>
public class HandTracker : IDisposable
{
    /// <summary>
    /// Stage 1 palm detection neural network instance.
    /// </summary>
    private readonly PalmDetector _palmDetector;

    /// <summary>
    /// Stage 2 hand landmark estimation neural network instance.
    /// </summary>
    private readonly HandLandmarkEstimator _landmarkEstimator;

    /// <summary>
    /// Bank of 21 OneEuroFilters (one X/Y/Z triplet per landmark joint) providing adaptive jitter reduction.
    /// </summary>
    private readonly Dictionary<int, (OneEuroFilter x, OneEuroFilter y, OneEuroFilter z)> _filters = new();

    /// <summary>
    /// High-resolution stopwatch providing accurate frame timestamps for temporal filter computations.
    /// </summary>
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
        List<TrackedHand> hands = [];
        List<PalmDetectionResult> palms = _palmDetector.Detect(frame);
        double timestamp = _stopwatch.Elapsed.TotalSeconds;

        foreach (PalmDetectionResult palm in palms)
        {
            HandLandmarkResult? landmarkResult = _landmarkEstimator.Estimate(frame, palm);
            if (landmarkResult == null) continue;

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

            // Classify hand pose gesture
            tracked.Gesture = RecognizeGesture(tracked);
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
    /// Heuristic gesture classification analyzing skeletal joint extensions, thumb abduction, and inter-finger gaps.
    /// </summary>
    /// <param name="hand">The tracked hand with smoothed landmark coordinates.</param>
    /// <returns>A string identifying the classified gesture.</returns>
    private static string RecognizeGesture(TrackedHand hand)
    {
        Point2f[] lm = hand.SmoothedLandmarks2D;
        double palmSize = hand.Distance(0, 9); // Distance from wrist (0) to middle finger MCP knuckle (9)
        if (palmSize <= 1.0) return "Hand";

        // 1. Finger Extension Checks: Fingertip must be significantly farther from wrist and knuckle than the intermediate PIP joint
        bool indexExt = hand.Distance(8, 0) > hand.Distance(6, 0) * 1.12 && hand.Distance(8, 5) > hand.Distance(6, 5) * 1.25;
        bool middleExt = hand.Distance(12, 0) > hand.Distance(10, 0) * 1.12 && hand.Distance(12, 9) > hand.Distance(10, 9) * 1.25;
        bool ringExt = hand.Distance(16, 0) > hand.Distance(14, 0) * 1.12 && hand.Distance(16, 13) > hand.Distance(14, 13) * 1.25;
        bool pinkyExt = hand.Distance(20, 0) > hand.Distance(18, 0) * 1.12 && hand.Distance(20, 17) > hand.Distance(18, 17) * 1.25;

        // 2. Thumb Abduction Checks: Distance from thumb tip (4) to index (5) and middle (9) knuckles
        double thumbToKnuckle5 = hand.Distance(4, 5);
        double thumbToKnuckle9 = hand.Distance(4, 9);

        bool thumbStretchedOut = thumbToKnuckle5 > palmSize * 0.58 && thumbToKnuckle9 > palmSize * 0.68;
        bool thumbWideL = thumbToKnuckle5 > palmSize * 0.72 && thumbToKnuckle9 > palmSize * 0.85;

        // 3. Inter-finger angular separation gaps
        double indexMiddleGap = hand.Distance(8, 12);
        double middleRingGap = hand.Distance(12, 16);
        double ringPinkyGap = hand.Distance(16, 20);

        bool spockSplit = middleRingGap > indexMiddleGap * 1.5 && middleRingGap > ringPinkyGap * 1.5;

        // --- GESTURE HIERARCHY EVALUATION ---

        // A. Spock (Vulcan Salute)
        if (thumbStretchedOut && indexExt && middleExt && ringExt && pinkyExt && spockSplit)
            return "Spock";

        // B. Open Hand: Hand Up vs Hand Down
        bool allFingersExtended = indexExt && middleExt && ringExt && pinkyExt;
        if (allFingersExtended || (indexExt && middleExt && ringExt))
        {
            bool fingersPointingUp = lm[12].Y < (lm[0].Y - palmSize * 0.5) && lm[12].Y < lm[9].Y;
            bool fingersPointingDown = lm[12].Y > lm[9].Y || lm[8].Y > lm[5].Y || lm[12].Y > (lm[0].Y - palmSize * 0.2);

            if (fingersPointingUp)
                return "Hand Up";

            if (fingersPointingDown)
                return "Hand Down";

            return "Open Palm";
        }

        // C. 4 Fingers Folded (Index, Middle, Ring, Pinky curled into palm)
        if (!indexExt && !middleExt && !ringExt && !pinkyExt)
        {
            double thumbToIndexTip = hand.Distance(4, 8);
            bool thumbPointsUp = lm[4].Y < (lm[2].Y - palmSize * 0.15);

            if (thumbStretchedOut && thumbToIndexTip > palmSize * 0.50 && thumbPointsUp)
                return "Thumbs Up";

            // Otherwise, clenched fist
            return "Fist";
        }

        // D. Peace / Victory (Index and Middle extended)
        if (indexExt && middleExt && !ringExt && !pinkyExt)
            return "Peace";

        // E. Rock / Spider-Man (Index and Pinky extended)
        if (indexExt && pinkyExt && !middleExt && !ringExt)
            return "Rock";

        // F. Pinch Detection (Thumb tip and Index fingertip in contact)
        double thumbIndexDist = hand.Distance(4, 8);
        if (thumbIndexDist < palmSize * 0.25)
        {
            if (!middleExt && !ringExt && !pinkyExt)
                return "Pinch Closed";

            return "Pinch";
        }

        // G. Single Pointer Finger Active (Middle, Ring, Pinky folded)
        if (indexExt && !middleExt && !ringExt && !pinkyExt)
        {
            if (thumbWideL)
                return "L";

            return "Pointing";
        }

        return "Tracking";
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
