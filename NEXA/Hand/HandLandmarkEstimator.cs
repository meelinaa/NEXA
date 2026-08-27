using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NEXA.Detector;
using OpenCvSharp;

namespace NEXA.Hand;

/// <summary>
/// Stage 2 Hand Landmark Estimator model based on Google MediaPipe HandPose architecture running via ONNX Runtime.
/// <para>
/// <b>What it is:</b> A high-precision deep learning regressor that estimates 21 anatomical 3D finger joints from an isolated hand crop.
/// </para>
/// </summary>
public class HandLandmarkEstimator : IDisposable
{
    private readonly InferenceSession _session;
    private readonly float _confThreshold;

    private const int InputSize = 224;

    private static readonly float[] PalmBoxPreShift = { 0f, 0f };
    private const float PalmBoxPreEnlarge = 4.0f;
    private static readonly float[] PalmBoxShift = { 0f, -0.4f };
    private const float PalmBoxEnlarge = 3.0f;
    private static readonly float[] HandBoxShift = { 0f, -0.1f };
    private const float HandBoxEnlarge = 1.65f;

    // Zero-allocation reusable OpenCV matrices and tensor buffer
    private readonly Mat _rgbMat = new();
    private readonly Mat _rotatedImage = new();
    private readonly Mat _resizedCrop = new();
    private readonly DenseTensor<float> _inputTensor = new([1, InputSize, InputSize, 3]);

    /// <summary>
    /// Initializes a new instance of the <see cref="HandLandmarkEstimator"/> class, configuring the ONNX inference session.
    /// </summary>
    /// <param name="modelPath">The file path to the handpose_estimation.onnx model.</param>
    /// <param name="confThreshold">Minimum presence confidence threshold (default: 0.7f).</param>
    /// <param name="enableGpu">Whether to attempt DirectML GPU hardware acceleration with automatic CPU fallback.</param>
    public HandLandmarkEstimator(string modelPath, float confThreshold = 0.7f, bool enableGpu = true)
    {
        _confThreshold = confThreshold;
        SessionOptions options = new() 
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
        };

        if (enableGpu)
        {
            try
            {
                options.AppendExecutionProvider_DML(0);
            }
            catch
            {
                // Graceful fallback to CPU Execution Provider
            }
        }

        _session = new InferenceSession(modelPath, options);
    }

    /// <summary>
    /// Estimates 21 3D finger landmarks for a given palm detection.
    /// </summary>
    /// <param name="frame">The raw original camera frame (BGR Mat).</param>
    /// <param name="palm">The Stage 1 palm detection result containing bounding box and alignment keypoints.</param>
    /// <returns>A <see cref="HandLandmarkResult"/> with 21 joint landmarks, or <c>null</c> if confidence falls below threshold.</returns>
    public HandLandmarkResult? Estimate(Mat frame, PalmDetectionResult palm)
    {
        // 1. Initial crop and square padding to allow full 360-degree in-plane rotation without clipping corners
        (Mat crop1, Rect2f palmBox1, Point2f bias1) = CropAndPadFromPalm(frame, palm.Box, forRotation: true);
        using Mat? crop1Mat = crop1;
        Cv2.CvtColor(crop1Mat, _rgbMat, ColorConversionCodes.BGR2RGB);

        Point2f padBias = new(bias1.X, bias1.Y);

        // Adjust palm landmarks to crop1 local coordinate system
        Point2f p1 = palm.Keypoints[0] - padBias; // Wrist center
        Point2f p2 = palm.Keypoints[2] - padBias; // Middle finger MCP knuckle

        // Compute rotation angle to orient hand vertically upright (-90 degrees orientation)
        double radians = Math.PI / 2.0 - Math.Atan2(-(p2.Y - p1.Y), p2.X - p1.X);
        radians -= 2.0 * Math.PI * Math.Floor((radians + Math.PI) / (2.0 * Math.PI));
        double angleDeg = radians * 180.0 / Math.PI;

        Point2f centerPalmBox = new(
            (palmBox1.X + palmBox1.Width / 2.0f) - padBias.X,
            (palmBox1.Y + palmBox1.Height / 2.0f) - padBias.Y
        );

        // Compute 2D affine rotation matrix around palm center
        using Mat? rotMat = Cv2.GetRotationMatrix2D(centerPalmBox, angleDeg, 1.0);
        Cv2.WarpAffine(_rgbMat, _rotatedImage, rotMat, new Size(_rgbMat.Width, _rgbMat.Height));

        double m00 = rotMat.At<double>(0, 0);
        double m01 = rotMat.At<double>(0, 1);
        double m02 = rotMat.At<double>(0, 2);
        double m10 = rotMat.At<double>(1, 0);
        double m11 = rotMat.At<double>(1, 1);
        double m12 = rotMat.At<double>(1, 2);

        // Rotate palm keypoints to compute the rotated bounding box
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        for (int i = 0; i < palm.Keypoints.Length; i++)
        {
            Point2f pt = palm.Keypoints[i] - padBias;
            float rx = (float)(m00 * pt.X + m01 * pt.Y + m02);
            float ry = (float)(m10 * pt.X + m11 * pt.Y + m12);
            if (rx < minX) minX = rx;
            if (ry < minY) minY = ry;
            if (rx > maxX) maxX = rx;
            if (ry > maxY) maxY = ry;
        }

        Rect2f rotPalmBox = new(minX, minY, Math.Max(1f, maxX - minX), Math.Max(1f, maxY - minY));

        // 2. Crop the upright rotated hand and resize to 224x224 for neural network input
        (Mat crop2, Rect2f rotPalmBoxEnlarged, _) = CropAndPadFromPalm(_rotatedImage, rotPalmBox, forRotation: false);
        using Mat? crop2Mat = crop2;
        Cv2.Resize(crop2Mat, _resizedCrop, new Size(InputSize, InputSize), 0, 0, InterpolationFlags.Area);

        // Prepare preallocated ONNX dense tensor [Batch=1, Height=224, Width=224, Channels=3]
        unsafe
        {
            byte* ptr = (byte*)_resizedCrop.Data.ToPointer();
            int step = (int)_resizedCrop.Step();
            for (int y = 0; y < InputSize; y++)
            {
                byte* row = ptr + y * step;
                for (int x = 0; x < InputSize; x++)
                {
                    _inputTensor[0, y, x, 0] = row[x * 3 + 0] / 255.0f;
                    _inputTensor[0, y, x, 1] = row[x * 3 + 1] / 255.0f;
                    _inputTensor[0, y, x, 2] = row[x * 3 + 2] / 255.0f;
                }
            }
        }

        List<NamedOnnxValue> inputs =
        [
            NamedOnnxValue.CreateFromTensor("input_1", _inputTensor)
        ];

        // 3. Execute ONNX landmark inference
        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs = _session.Run(inputs);

        float[] lmRaw = [];
        float[] worldLmRaw = [];
        float score = 0f;
        float handedness = 0f;

        foreach (NamedOnnxValue outVal in outputs)
        {
            if (outVal.Name == "Identity")
            {
                lmRaw = outVal.AsEnumerable<float>().ToArray();
            }
            else if (outVal.Name == "Identity_1")
            {
                score = outVal.AsEnumerable<float>().First();
            }
            else if (outVal.Name == "Identity_2")
            {
                handedness = outVal.AsEnumerable<float>().First();
            }
            else if (outVal.Name == "Identity_3")
            {
                worldLmRaw = outVal.AsEnumerable<float>().ToArray();
            }
        }

        // Reject low-confidence hand detections
        if (score < _confThreshold) return null;

        // 4. Post-process coordinates: Invert affine rotation and project back to original frame
        float rotBoxW = rotPalmBoxEnlarged.Width;
        float rotBoxH = rotPalmBoxEnlarged.Height;
        float scaleFactor = Math.Max(rotBoxW, rotBoxH) / InputSize;

        using Mat? coordsRotMat = Cv2.GetRotationMatrix2D(new Point2f(0, 0), angleDeg, 1.0);
        double c00 = coordsRotMat.At<double>(0, 0);
        double c01 = coordsRotMat.At<double>(0, 1);
        double c10 = coordsRotMat.At<double>(1, 0);
        double c11 = coordsRotMat.At<double>(1, 1);

        // Inverse affine transformation parameters
        double invM00 = m00;
        double invM01 = m10;
        double invM10 = m01;
        double invM11 = m11;
        double invT0 = -(invM00 * m02 + invM01 * m12);
        double invT1 = -(invM10 * m02 + invM11 * m12);

        float rotBoxCenterX = rotPalmBoxEnlarged.X + rotPalmBoxEnlarged.Width / 2.0f;
        float rotBoxCenterY = rotPalmBoxEnlarged.Y + rotPalmBoxEnlarged.Height / 2.0f;

        float origCenterX = (float)(invM00 * rotBoxCenterX + invM01 * rotBoxCenterY + invT0);
        float origCenterY = (float)(invM10 * rotBoxCenterX + invM11 * rotBoxCenterY + invT1);

        HandLandmarkResult result = new()
        {
            Confidence = score,
            HandednessScore = handedness
        };

        float bMinX = float.MaxValue, bMinY = float.MaxValue;
        float bMaxX = float.MinValue, bMaxY = float.MinValue;

        for (int i = 0; i < 21; i++)
        {
            float rawX = (lmRaw[i * 3 + 0] - InputSize / 2.0f) * scaleFactor;
            float rawY = (lmRaw[i * 3 + 1] - InputSize / 2.0f) * scaleFactor;
            float rawZ = lmRaw[i * 3 + 2] * scaleFactor;

            // Apply inverse 2D rotation
            float rotX = (float)(c00 * rawX + c10 * rawY);
            float rotY = (float)(c01 * rawX + c11 * rawY);

            float finalX = rotX + origCenterX + padBias.X;
            float finalY = rotY + origCenterY + padBias.Y;

            result.Landmarks[i] = new Point3f(finalX, finalY, rawZ);
            result.Landmarks2D[i] = new Point2f(finalX, finalY);

            if (finalX < bMinX) bMinX = finalX;
            if (finalY < bMinY) bMinY = finalY;
            if (finalX > bMaxX) bMaxX = finalX;
            if (finalY > bMaxY) bMaxY = finalY;

            if (worldLmRaw.Length >= 63)
            {
                float wx = worldLmRaw[i * 3 + 0];
                float wy = worldLmRaw[i * 3 + 1];
                float wz = worldLmRaw[i * 3 + 2];
                float rwx = (float)(c00 * wx + c10 * wy);
                float rwy = (float)(c01 * wx + c11 * wy);
                result.WorldLandmarks[i] = new Point3f(rwx, rwy, wz);
            }
        }

        // Compute enlarged hand bounding box for visual tracking feedback
        float bw = bMaxX - bMinX;
        float bh = bMaxY - bMinY;
        float bcx = bMinX + bw / 2.0f + HandBoxShift[0] * bw;
        float bcy = bMinY + bh / 2.0f + HandBoxShift[1] * bh;
        float nbw = bw * HandBoxEnlarge;
        float nbh = bh * HandBoxEnlarge;

        result.BoundingBox = new Rect2f(bcx - nbw / 2.0f, bcy - nbh / 2.0f, nbw, nbh);
        return result;
    }

    /// <summary>
    /// Helper method to crop, expand, and pad a sub-region around a palm box.
    /// </summary>
    private static (Mat cropped, Rect2f bbox, Point2f bias) CropAndPadFromPalm(Mat image, Rect2f palmBox, bool forRotation)
    {
        float[] shiftVector = forRotation ? PalmBoxPreShift : PalmBoxShift;
        float enlargeScale = forRotation ? PalmBoxPreEnlarge : PalmBoxEnlarge;

        float w = palmBox.Width;
        float h = palmBox.Height;
        float cx = palmBox.X + w / 2.0f + shiftVector[0] * w;
        float cy = palmBox.Y + h / 2.0f + shiftVector[1] * h;

        float newHalfW = (w * enlargeScale) / 2.0f;
        float newHalfH = (h * enlargeScale) / 2.0f;

        float x1 = Math.Clamp(cx - newHalfW, 0, image.Width);
        float y1 = Math.Clamp(cy - newHalfH, 0, image.Height);
        float x2 = Math.Clamp(cx + newHalfW, 0, image.Width);
        float y2 = Math.Clamp(cy + newHalfH, 0, image.Height);

        int cropX = (int)x1;
        int cropY = (int)y1;
        int cropW = Math.Max(1, (int)(x2 - x1));
        int cropH = Math.Max(1, (int)(y2 - y1));

        if (cropX + cropW > image.Width) cropW = image.Width - cropX;
        if (cropY + cropH > image.Height) cropH = image.Height - cropY;

        Mat cropped = new Mat(image, new Rect(cropX, cropY, cropW, cropH)).Clone();

        int sideLen = forRotation
            ? (int)Math.Ceiling(Math.Sqrt(cropW * cropW + cropH * cropH))
            : Math.Max(cropW, cropH);

        int padH = sideLen - cropH;
        int padW = sideLen - cropW;
        int left = padW / 2;
        int top = padH / 2;
        int right = padW - left;
        int bottom = padH - top;

        Mat padded = new();
        Cv2.CopyMakeBorder(cropped, padded, top, bottom, left, right, BorderTypes.Constant, Scalar.All(0));
        cropped.Dispose();

        Rect2f finalBox = new(cropX, cropY, cropW, cropH);
        Point2f bias = new(cropX - left, cropY - top);

        return (padded, finalBox, bias);
    }

    /// <summary>
    /// Disposes the unmanaged ONNX Runtime session and native memory buffers.
    /// </summary>
    public void Dispose()
    {
        _rgbMat.Dispose();
        _rotatedImage.Dispose();
        _resizedCrop.Dispose();
        _session.Dispose();
        GC.SuppressFinalize(this);
    }
}
