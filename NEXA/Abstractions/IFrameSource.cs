using System;
using OpenCvSharp;

namespace NEXA.Abstractions;

/// <summary>
/// Abstraction contract for camera video stream frame acquisition.
/// <para>
/// <b>What it is:</b> Interface decoupling video capture hardware from processing pipelines for testability.
/// </para>
/// </summary>
public interface IFrameSource : IDisposable
{
    /// <summary>
    /// Opens the camera stream device at the given webcam index.
    /// </summary>
    /// <param name="index">The zero-based hardware camera index.</param>
    /// <returns><c>true</c> if the camera was successfully opened; otherwise, <c>false</c>.</returns>
    bool Open(int index);

    /// <summary>
    /// Checks whether the camera device is currently opened and ready to stream.
    /// </summary>
    /// <returns><c>true</c> if opened; otherwise, <c>false</c>.</returns>
    bool IsOpened();

    /// <summary>
    /// Reads the next video frame from the capture stream.
    /// </summary>
    /// <param name="image">The output OpenCV image frame.</param>
    /// <returns><c>true</c> if a valid frame was read; otherwise, <c>false</c>.</returns>
    bool Read(Mat image);

    /// <summary>
    /// Sets a video capture hardware property.
    /// </summary>
    /// <param name="property">The property identifier.</param>
    /// <param name="value">The target property value.</param>
    /// <returns><c>true</c> if accepted; otherwise, <c>false</c>.</returns>
    bool Set(VideoCaptureProperties property, double value);
}
