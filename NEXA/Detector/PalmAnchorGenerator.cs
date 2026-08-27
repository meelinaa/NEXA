using System.Collections.Generic;

namespace NEXA.Detector;

/// <summary>
/// Static generator producing the 2,016 normalized SSD prior-anchor center coordinates for MediaPipe BlazePalm.
/// <para>
/// <b>What it is:</b> Anchor grid builder for the BlazePalm Single-Shot Detector architecture.
/// </para>
/// </summary>
public static class PalmAnchorGenerator
{
    /// <summary>
    /// Generates the static grid of 2,016 normalized anchor points across both feature map layers.
    /// <para>
    /// <list type="bullet">
    /// <item><description>Layer 1 (24x24 grid, stride 8): 2 anchors per cell = 1,152 anchors (small/distant hands).</description></item>
    /// <item><description>Layer 2 (12x12 grid, stride 16): 6 anchors per cell = 864 anchors (close-up hands).</description></item>
    /// </list>
    /// </para>
    /// </summary>
    /// <returns>An array of normalized [ax, ay] anchor coordinates.</returns>
    public static float[][] GenerateAnchors()
    {
        List<float[]> anchors = new(2016);

        // Feature Map 1: 24x24 grid (stride 8) - 2 anchors per cell
        for (int y = 0; y < 24; y++)
        {
            for (int x = 0; x < 24; x++)
            {
                float ax = (x + 0.5f) / 24.0f;
                float ay = (y + 0.5f) / 24.0f;
                anchors.Add([ax, ay]);
                anchors.Add([ax, ay]);
            }
        }

        // Feature Map 2: 12x12 grid (stride 16) - 6 anchors per cell
        for (int y = 0; y < 12; y++)
        {
            for (int x = 0; x < 12; x++)
            {
                float ax = (x + 0.5f) / 12.0f;
                float ay = (y + 0.5f) / 12.0f;
                for (int i = 0; i < 6; i++)
                {
                    anchors.Add([ax, ay]);
                }
            }
        }

        return anchors.ToArray();
    }
}
