using System;
using System.Collections.Generic;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace NEXA;

public class HandLandmarkResult
{
    public Point3f[] Landmarks { get; set; } = new Point3f[21];
    public Point2f[] Landmarks2D { get; set; } = new Point2f[21];
    public Point3f[] WorldLandmarks { get; set; } = new Point3f[21];
    public Rect2f BoundingBox { get; set; }
    public float Confidence { get; set; }
    public float HandednessScore { get; set; }
    public string Handedness => HandednessScore > 0.5f ? "Right" : "Left";
}

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

    public HandLandmarkEstimator(string modelPath, float confThreshold = 0.7f)
    {
        _confThreshold = confThreshold;
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
        };
        _session = new InferenceSession(modelPath, options);
    }

    public HandLandmarkResult? Estimate(Mat frame, PalmDetectionResult palm)
    {
        // 1. Initial crop and pad for rotation
        var (crop1, palmBox1, bias1) = CropAndPadFromPalm(frame, palm.Box, forRotation: true);
        using var crop1Mat = crop1;
        using var rgbMat = new Mat();
        Cv2.CvtColor(crop1Mat, rgbMat, ColorConversionCodes.BGR2RGB);

        var padBias = new Point2f(bias1.X, bias1.Y);

        // Adjust palm landmarks to crop1 coordinate frame
        var p1 = palm.Keypoints[0] - padBias; // Wrist / palm base
        var p2 = palm.Keypoints[2] - padBias; // Middle finger base

        // Compute rotation angle
        double radians = Math.PI / 2.0 - Math.Atan2(-(p2.Y - p1.Y), p2.X - p1.X);
        radians -= 2.0 * Math.PI * Math.Floor((radians + Math.PI) / (2.0 * Math.PI));
        double angleDeg = radians * 180.0 / Math.PI;

        var centerPalmBox = new Point2f(
            (palmBox1.X + palmBox1.Width / 2.0f) - padBias.X,
            (palmBox1.Y + palmBox1.Height / 2.0f) - padBias.Y
        );

        using var rotMat = Cv2.GetRotationMatrix2D(centerPalmBox, angleDeg, 1.0);
        using var rotatedImage = new Mat();
        Cv2.WarpAffine(rgbMat, rotatedImage, rotMat, new Size(rgbMat.Width, rgbMat.Height));

        double m00 = rotMat.At<double>(0, 0);
        double m01 = rotMat.At<double>(0, 1);
        double m02 = rotMat.At<double>(0, 2);
        double m10 = rotMat.At<double>(1, 0);
        double m11 = rotMat.At<double>(1, 1);
        double m12 = rotMat.At<double>(1, 2);

        // Rotate palm landmarks
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        for (int i = 0; i < palm.Keypoints.Length; i++)
        {
            var pt = palm.Keypoints[i] - padBias;
            float rx = (float)(m00 * pt.X + m01 * pt.Y + m02);
            float ry = (float)(m10 * pt.X + m11 * pt.Y + m12);
            if (rx < minX) minX = rx;
            if (ry < minY) minY = ry;
            if (rx > maxX) maxX = rx;
            if (ry > maxY) maxY = ry;
        }

        var rotPalmBox = new Rect2f(minX, minY, Math.Max(1f, maxX - minX), Math.Max(1f, maxY - minY));

        // 2. Crop and pad rotated palm for landmark network
        var (crop2, rotPalmBoxEnlarged, _) = CropAndPadFromPalm(rotatedImage, rotPalmBox, forRotation: false);
        using var crop2Mat = crop2;

        using var resizedCrop = new Mat();
        Cv2.Resize(crop2Mat, resizedCrop, new Size(InputSize, InputSize), 0, 0, InterpolationFlags.Area);

        // Prepare Tensor [1, 224, 224, 3]
        var tensor = new DenseTensor<float>(new[] { 1, InputSize, InputSize, 3 });
        unsafe
        {
            byte* ptr = (byte*)resizedCrop.Data.ToPointer();
            int step = (int)resizedCrop.Step();
            for (int y = 0; y < InputSize; y++)
            {
                byte* row = ptr + y * step;
                for (int x = 0; x < InputSize; x++)
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

        float[] lmRaw = Array.Empty<float>();
        float[] worldLmRaw = Array.Empty<float>();
        float score = 0f;
        float handedness = 0f;

        foreach (var outVal in outputs)
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

        if (score < _confThreshold) return null;

        // Post-process coordinates
        float rotBoxW = rotPalmBoxEnlarged.Width;
        float rotBoxH = rotPalmBoxEnlarged.Height;
        float scaleFactor = Math.Max(rotBoxW, rotBoxH) / InputSize;

        using var coordsRotMat = Cv2.GetRotationMatrix2D(new Point2f(0, 0), angleDeg, 1.0);
        double c00 = coordsRotMat.At<double>(0, 0);
        double c01 = coordsRotMat.At<double>(0, 1);
        double c10 = coordsRotMat.At<double>(1, 0);
        double c11 = coordsRotMat.At<double>(1, 1);

        // Inverse affine transform of rotation matrix
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

        var result = new HandLandmarkResult
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

            // Correct 2D un-rotation (equivalent to Python np.dot([rawX, rawY], coordsRotMat[:, :2]))
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

        // Bounding Box
        float bw = bMaxX - bMinX;
        float bh = bMaxY - bMinY;
        float bcx = bMinX + bw / 2.0f + HandBoxShift[0] * bw;
        float bcy = bMinY + bh / 2.0f + HandBoxShift[1] * bh;
        float nbw = bw * HandBoxEnlarge;
        float nbh = bh * HandBoxEnlarge;

        result.BoundingBox = new Rect2f(bcx - nbw / 2.0f, bcy - nbh / 2.0f, nbw, nbh);
        return result;
    }

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

        var cropped = new Mat(image, new Rect(cropX, cropY, cropW, cropH)).Clone();

        int sideLen = forRotation
            ? (int)Math.Ceiling(Math.Sqrt(cropW * cropW + cropH * cropH))
            : Math.Max(cropW, cropH);

        int padH = sideLen - cropH;
        int padW = sideLen - cropW;
        int left = padW / 2;
        int top = padH / 2;
        int right = padW - left;
        int bottom = padH - top;

        var padded = new Mat();
        Cv2.CopyMakeBorder(cropped, padded, top, bottom, left, right, BorderTypes.Constant, Scalar.All(0));
        cropped.Dispose();

        var finalBox = new Rect2f(cropX, cropY, cropW, cropH);
        var bias = new Point2f(cropX - left, cropY - top);

        return (padded, finalBox, bias);
    }

    public void Dispose()
    {
        _session.Dispose();
        GC.SuppressFinalize(this);
    }
}
