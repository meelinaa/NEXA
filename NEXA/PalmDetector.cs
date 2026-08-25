using System;
using System.Collections.Generic;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using OpenCvSharp.Dnn;

namespace NEXA;

public class PalmDetectionResult
{
    public Rect2f Box { get; set; }
    public Point2f[] Keypoints { get; set; } = new Point2f[7];
    public float Score { get; set; }
}

public class PalmDetector : IDisposable
{
    private readonly InferenceSession _session;
    private readonly float[][] _anchors;
    private readonly float _scoreThreshold;
    private readonly float _nmsThreshold;
    private const int InputWidth = 192;
    private const int InputHeight = 192;

    public PalmDetector(string modelPath, float scoreThreshold = 0.6f, float nmsThreshold = 0.3f)
    {
        _scoreThreshold = scoreThreshold;
        _nmsThreshold = nmsThreshold;

        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
        };
        _session = new InferenceSession(modelPath, options);
        _anchors = GenerateAnchors();
    }

    private static float[][] GenerateAnchors()
    {
        var anchors = new List<float[]>(2016);
        // Grid 24x24, stride 8, 2 anchors per cell
        for (int y = 0; y < 24; y++)
        {
            for (int x = 0; x < 24; x++)
            {
                float ax = (x + 0.5f) / 24.0f;
                float ay = (y + 0.5f) / 24.0f;
                anchors.Add(new[] { ax, ay });
                anchors.Add(new[] { ax, ay });
            }
        }
        // Grid 12x12, stride 16, 6 anchors per cell
        for (int y = 0; y < 12; y++)
        {
            for (int x = 0; x < 12; x++)
            {
                float ax = (x + 0.5f) / 12.0f;
                float ay = (y + 0.5f) / 12.0f;
                for (int i = 0; i < 6; i++)
                {
                    anchors.Add(new[] { ax, ay });
                }
            }
        }
        return anchors.ToArray();
    }

    public List<PalmDetectionResult> Detect(Mat image)
    {
        int origW = image.Width;
        int origH = image.Height;

        float ratio = Math.Min((float)InputWidth / origW, (float)InputHeight / origH);
        int targetW = (int)Math.Round(origW * ratio);
        int targetH = (int)Math.Round(origH * ratio);

        int padW = InputWidth - targetW;
        int padH = InputHeight - targetH;
        int padLeft = padW / 2;
        int padTop = padH / 2;
        int padRight = padW - padLeft;
        int padBottom = padH - padTop;

        using var resized = new Mat();
        Cv2.Resize(image, resized, new Size(targetW, targetH));

        using var padded = new Mat();
        Cv2.CopyMakeBorder(resized, padded, padTop, padBottom, padLeft, padRight, BorderTypes.Constant, Scalar.All(0));

        using var rgb = new Mat();
        Cv2.CvtColor(padded, rgb, ColorConversionCodes.BGR2RGB);

        // Convert to float tensor [1, 192, 192, 3]
        var tensor = new DenseTensor<float>(new[] { 1, InputHeight, InputWidth, 3 });
        unsafe
        {
            byte* ptr = (byte*)rgb.Data.ToPointer();
            int step = (int)rgb.Step();
            for (int y = 0; y < InputHeight; y++)
            {
                byte* row = ptr + y * step;
                for (int x = 0; x < InputWidth; x++)
                {
                    tensor[0, y, x, 0] = row[x * 3 + 0] / 255.0f;
                    tensor[0, y, x, 1] = row[x * 3 + 1] / 255.0f;
                    tensor[0, y, x, 2] = row[x * 3 + 2] / 255.0f;
                }
            }
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_1", tensor)
        };

        using var outputs = _session.Run(inputs);

        float[] boxData = Array.Empty<float>();
        float[] scoreData = Array.Empty<float>();

        foreach (var output in outputs)
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

        float padBiasX = padLeft / ratio;
        float padBiasY = padTop / ratio;
        float scale = Math.Max(origW, origH);

        var candidateBoxes = new List<Rect2d>();
        var candidateScores = new List<float>();
        var candidateLandmarks = new List<Point2f[]>();

        for (int i = 0; i < _anchors.Length; i++)
        {
            float rawScore = scoreData[i];
            float score = 1.0f / (1.0f + MathF.Exp(-rawScore));

            if (score < _scoreThreshold) continue;

            float anchorX = _anchors[i][0];
            float anchorY = _anchors[i][1];

            int boxOffset = i * 18;
            float cxDelta = boxData[boxOffset + 0] / InputWidth;
            float cyDelta = boxData[boxOffset + 1] / InputHeight;
            float wDelta = boxData[boxOffset + 2] / InputWidth;
            float hDelta = boxData[boxOffset + 3] / InputHeight;

            float x1 = (cxDelta - wDelta / 2.0f + anchorX) * scale - padBiasX;
            float y1 = (cyDelta - hDelta / 2.0f + anchorY) * scale - padBiasY;
            float x2 = (cxDelta + wDelta / 2.0f + anchorX) * scale - padBiasX;
            float y2 = (cyDelta + hDelta / 2.0f + anchorY) * scale - padBiasY;

            var keypoints = new Point2f[7];
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

        var results = new List<PalmDetectionResult>();
        if (candidateBoxes.Count == 0) return results;

        CvDnn.NMSBoxes(candidateBoxes, candidateScores, _scoreThreshold, _nmsThreshold, out int[] indices);

        foreach (int idx in indices)
        {
            var r = candidateBoxes[idx];
            results.Add(new PalmDetectionResult
            {
                Box = new Rect2f((float)r.X, (float)r.Y, (float)r.Width, (float)r.Height),
                Keypoints = candidateLandmarks[idx],
                Score = candidateScores[idx]
            });
        }

        return results;
    }

    public void Dispose()
    {
        _session.Dispose();
        GC.SuppressFinalize(this);
    }
}
