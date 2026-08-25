namespace NEXA.Object;

public class ZoomState
{
    public bool Active { get; set; } = false;
    public double BaselineRatio { get; set; } = 1.0;
    public double CurrentZoom { get; set; } = 1.0;
    public double LastStableZoom { get; set; } = 1.0;
    public double LiveRatio { get; set; } = 0.0;
}
