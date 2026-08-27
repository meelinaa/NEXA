using System;
using OpenCvSharp;

namespace NEXA.Abstractions;

/// <summary>
/// Abstraction contract for presenting rendered camera video frames onto an operating system viewport window.
/// <para>
/// <b>What it is:</b> Interface decoupling OpenCV window rendering from application pipeline logic.
/// </para>
/// </summary>
public interface IDisplaySink : IDisposable
{
    /// <summary>
    /// Displays the specified image frame in the presentation window.
    /// </summary>
    /// <param name="image">The image frame to display.</param>
    void ShowImage(Mat image);
}
