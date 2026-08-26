using System;
using System.Collections.Generic;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using OpenCvSharp.Dnn;

namespace NEXA.Detector;

/// <summary>
/// Stage 1 Object Detector based on the Google MediaPipe BlazePalm architecture running via ONNX Runtime.
/// <para>
/// <b>What it is:</b> A Single-Shot Detector (SSD) neural network optimized for mobile and real-time palm detection.
/// </para>
/// <para>
/// <b>What it does:</b> Takes a raw camera frame, performs aspect-ratio preserving letterbox scaling to 192x192 pixels,
/// runs ONNX neural network inference, decodes 2016 anchor-based bounding box regressions, computes Sigmoid confidence scores,
/// and applies Non-Maximum Suppression (NMS) to eliminate duplicate overlapping detections.
/// </para>
/// <para>
/// <b>Why it is used:</b> Directly finding 21 3D finger landmarks in a full 720p/1080p frame is computationally expensive.
/// PalmDetector quickly identifies the coarse bounding box and orientation of the hand in the whole frame, allowing the Stage 2
/// Landmark Estimator to run only on a tight, high-resolution crop.
/// </para>
/// <para>
/// <b>Result:</b> Returns a list of <see cref="PalmDetectionResult"/> containing calibrated bounding boxes and 7 keypoints in original frame coordinates.
/// </para>
/// </summary>
public class PalmDetector : IDisposable
{
    /// <summary>
    /// The active ONNX Runtime inference engine holding the compiled neural network graph in memory.
    /// </summary>
    private readonly InferenceSession _session;

    /// <summary>
    /// Precalculated normalized anchor center points [ax, ay] across both feature map grids (24x24 and 12x12).
    /// Total count is exactly 2016 anchors.
    /// </summary>
    private readonly float[][] _anchors;

    /// <summary>
    /// Minimum confidence threshold (0.0 to 1.0) required to consider a candidate detection valid.
    /// </summary>
    private readonly float _scoreThreshold;

    /// <summary>
    /// Intersection-over-Union (IoU) threshold for Non-Maximum Suppression (NMS).
    /// Overlapping bounding boxes with IoU greater than this threshold are suppressed.
    /// </summary>
    private readonly float _nmsThreshold;

    /// <summary>
    /// Fixed input width required by the BlazePalm ONNX model tensor shape [1, 192, 192, 3].
    /// </summary>
    private const int InputWidth = 192;

    /// <summary>
    /// Fixed input height required by the BlazePalm ONNX model tensor shape [1, 192, 192, 3].
    /// </summary>
    private const int InputHeight = 192;

    /// <summary>
    /// Initializes a new instance of the <see cref="PalmDetector"/> class, loading the ONNX model and generating anchor boxes.
    /// </summary>
    /// <param name="modelPath">The file path to the palm_detection.onnx model.</param>
    /// <param name="scoreThreshold">Minimum confidence score required for candidate boxes (default: 0.6f).</param>
    /// <param name="nmsThreshold">IoU threshold used during Non-Maximum Suppression to remove duplicates (default: 0.3f).</param>
    public PalmDetector(string modelPath, float scoreThreshold = 0.6f, float nmsThreshold = 0.3f)
    {
        _scoreThreshold = scoreThreshold;
        _nmsThreshold = nmsThreshold;

        // Configure ONNX Runtime session for optimal CPU inference performance
        SessionOptions options = new()
        {
            // Enables constant folding, node fusion, and kernel optimizations in the ONNX execution graph
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            // Executes graph nodes sequentially within a thread pool to minimize thread context-switching overhead
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
        };

        _session = new InferenceSession(modelPath, options);
        _anchors = GenerateAnchors();
    }

    /// <summary>
    /// Pre-generates the static grid of 2016 anchor points used by the SSD architecture.
    /// <para>
    /// <b>How it works:</b>
    /// <list type="bullet">
    /// <item><description>Layer 1 (24x24 grid, stride 8): Generates 2 anchors per cell = 1152 anchors (tuned for distant/smaller hands).</description></item>
    /// <item><description>Layer 2 (12x12 grid, stride 16): Generates 6 anchors per cell = 864 anchors (tuned for close-up/larger hands).</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Why it is needed:</b> The neural network does not predict absolute pixel coordinates from scratch;
    /// it predicts offsets (deltas) relative to these pre-defined anchor centers.
    /// </para>
    /// </summary>
    /// <returns>An array of normalized [ax, ay] anchor coordinates.</returns>
    private static float[][] GenerateAnchors()
    {
        // Initializing with explicit capacity (2016) allocates memory upfront, avoiding runtime re-allocations
        List<float[]> anchors = new(2016);

        // Feature Map 1: 24x24 grid (stride 8) - 2 anchors per cell
        for (int y = 0; y < 24; y++)
        {
            for (int x = 0; x < 24; x++)
            {
                float ax = (x + 0.5f) / 24.0f; // Normalized horizontal center of the grid cell
                float ay = (y + 0.5f) / 24.0f; // Normalized vertical center of the grid cell
                anchors.Add([ax, ay]);
                anchors.Add([ax, ay]);
            }
        }

        // Feature Map 2: 12x12 grid (stride 16) - 6 anchors per cell
        for (int y = 0; y < 12; y++)
        {
            for (int x = 0; x < 12; x++)
            {
                float ax = (x + 0.5f) / 12.0f;
                float ay = (y + 0.5f) / 12.0f;
                for (int i = 0; i < 6; i++)
                {
                    anchors.Add([ax, ay]);
                }
            }
        }

        return anchors.ToArray();
    }

    /// <summary>
    /// Detects all visible hands/palms in the given input image frame.
    /// <para>
    /// <b>Pipeline stages:</b>
    /// <list type="number">
    /// <item><description><b>Letterbox Preprocessing:</b> Scales the image while preserving aspect ratio and adds black borders to fit exactly 192x192.</description></item>
    /// <item><description><b>Color Space Conversion:</b> Converts OpenCV's BGR format to RGB format expected by the model.</description></item>
    /// <item><description><b>Unsafe Tensor Normalization:</b> Directly copies and normalizes byte pixel data into float values in range [0.0, 1.0] using C# pointers for zero-copy high performance.</description></item>
    /// <item><description><b>Model Inference:</b> Runs the ONNX graph to produce raw bounding box offsets and classification logits.</description></item>
    /// <item><description><b>Anchor Decoding:</b> Applies Sigmoid activation to logits and projects offsets back to pixel space.</description></item>
    /// <item><description><b>NMS Filtering:</b> Uses OpenCV DNN Non-Maximum Suppression to remove duplicate overlapping detections.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="image">The original camera frame (BGR Mat).</param>
    /// <returns>A list of detected palms with bounding boxes, keypoints, and confidence scores mapped to the original image dimensions.</returns>
    public List<PalmDetectionResult> Detect(Mat image)
    {
        int origW = image.Width;
        int origH = image.Height;

        // 1. Calculate uniform scale ratio to preserve aspect ratio without distortion
        float ratio = Math.Min((float)InputWidth / origW, (float)InputHeight / origH);
        int targetW = (int)Math.Round(origW * ratio);
        int targetH = (int)Math.Round(origH * ratio);

        // 2. Compute symmetric black border padding (Letterbox)
        int padW = InputWidth - targetW;
        int padH = InputHeight - targetH;
        int padLeft = padW / 2;
        int padTop = padH / 2;
        int padRight = padW - padLeft;
        int padBottom = padH - padTop;

        using Mat resized = new();
        Cv2.Resize(image, resized, new Size(targetW, targetH));

        using Mat padded = new();
        Cv2.CopyMakeBorder(resized, padded, padTop, padBottom, padLeft, padRight, BorderTypes.Constant, Scalar.All(0));

        using Mat rgb = new();
        Cv2.CvtColor(padded, rgb, ColorConversionCodes.BGR2RGB);

        // 3. Populate ONNX dense float tensor [Batch=1, Height=192, Width=192, Channels=3]
        DenseTensor<float> tensor = new([1, InputHeight, InputWidth, 3]);

        // Unsafe block: Uses raw memory pointers to read OpenCV image bytes directly.
        // This eliminates managed method-call overhead and bounds-checking, speeding up execution by ~50x (sub-millisecond conversion).
        unsafe
        {
            byte* ptr = (byte*)rgb.Data.ToPointer(); // Base pointer to the first pixel in unmanaged memory
            int step = (int)rgb.Step();              // Number of bytes per image row (stride including alignment padding)

            for (int y = 0; y < InputHeight; y++)
            {
                byte* row = ptr + y * step; // Direct pointer to the start of row 'y'
                for (int x = 0; x < InputWidth; x++)
                {
                    // Normalize [0..255] byte color values to [0.0f..1.0f] float values
                    tensor[0, y, x, 0] = row[x * 3 + 0] / 255.0f; // Red channel
                    tensor[0, y, x, 1] = row[x * 3 + 1] / 255.0f; // Green channel
                    tensor[0, y, x, 2] = row[x * 3 + 2] / 255.0f; // Blue channel
                }
            }
        }

        // 4. Execute ONNX inference
        List<NamedOnnxValue> inputs =
        [
            NamedOnnxValue.CreateFromTensor("input_1", tensor)
        ];

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs = _session.Run(inputs);

        float[] boxData = [];
        float[] scoreData = [];

        // Extract output tensors:
        // "Identity" contains bounding box deltas and 7 palm keypoint offsets [1, 2016, 18]
        // "Identity_1" contains raw classification logits [1, 2016, 1]
        foreach (NamedOnnxValue output in outputs)
        {
            if (output.Name == "Identity")
            {
                boxData = output.AsEnumerable<float>().ToArray();
            }
            else if (output.Name == "Identity_1")
            {
                scoreData = output.AsEnumerable<float>().ToArray();
            }
        }

        // Inverse scaling parameters to map coordinates from 192x192 back to original camera resolution
        float padBiasX = padLeft / ratio;
        float padBiasY = padTop / ratio;
        float scale = Math.Max(origW, origH);

        List<Rect2d> candidateBoxes = [];
        List<float> candidateScores = [];
        List<Point2f[]> candidateLandmarks = [];

        // 5. Decode anchor boxes and evaluate classification confidence
        for (int i = 0; i < _anchors.Length; i++)
        {
            float rawScore = scoreData[i];
            // Sigmoid activation: converts raw logit into probability score between 0.0 and 1.0
            float score = 1.0f / (1.0f + MathF.Exp(-rawScore));

            // Fast rejection: Skip anchors that do not meet the minimum confidence threshold
            if (score < _scoreThreshold) continue;

            float anchorX = _anchors[i][0];
            float anchorY = _anchors[i][1];

            // 18 values per anchor:
            // [0..3]: CenterX, CenterY, Width, Height offsets
            // [4..17]: 7 Keypoints (X, Y pairs for wrist, index MCP, middle MCP, ring MCP, pinky MCP, etc.)
            int boxOffset = i * 18;
            float cxDelta = boxData[boxOffset + 0] / InputWidth;
            float cyDelta = boxData[boxOffset + 1] / InputHeight;
            float wDelta = boxData[boxOffset + 2] / InputWidth;
            float hDelta = boxData[boxOffset + 3] / InputHeight;

            // Project bounding box coordinates to original frame pixel space
            float x1 = (cxDelta - wDelta / 2.0f + anchorX) * scale - padBiasX;
            float y1 = (cyDelta - hDelta / 2.0f + anchorY) * scale - padBiasY;
            float x2 = (cxDelta + wDelta / 2.0f + anchorX) * scale - padBiasX;
            float y2 = (cyDelta + hDelta / 2.0f + anchorY) * scale - padBiasY;

            // Project all 7 palm keypoints to original frame pixel space
            Point2f[] keypoints = new Point2f[7];
            for (int k = 0; k < 7; k++)
            {
                float lmxDelta = boxData[boxOffset + 4 + k * 2] / InputWidth;
                float lmyDelta = boxData[boxOffset + 4 + k * 2 + 1] / InputHeight;
                float kx = (lmxDelta + anchorX) * scale - padBiasX;
                float ky = (lmyDelta + anchorY) * scale - padBiasY;
                keypoints[k] = new Point2f(kx, ky);
            }

            candidateBoxes.Add(new Rect2d(x1, y1, Math.Max(1, x2 - x1), Math.Max(1, y2 - y1)));
            candidateScores.Add(score);
            candidateLandmarks.Add(keypoints);
        }

        List<PalmDetectionResult> results = [];
        if (candidateBoxes.Count == 0) return results;

        // 6. Non-Maximum Suppression (NMS): Merges/suppresses redundant overlapping bounding boxes
        CvDnn.NMSBoxes(candidateBoxes, candidateScores, _scoreThreshold, _nmsThreshold, out int[] indices);

        // 7. Collect and assemble final filtered detection results
        foreach (int idx in indices)
        {
            Rect2d r = candidateBoxes[idx];
            results.Add(new PalmDetectionResult
            {
                Box = new Rect2f((float)r.X, (float)r.Y, (float)r.Width, (float)r.Height),
                Keypoints = candidateLandmarks[idx],
                Score = candidateScores[idx]
            });
        }

        return results;
    }

    /// <summary>
    /// Releases the unmanaged ONNX Runtime session and frees allocated native memory resources.
    /// </summary>
    public void Dispose()
    {
        _session.Dispose();
        GC.SuppressFinalize(this);
    }
}
