using System;
using System.IO;
using System.Runtime.InteropServices;
using DlibDotNet;
using NEXA.Filter;
using OpenCvSharp;

namespace NEXA.Face;

/// <summary>
/// Hybrid high-performance Face Tracker combining OpenCV Haar Cascade face detection with DlibDotNet 68-point facial landmark shape prediction.
/// <para>
/// <b>What it is:</b> Real-time facial telemetry module providing sub-millisecond face bounding boxes and 68 facial landmark contour coordinates.
/// </para>
/// </summary>
public class FaceTracker : IDisposable
{
    private CascadeClassifier? _faceCascade;
    private FrontalFaceDetector? _dlibFaceDetector;
    private ShapePredictor? _shapePredictor;
    private readonly OneEuroFilter _filterX;
    private readonly OneEuroFilter _filterY;
    private readonly OneEuroFilter _filterW;
    private readonly OneEuroFilter _filterH;
    private readonly OneEuroFilter[] _landmarkFilters = new OneEuroFilter[68];

    /// <summary>
    /// Gets or sets a value indicating whether face tracking is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="FaceTracker"/> class.
    /// </summary>
    public FaceTracker()
    {
        _filterX = new OneEuroFilter(30.0, 1.2, 0.005);
        _filterY = new OneEuroFilter(30.0, 1.2, 0.005);
        _filterW = new OneEuroFilter(30.0, 1.2, 0.005);
        _filterH = new OneEuroFilter(30.0, 1.2, 0.005);

        for (int i = 0; i < 68; i++)
        {
            _landmarkFilters[i] = new OneEuroFilter(30.0, 1.5, 0.007);
        }

        TryInitializeDetectors();
    }

    private void TryInitializeDetectors()
    {
        // 1. Initialize OpenCV Haar Cascade
        string[] cascadePaths = new string[]
        {
            Path.Combine(AppContext.BaseDirectory, "models", "haarcascade_frontalface_default.xml"),
            "models/haarcascade_frontalface_default.xml",
            Path.Combine(Directory.GetCurrentDirectory(), "models", "haarcascade_frontalface_default.xml")
        };

        foreach (string path in cascadePaths)
        {
            if (File.Exists(path))
            {
                try
                {
                    _faceCascade = new CascadeClassifier(path);
                    Console.WriteLine($"[FaceTracker] OpenCV Face Cascade loaded from: {path}");
                    break;
                }
                catch
                {
                    // Ignore and try fallback
                }
            }
        }

        // 2. Initialize Dlib Detectors & Shape Predictor
        try
        {
            _dlibFaceDetector = Dlib.GetFrontalFaceDetector();

            string[] shapePaths = new string[]
            {
                Path.Combine(AppContext.BaseDirectory, "models", "shape_predictor_68_face_landmarks.dat"),
                "models/shape_predictor_68_face_landmarks.dat",
                Path.Combine(Directory.GetCurrentDirectory(), "models", "shape_predictor_68_face_landmarks.dat")
            };

            foreach (string path in shapePaths)
            {
                if (File.Exists(path) && new FileInfo(path).Length > 10_000_000)
                {
                    Console.WriteLine($"[FaceTracker] Loading Dlib 68-landmark model from: {path} ({new FileInfo(path).Length / 1_000_000} MB)...");
                    _shapePredictor = ShapePredictor.Deserialize(path);
                    Console.WriteLine("[FaceTracker] Dlib 68-landmark model successfully loaded!");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[FaceTracker Warning] Dlib initialization note: {ex.Message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Analyzes the input frame to detect the primary user's face and all 68 landmark tracking points.
    /// </summary>
    /// <param name="frame">The camera BGR frame.</param>
    /// <returns>A <see cref="TrackedFace"/> instance if detected; otherwise, <c>null</c>.</returns>
    public TrackedFace? ProcessFrame(Mat frame)
    {
        if (!Enabled || frame == null || frame.Empty())
            return null;

        double timestamp = DateTime.Now.Ticks / (double)TimeSpan.TicksPerSecond;
        Rect? detectedFaceRect = null;
        Point2f[] landmarks = new Point2f[68];
        bool landmarksExtracted = false;

        // 1. Detect Face Bounding Box via Fast OpenCV Haar Cascade
        if (_faceCascade != null && !_faceCascade.Empty())
        {
            try
            {
                using Mat gray = new();
                Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
                Cv2.EqualizeHist(gray, gray);

                Rect[] faces = _faceCascade.DetectMultiScale(
                    gray,
                    scaleFactor: 1.15,
                    minNeighbors: 4,
                    flags: HaarDetectionTypes.ScaleImage,
                    minSize: new Size(80, 80)
                );

                if (faces != null && faces.Length > 0)
                {
                    Rect largest = faces[0];
                    for (int i = 1; i < faces.Length; i++)
                    {
                        if (faces[i].Width * faces[i].Height > largest.Width * largest.Height)
                        {
                            largest = faces[i];
                        }
                    }
                    detectedFaceRect = largest;
                }
            }
            catch
            {
                // Fallback to Dlib detector
            }
        }

        // 2. Prepare RGB Image for Dlib Shape Prediction
        if (_shapePredictor != null)
        {
            try
            {
                using Mat rgb = new();
                Cv2.CvtColor(frame, rgb, ColorConversionCodes.BGR2RGB);

                byte[] array = new byte[rgb.Width * rgb.Height * rgb.ElemSize()];
                Marshal.Copy(rgb.Data, array, 0, array.Length);

                using Array2D<RgbPixel> cimg = Dlib.LoadImageData<RgbPixel>(
                    array,
                    (uint)rgb.Height,
                    (uint)rgb.Width,
                    (uint)rgb.Step());

                DlibDotNet.Rectangle targetFace;

                if (detectedFaceRect.HasValue)
                {
                    Rect r = detectedFaceRect.Value;
                    // Dlib shape predictor was trained on full-head boxes including forehead (fine-tuned padding)
                    int dlibTop = Math.Max(0, (int)(r.Y - r.Height * 0.08));
                    int dlibBottom = Math.Min(rgb.Height, (int)(r.Y + r.Height * 1.15));
                    int dlibLeft = Math.Max(0, (int)(r.X - r.Width * 0.04));
                    int dlibRight = Math.Min(rgb.Width, (int)(r.X + r.Width * 1.04));

                    targetFace = new DlibDotNet.Rectangle(dlibLeft, dlibTop, dlibRight, dlibBottom);
                }
                else if (_dlibFaceDetector != null)
                {
                    DlibDotNet.Rectangle[] dlibFaces = _dlibFaceDetector.Operator(cimg);
                    if (dlibFaces != null && dlibFaces.Length > 0)
                    {
                        targetFace = dlibFaces[0];
                        for (int i = 1; i < dlibFaces.Length; i++)
                        {
                            if (dlibFaces[i].Area > targetFace.Area)
                            {
                                targetFace = dlibFaces[i];
                            }
                        }
                        detectedFaceRect = new Rect(targetFace.Left, targetFace.Top, (int)targetFace.Width, (int)targetFace.Height);
                    }
                    else
                    {
                        return null; // No face found
                    }
                }
                else
                {
                    return null; // No detector available
                }

                using FullObjectDetection shape = _shapePredictor.Detect(cimg, targetFace);
                for (uint i = 0; i < 68; i++)
                {
                    DlibDotNet.Point pt = shape.GetPart(i);
                    float fx = (float)_landmarkFilters[i].Filter(pt.X, timestamp);
                    float fy = (float)_landmarkFilters[i].Filter(pt.Y, timestamp);
                    landmarks[i] = new Point2f(fx, fy);
                }
                landmarksExtracted = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FaceTracker Landmark Error] {ex.Message}");
            }
        }

        if (!detectedFaceRect.HasValue || !landmarksExtracted)
        {
            return null;
        }

        Rect raw = detectedFaceRect.Value;
        float smoothX = (float)_filterX.Filter(raw.X, timestamp);
        float smoothY = (float)_filterY.Filter(raw.Y, timestamp);
        float smoothW = (float)_filterW.Filter(raw.Width, timestamp);
        float smoothH = (float)_filterH.Filter(raw.Height, timestamp);
        Rect2f smoothedBox = new(smoothX, smoothY, smoothW, smoothH);

        // 3. Compute Mouth Center & Proximity Radius from Lip Landmarks (48..67)
        float mouthSumX = 0f;
        float mouthSumY = 0f;
        for (int i = 48; i <= 67; i++)
        {
            mouthSumX += landmarks[i].X;
            mouthSumY += landmarks[i].Y;
        }

        Point2f mouthCenter = new(mouthSumX / 20f, mouthSumY / 20f);
        double mouthCornerDist = Math.Sqrt(Math.Pow(landmarks[54].X - landmarks[48].X, 2) + Math.Pow(landmarks[54].Y - landmarks[48].Y, 2));
        float mouthRadius = (float)Math.Max(35.0, mouthCornerDist * 0.70);

        Point2f leftEye = landmarks[45]; // Left eye outer corner
        Point2f rightEye = landmarks[36]; // Right eye outer corner
        Point2f noseTip = landmarks[30]; // Nose tip

        return new TrackedFace
        {
            BoundingBox = smoothedBox,
            Landmarks68 = landmarks,
            MouthCenter = mouthCenter,
            MouthRadius = mouthRadius,
            LeftEye = leftEye,
            RightEye = rightEye,
            NoseTip = noseTip,
            Confidence = 1.0f
        };
    }

    /// <summary>
    /// Disposes face tracking resources.
    /// </summary>
    public void Dispose()
    {
        _faceCascade?.Dispose();
        _faceCascade = null;

        _dlibFaceDetector?.Dispose();
        _dlibFaceDetector = null;

        _shapePredictor?.Dispose();
        _shapePredictor = null;

        GC.SuppressFinalize(this);
    }
}
