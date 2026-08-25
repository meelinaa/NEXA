using System;

namespace NEXA;

public class LowPassFilter
{
    public double LastValue { get; private set; }
    public bool HasLastValue { get; private set; }

    public double Filter(double value, double alpha)
    {
        double result = HasLastValue ? alpha * value + (1.0 - alpha) * LastValue : value;
        LastValue = result;
        HasLastValue = true;
        return result;
    }

    public void Reset()
    {
        HasLastValue = false;
        LastValue = 0;
    }
}

public class OneEuroFilter
{
    private readonly double _freq;
    private readonly double _minCutoff;
    private readonly double _beta;
    private readonly double _dCutoff;
    private readonly LowPassFilter _x;
    private readonly LowPassFilter _dx;
    private double? _lastTime;

    public OneEuroFilter(double freq = 30.0, double minCutoff = 1.0, double beta = 0.007, double dCutoff = 1.0)
    {
        _freq = freq;
        _minCutoff = minCutoff;
        _beta = beta;
        _dCutoff = dCutoff;
        _x = new LowPassFilter();
        _dx = new LowPassFilter();
    }

    public double Filter(double value, double timestamp)
    {
        double dt = _lastTime.HasValue ? timestamp - _lastTime.Value : 1.0 / _freq;
        if (dt <= 0) dt = 1.0 / _freq;
        _lastTime = timestamp;

        double dValue = _x.HasLastValue ? (value - _x.LastValue) / dt : 0.0;
        double edValue = _dx.Filter(dValue, Alpha(dt, _dCutoff));

        double cutoff = _minCutoff + _beta * Math.Abs(edValue);
        return _x.Filter(value, Alpha(dt, cutoff));
    }

    public void Reset()
    {
        _x.Reset();
        _dx.Reset();
        _lastTime = null;
    }

    private static double Alpha(double dt, double cutoff)
    {
        double tau = 1.0 / (2.0 * Math.PI * cutoff);
        return 1.0 / (1.0 + tau / dt);
    }
}
