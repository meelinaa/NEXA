using OpenCvSharp;

namespace NEXA.Abstractions;

/// <summary>
/// Abstraction interface for components providing real-time telemetry status lines and highlight colors to the HUD overlay renderer.
/// <para>
/// <b>What it is:</b> Decouples the HUD presentation renderer from concrete domain controllers via Dependency Inversion.
/// </para>
/// </summary>
public interface IHudStatusProvider
{
    /// <summary>
    /// Computes the formatted status line string to be rendered onto the HUD.
    /// </summary>
    /// <returns>A string containing the feature label, hotkey, and current state.</returns>
    string GetStatusText();

    /// <summary>
    /// Computes the BGR color scalar for the HUD status line.
    /// </summary>
    /// <returns>The OpenCvSharp <see cref="Scalar"/> color.</returns>
    Scalar GetStatusColor();
}
