using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace NEXA.Detector;

/// <summary>
/// Stage 1 Object Detector based on the Google MediaPipe BlazePalm architecture running via ONNX Runtime.
/// <para>
/// <b>What it is:</b> A Single-Shot Detector (SSD) neural network optimized for mobile and real-time palm detection.
/// </para>
/// <para>
/// <b>What it does:</b> Takes a raw camera frame, performs aspect-ratio preserving letterbox scaling to 192x192 pixels,
/// runs ONNX neural network inference, and delegates anchor decoding and Non-Maximum Suppression to <see cref="PalmBoxDecoder"/>.
/// </para>
/// </summary>
public class PalmDetector : IDisposable
{
    private readonly InferenceSession _session;
    private readonly float[][] _anchors;
    private readonly float _scoreThreshold;
    private readonly float _nmsThreshold;

    private const int InputWidth = 192;
    private const int InputHeight = 192;

    // Zero-allocation reusable OpenCV matrices and tensor buffer
    private readonly Mat _resizedMat = new();
    private readonly Mat _paddedMat = new();
    private readonly Mat _rgbMat = new();
    private readonly DenseTensor<float> _inputTensor = new([1, InputHeight, InputWidth, 3]);

    /// <summary>
    /// Initializes a new instance of the <see cref="PalmDetector"/> class, loading the ONNX model and generating anchor boxes.
    /// </summary>
    /// <param name="modelPath">The file path to the palm_detection.onnx model.</param>
    /// <param name="scoreThreshold">Minimum confidence score required for candidate boxes (default: 0.6f).</param>
    /// <param name="nmsThreshold">IoU threshold used during Non-Maximum Suppression to remove duplicates (default: 0.3f).</param>
    /// <param name="enableGpu">Whether to attempt DirectML GPU hardware acceleration with automatic CPU fallback.</param>
    public PalmDetector(string modelPath, float scoreThreshold = 0.6f, float nmsThreshold = 0.3f, bool enableGpu = true)
    {
        _scoreThreshold = scoreThreshold;
        _nmsThreshold = nmsThreshold;

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
        _anchors = PalmAnchorGenerator.GenerateAnchors();
    }

    /// <summary>
    /// Detects all visible hands/palms in the given input image frame.
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

        Cv2.Resize(image, _resizedMat, new Size(targetW, targetH));
        Cv2.CopyMakeBorder(_resizedMat, _paddedMat, padTop, padBottom, padLeft, padRight, BorderTypes.Constant, Scalar.All(0));
        Cv2.CvtColor(_paddedMat, _rgbMat, ColorConversionCodes.BGR2RGB);

        // 3. Populate preallocated ONNX dense float tensor [Batch=1, Height=192, Width=192, Channels=3]
        unsafe
        {
            byte* ptr = (byte*)_rgbMat.Data.ToPointer();
            int step = (int)_rgbMat.Step();

            for (int y = 0; y < InputHeight; y++)
            {
                byte* row = ptr + y * step;
                for (int x = 0; x < InputWidth; x++)
                {
                    _inputTensor[0, y, x, 0] = row[x * 3 + 0] / 255.0f;
                    _inputTensor[0, y, x, 1] = row[x * 3 + 1] / 255.0f;
                    _inputTensor[0, y, x, 2] = row[x * 3 + 2] / 255.0f;
                }
            }
        }

        // 4. Execute ONNX inference
        List<NamedOnnxValue> inputs =
        [
            NamedOnnxValue.CreateFromTensor("input_1", _inputTensor)
        ];

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs = _session.Run(inputs);

        float[] boxData = [];
        float[] scoreData = [];

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

        // 5. Decode anchor boxes and evaluate classification confidence via PalmBoxDecoder
        float padBiasX = padLeft / ratio;
        float padBiasY = padTop / ratio;
        float scale = Math.Max(origW, origH);

        return PalmBoxDecoder.Decode(
            boxData,
            scoreData,
            _anchors,
            _scoreThreshold,
            _nmsThreshold,
            scale,
            padBiasX,
            padBiasY,
            InputWidth,
            InputHeight);
    }

    /// <summary>
    /// Releases the unmanaged ONNX Runtime session and frees allocated native memory resources.
    /// </summary>
    public void Dispose()
    {
        _resizedMat.Dispose();
        _paddedMat.Dispose();
        _rgbMat.Dispose();
        _session.Dispose();
        GC.SuppressFinalize(this);
    }
}
