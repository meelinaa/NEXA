using System;
using System.Collections.Generic;
using OpenCvSharp;
using OpenCvSharp.Dnn;

namespace NEXA.Detector;

/// <summary>
/// Domain decoder for BlazePalm ONNX output tensors, performing Sigmoid thresholding, anchor box delta reconstruction, and Non-Maximum Suppression (NMS).
/// <para>
/// <b>What it is:</b> Post-processor converting raw neural network regression arrays into calibrated palm detections in original image pixel space.
/// </para>
/// </summary>
public static class PalmBoxDecoder
{
    /// <summary>
    /// Decodes raw bounding box and classification tensors against precalculated anchors and applies Non-Maximum Suppression.
    /// </summary>
    /// <param name="boxData">Raw bounding box and keypoint regression tensor array [2016 * 18].</param>
    /// <param name="scoreData">Raw classification logit tensor array [2016].</param>
    /// <param name="anchors">Precalculated normalized anchor center points [ax, ay].</param>
    /// <param name="scoreThreshold">Minimum confidence threshold (0.0 to 1.0) required to accept a candidate.</param>
    /// <param name="nmsThreshold">Intersection-over-Union (IoU) threshold for Non-Maximum Suppression.</param>
    /// <param name="scale">Scale factor of the letterbox transformation.</param>
    /// <param name="padBiasX">Horizontal padding offset in original pixel coordinates.</param>
    /// <param name="padBiasY">Vertical padding offset in original pixel coordinates.</param>
    /// <param name="inputWidth">Model input tensor width (192).</param>
    /// <param name="inputHeight">Model input tensor height (192).</param>
    /// <returns>A list of filtered and decoded <see cref="PalmDetectionResult"/> objects.</returns>
    public static List<PalmDetectionResult> Decode(
        float[] boxData,
        float[] scoreData,
        float[][] anchors,
        float scoreThreshold,
        float nmsThreshold,
        float scale,
        float padBiasX,
        float padBiasY,
        int inputWidth,
        int inputHeight)
    {
        List<Rect2d> candidateBoxes = new();
        List<float> candidateScores = new();
        List<Point2f[]> candidateLandmarks = new();

        for (int i = 0; i < anchors.Length; i++)
        {
            float rawScore = scoreData[i];
            float score = 1.0f / (1.0f + MathF.Exp(-rawScore));

            if (score < scoreThreshold)
            {
                continue;
            }

            float anchorX = anchors[i][0];
            float anchorY = anchors[i][1];

            int boxOffset = i * 18;
            float cxDelta = boxData[boxOffset + 0] / inputWidth;
            float cyDelta = boxData[boxOffset + 1] / inputHeight;
            float wDelta = boxData[boxOffset + 2] / inputWidth;
            float hDelta = boxData[boxOffset + 3] / inputHeight;

            float x1 = (cxDelta - wDelta / 2.0f + anchorX) * scale - padBiasX;
            float y1 = (cyDelta - hDelta / 2.0f + anchorY) * scale - padBiasY;
            float x2 = (cxDelta + wDelta / 2.0f + anchorX) * scale - padBiasX;
            float y2 = (cyDelta + hDelta / 2.0f + anchorY) * scale - padBiasY;

            Point2f[] keypoints = new Point2f[7];
            for (int k = 0; k < 7; k++)
            {
                float lmxDelta = boxData[boxOffset + 4 + k * 2] / inputWidth;
                float lmyDelta = boxData[boxOffset + 4 + k * 2 + 1] / inputHeight;
                float kx = (lmxDelta + anchorX) * scale - padBiasX;
                float ky = (lmyDelta + anchorY) * scale - padBiasY;
                keypoints[k] = new Point2f(kx, ky);
            }

            candidateBoxes.Add(new Rect2d(x1, y1, Math.Max(1, x2 - x1), Math.Max(1, y2 - y1)));
            candidateScores.Add(score);
            candidateLandmarks.Add(keypoints);
        }

        List<PalmDetectionResult> results = new();
        if (candidateBoxes.Count == 0)
        {
            return results;
        }

        // Non-Maximum Suppression: Suppresses duplicate overlapping detections
        CvDnn.NMSBoxes(candidateBoxes, candidateScores, scoreThreshold, nmsThreshold, out int[] indices);

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
}
