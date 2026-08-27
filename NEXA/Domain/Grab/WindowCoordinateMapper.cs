using System;

namespace NEXA.Domain.Grab;

/// <summary>
/// Coordinate transformation engine converting between camera 2D frame pixels and desktop monitor screen pixels with comfort margins.
/// <para>
/// <b>What it is:</b> Mathematical projector applying normalized aspect scaling and edge margin padding.
/// </para>
/// </summary>
public class WindowCoordinateMapper
{
    /// <summary>
    /// Desktop primary monitor horizontal resolution in pixels.
    /// </summary>
    public int ScreenWidth { get; }

    /// <summary>
    /// Desktop primary monitor vertical resolution in pixels.
    /// </summary>
    public int ScreenHeight { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowCoordinateMapper"/> class.
    /// </summary>
    /// <param name="screenWidth">Monitor display width in pixels.</param>
    /// <param name="screenHeight">Monitor display height in pixels.</param>
    public WindowCoordinateMapper(int screenWidth, int screenHeight)
    {
        ScreenWidth = screenWidth > 0 ? screenWidth : 1920;
        ScreenHeight = screenHeight > 0 ? screenHeight : 1080;
    }

    /// <summary>
    /// Maps 2D camera coordinates into desktop screen coordinates with 15% comfort margins.
    /// </summary>
    /// <param name="x">Horizontal camera coordinate in pixels.</param>
    /// <param name="y">Vertical camera coordinate in pixels.</param>
    /// <param name="frameWidth">Camera frame width in pixels.</param>
    /// <param name="frameHeight">Camera frame height in pixels.</param>
    /// <returns>A tuple with (screenX, screenY) in physical desktop pixel coordinates.</returns>
    public (double screenX, double screenY) MapToScreen(float x, float y, int frameWidth, int frameHeight)
    {
        float marginX = frameWidth * 0.15f;
        float marginY = frameHeight * 0.15f;

        float normX = Math.Clamp((x - marginX) / (frameWidth - 2 * marginX), 0.0f, 1.0f);
        float normY = Math.Clamp((y - marginY) / (frameHeight - 2 * marginY), 0.0f, 1.0f);

        double screenX = normX * ScreenWidth;
        double screenY = normY * ScreenHeight;

        return (screenX, screenY);
    }

    /// <summary>
    /// Performs the exact mathematical inverse of <see cref="MapToScreen"/> to map desktop screen coordinates back into camera pixel space.
    /// </summary>
    /// <param name="screenX">Horizontal screen pixel coordinate.</param>
    /// <param name="screenY">Vertical screen pixel coordinate.</param>
    /// <param name="frameWidth">Camera frame width in pixels.</param>
    /// <param name="frameHeight">Camera frame height in pixels.</param>
    /// <returns>A tuple with (camX, camY) in camera frame pixel coordinates.</returns>
    public (float camX, float camY) MapFromScreen(int screenX, int screenY, int frameWidth, int frameHeight)
    {
        float normX = (float)screenX / ScreenWidth;
        float normY = (float)screenY / ScreenHeight;

        float marginX = frameWidth * 0.15f;
        float marginY = frameHeight * 0.15f;

        float camX = marginX + normX * (frameWidth - 2 * marginX);
        float camY = marginY + normY * (frameHeight - 2 * marginY);

        return (camX, camY);
    }
}
