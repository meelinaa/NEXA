using System;
using System.Collections.Generic;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using OpenCvSharp.Dnn;

namespace NEXA.Detector;

/// <summary>
/// Stage 1 Face Detector based on the Google MediaPipe BlazeFace architecture running via ONNX Runtime.
/// <para>
/// <b>What it is:</b> A lightweight Single-Shot Detector (SSD) neural network operating at 128x128 pixels to detect human faces in real time.
/// </para>
/// <para>
/// <b>What it does:</b> Takes the camera frame, letterboxes it to 128x128 pixels, runs ONNX inference over 896 anchor positions,
/// evaluates classification confidence with Sigmoid activation, and performs Non-Maximum Suppression (NMS).
/// </para>
/// </summary>
public class BlazeFaceDetector : IDisposable
{
    private readonly InferenceSession _session;
    private readonly float[][] _anchors;
    private readonly float _scoreThreshold;
    private readonly float _nmsThreshold;

    private const int InputWidth = 128;
    private const int InputHeight = 128;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlazeFaceDetector"/> class.
    /// </summary>
    /// <param name="modelPath">File path to blazeface.onnx.</param>
    /// <param name="scoreThreshold">Minimum detection confidence threshold (default: 0.6f).</param>
    /// <param name="nmsThreshold">Non-Maximum Suppression IoU threshold (default: 0.3f).</param>
    public BlazeFaceDetector(string modelPath, float scoreThreshold = 0.6f, float nmsThreshold = 0.3f)
    {
        _scoreThreshold = scoreThreshold;
        _nmsThreshold = nmsThreshold;

        SessionOptions options = new()
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
        };

        _session = new InferenceSession(modelPath, options);
        _anchors = GenerateAnchors();
    }

    /// <summary>
    /// Generates the standard 896 SSD anchor grid for BlazeFace (16x16 grid with 2 anchors + 8x8 grid with 6 anchors).
    /// </summary>
    private static float[][] GenerateAnchors()
    {
        List<float[]> anchors = new(896);

        // Grid 1: 16x16 (stride 8) - 2 anchors per cell = 512
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                float ax = (x + 0.5f) / 16.0f;
                float ay = (y + 0.5f) / 16.0f;
                anchors.Add([ax, ay]);
                anchors.Add([ax, ay]);
            }
        }

        // Grid 2: 8x8 (stride 16) - 6 anchors per cell = 384
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                float ax = (x + 0.5f) / 8.0f;
                float ay = (y + 0.5f) / 8.0f;
                for (int i = 0; i < 6; i++)
                {
                    anchors.Add([ax, ay]);
                }
            }
        }

        return anchors.ToArray();
    }

    /// <summary>
    /// Detects visible human faces within the provided BGR image frame.
    /// </summary>
    /// <param name="image">The camera BGR frame.</param>
    /// <returns>A list of detected faces with bounding boxes and 6 facial keypoints.</returns>
    public List<BlazeFaceDetectionResult> Detect(Mat image)
    {
        if (image == null || image.Empty())
            return [];

        int origW = image.Width;
        int origH = image.Height;

        // 1. Calculate aspect-preserving letterbox scale
        float ratio = Math.Min((float)InputWidth / origW, (float)InputHeight / origH);
        int targetW = (int)Math.Round(origW * ratio);
        int targetH = (int)Math.Round(origH * ratio);

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

        // 2. Populate NCHW Tensor [1, 3, 128, 128]
        DenseTensor<float> tensor = new([1, 3, InputHeight, InputWidth]);

        unsafe
        {
            byte* ptr = (byte*)rgb.Data.ToPointer();
            int step = (int)rgb.Step();

            for (int y = 0; y < InputHeight; y++)
            {
                byte* row = ptr + y * step;
                for (int x = 0; x < InputWidth; x++)
                {
                    tensor[0, 0, y, x] = row[x * 3 + 0] / 255.0f; // R
                    tensor[0, 1, y, x] = row[x * 3 + 1] / 255.0f; // G
                    tensor[0, 2, y, x] = row[x * 3 + 2] / 255.0f; // B
                }
            }
        }

        // 3. Execute ONNX inference
        List<NamedOnnxValue> inputs =
        [
            NamedOnnxValue.CreateFromTensor("input", tensor)
        ];

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs = _session.Run(inputs);

        float[] boxData = [];
        float[] scoreData = [];

        foreach (NamedOnnxValue output in outputs)
        {
            if (output.Name == "regressors")
            {
                boxData = output.AsEnumerable<float>().ToArray();
            }
            else if (output.Name == "scores")
            {
                scoreData = output.AsEnumerable<float>().ToArray();
            }
        }

        float padBiasX = padLeft / ratio;
        float padBiasY = padTop / ratio;
        float scale = Math.Max(origW, origH);

        List<Rect2d> candidateBoxes = [];
        List<float> candidateScores = [];
        List<Point2f[]> candidateKeypoints = [];

        // 4. Decode 896 anchors
        for (int i = 0; i < _anchors.Length; i++)
        {
            float rawScore = scoreData[i];
            float score = 1.0f / (1.0f + MathF.Exp(-rawScore));

            if (score < _scoreThreshold) continue;

            float anchorX = _anchors[i][0];
            float anchorY = _anchors[i][1];

            // 16 values per anchor: [cx, cy, w, h, kp0_x, kp0_y, ... kp5_x, kp5_y]
            int offset = i * 16;
            float cxDelta = boxData[offset + 0] / InputWidth;
            float cyDelta = boxData[offset + 1] / InputHeight;
            float wDelta = boxData[offset + 2] / InputWidth;
            float hDelta = boxData[offset + 3] / InputHeight;

            float x1 = (cxDelta - wDelta / 2.0f + anchorX) * scale - padBiasX;
            float y1 = (cyDelta - hDelta / 2.0f + anchorY) * scale - padBiasY;
            float w = wDelta * scale;
            float h = hDelta * scale;

            Point2f[] kps = new Point2f[6];
            for (int k = 0; k < 6; k++)
            {
                float kpx = (boxData[offset + 4 + k * 2 + 0] / InputWidth + anchorX) * scale - padBiasX;
                float kpy = (boxData[offset + 4 + k * 2 + 1] / InputHeight + anchorY) * scale - padBiasY;
                kps[k] = new Point2f(kpx, kpy);
            }

            candidateBoxes.Add(new Rect2d(x1, y1, w, h));
            candidateScores.Add(score);
            candidateKeypoints.Add(kps);
        }

        if (candidateBoxes.Count == 0)
            return [];

        // 5. Non-Maximum Suppression (NMS)
        CvDnn.NMSBoxes(candidateBoxes, candidateScores, _scoreThreshold, _nmsThreshold, out int[] indices);

        List<BlazeFaceDetectionResult> results = new(indices.Length);
        for (int i = 0; i < indices.Length; i++)
        {
            int idx = indices[i];
            results.Add(new BlazeFaceDetectionResult
            {
                Box = candidateBoxes[idx],
                Score = candidateScores[idx],
                Keypoints = candidateKeypoints[idx]
            });
        }

        return results;
    }

    /// <summary>
    /// Disposes ONNX session resources.
    /// </summary>
    public void Dispose()
    {
        _session?.Dispose();
        GC.SuppressFinalize(this);
    }
}
