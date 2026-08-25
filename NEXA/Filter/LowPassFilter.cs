namespace NEXA.Filter;

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
