using System;
using System.Collections.Generic;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NEXA.Detector;
using OpenCvSharp;

namespace NEXA.Face;

/// <summary>
/// Stage 2 Face Landmark Estimator model based on Google MediaPipe FaceMesh running via ONNX Runtime.
/// <para>
/// <b>What it is:</b> A high-precision deep learning regressor that estimates 468 3D facial landmarks from an isolated face crop.
/// </para>
/// <para>
/// <b>What it does:</b> Takes the detected face box from BlazeFace, aligns rotation according to eye angle, warps the patch to 192x192,
/// executes ONNX inference, and maps all 468 points back to full camera frame coordinates via inverse affine transformation.
/// </para>
/// </summary>
public class FaceLandmarkEstimator : IDisposable
{
    private readonly InferenceSession _session;
    private readonly float _confThreshold;
    private const int InputSize = 192;

    /// <summary>
    /// Initializes a new instance of the <see cref="FaceLandmarkEstimator"/> class.
    /// </summary>
    /// <param name="modelPath">File path to face_mesh.onnx.</param>
    /// <param name="confThreshold">Minimum confidence score threshold (default: 0.5f).</param>
    public FaceLandmarkEstimator(string modelPath, float confThreshold = 0.5f)
    {
        _confThreshold = confThreshold;

        SessionOptions options = new()
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
        };

        _session = new InferenceSession(modelPath, options);
    }

    /// <summary>
    /// Estimates 468 facial landmark coordinates from the provided image and face detection result.
    /// </summary>
    /// <param name="image">The camera BGR frame.</param>
    /// <param name="face">The Stage 1 BlazeFace detection result.</param>
    /// <param name="confidence">Output detection confidence score.</param>
    /// <returns>An array of 468 2D landmark points in original image coordinates if successful; otherwise, <c>null</c>.</returns>
    public Point2f[]? Estimate(Mat image, BlazeFaceDetectionResult face, out float confidence)
    {
        confidence = 0f;
        if (image == null || image.Empty() || face == null)
            return null;

        // 1. Calculate face center, scale, and rotation angle from eye keypoints
        Point2f rightEye = face.Keypoints[0];
        Point2f leftEye = face.Keypoints[1];

        float deltaX = leftEye.X - rightEye.X;
        float deltaY = leftEye.Y - rightEye.Y;
        double angleRad = Math.Atan2(deltaY, deltaX);
        double angleDeg = angleRad * (180.0 / Math.PI);

        float centerX = (float)(face.Box.X + face.Box.Width / 2.0);
        float centerY = (float)(face.Box.Y + face.Box.Height / 2.0);

        // Enlarge crop to encompass forehead and chin
        float boxSize = (float)Math.Max(face.Box.Width, face.Box.Height) * 1.50f;
        if (boxSize < 10f)
            return null;

        // 2. Compute Affine Transformation Matrix to crop & rotate 192x192 patch
        Point2f center = new(centerX, centerY);
        using Mat rotMat = Cv2.GetRotationMatrix2D(center, angleDeg, InputSize / (double)boxSize);

        // Adjust translation so face center maps to center of 192x192 patch
        rotMat.Set(0, 2, rotMat.At<double>(0, 2) - centerX + InputSize / 2.0);
        rotMat.Set(1, 2, rotMat.At<double>(1, 2) - centerY + InputSize / 2.0);

        using Mat warped = new();
        Cv2.WarpAffine(image, warped, rotMat, new Size(InputSize, InputSize), InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(0));

        using Mat rgb = new();
        Cv2.CvtColor(warped, rgb, ColorConversionCodes.BGR2RGB);

        // 3. Populate NCHW Tensor [1, 3, 192, 192]
        DenseTensor<float> tensor = new([1, 3, InputSize, InputSize]);

        unsafe
        {
            byte* ptr = (byte*)rgb.Data.ToPointer();
            int step = (int)rgb.Step();

            for (int y = 0; y < InputSize; y++)
            {
                byte* row = ptr + y * step;
                for (int x = 0; x < InputSize; x++)
                {
                    tensor[0, 0, y, x] = row[x * 3 + 0] / 255.0f;
                    tensor[0, 1, y, x] = row[x * 3 + 1] / 255.0f;
                    tensor[0, 2, y, x] = row[x * 3 + 2] / 255.0f;
                }
            }
        }

        // 4. Run ONNX Inference
        List<NamedOnnxValue> inputs =
        [
            NamedOnnxValue.CreateFromTensor("input", tensor)
        ];

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs = _session.Run(inputs);

        float[] rawLandmarks = [];
        float score = 0f;

        foreach (NamedOnnxValue output in outputs)
        {
            if (output.Name == "landmarks")
            {
                rawLandmarks = output.AsEnumerable<float>().ToArray();
            }
            else if (output.Name == "score")
            {
                float[] scoreArr = output.AsEnumerable<float>().ToArray();
                if (scoreArr.Length > 0)
                {
                    score = scoreArr[0];
                }
            }
        }

        confidence = score;
        if (score < _confThreshold || rawLandmarks.Length < 468 * 3)
            return null;

        // 5. Invert Affine Transform to unproject landmarks back to original camera pixel coordinates
        using Mat invRotMat = new();
        Cv2.InvertAffineTransform(rotMat, invRotMat);

        double m00 = invRotMat.At<double>(0, 0);
        double m01 = invRotMat.At<double>(0, 1);
        double m02 = invRotMat.At<double>(0, 2);
        double m10 = invRotMat.At<double>(1, 0);
        double m11 = invRotMat.At<double>(1, 1);
        double m12 = invRotMat.At<double>(1, 2);

        Point2f[] landmarks = new Point2f[468];
        for (int i = 0; i < 468; i++)
        {
            float cropX = rawLandmarks[i * 3 + 0];
            float cropY = rawLandmarks[i * 3 + 1];

            float unwarpedX = (float)(m00 * cropX + m01 * cropY + m02);
            float unwarpedY = (float)(m10 * cropX + m11 * cropY + m12);

            landmarks[i] = new Point2f(unwarpedX, unwarpedY);
        }

        return landmarks;
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
