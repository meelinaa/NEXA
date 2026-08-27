namespace NEXA.Configuration;

/// <summary>
/// Machine learning model and ONNX runtime configuration options.
/// </summary>
public class TrackingOptions
{
    /// <summary>
    /// File path to the MediaPipe Palm Detection ONNX model.
    /// </summary>
    public string PalmModelPath { get; set; } = "models/palm_detection.onnx";

    /// <summary>
    /// File path to the MediaPipe Hand Landmark Estimation ONNX model.
    /// </summary>
    public string LandmarkModelPath { get; set; } = "models/handpose_estimation.onnx";

    /// <summary>
    /// Gets or sets a value indicating whether DirectML hardware GPU acceleration is requested.
    /// </summary>
    public bool EnableGpuAcceleration { get; set; } = true;
}
