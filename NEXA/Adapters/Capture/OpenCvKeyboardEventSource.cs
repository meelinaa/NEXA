using NEXA.Abstractions;
using OpenCvSharp;

namespace NEXA.Adapters.Capture;

/// <summary>
/// Concrete OpenCV implementation of <see cref="IKeyboardEventSource"/> wrapping <see cref="Cv2.WaitKey(int)"/>.
/// </summary>
public class OpenCvKeyboardEventSource : IKeyboardEventSource
{
    /// <inheritdoc/>
    public int WaitKey(int delayMs)
    {
        return Cv2.WaitKey(delayMs);
    }
}
