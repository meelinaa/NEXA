using OpenCvSharp;
using System.Diagnostics;

namespace NEXA.Object;

public class GrabState
{
    public bool Active { get; set; } = false;
    public double HoldDurationSeconds { get; set; } = 0.0;
    public double RequiredHoldTime { get; set; } = 2.0; // 2 Sekunden Haltezeit
    public (double X, double Y) HandOffsetToObject { get; set; }
    public Point2f LastPalmCenter { get; set; }
    public readonly Stopwatch FistTimer = new();
}
