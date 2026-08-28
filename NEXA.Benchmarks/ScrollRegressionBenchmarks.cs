using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using NEXA.Domain.Scroll;

namespace NEXA.Benchmarks;

/// <summary>
/// Micro-benchmarks for closed-form Least-Squares linear regression calculating gesture trend slopes and swipe speeds.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class ScrollRegressionBenchmarks
{
    private Queue<(double Y, DateTime Time)> _history5 = null!;
    private Queue<(double Y, DateTime Time)> _history10 = null!;
    private Queue<(double Y, DateTime Time)> _history20 = null!;
    private DateTime _referenceTime;

    [GlobalSetup]
    public void Setup()
    {
        _referenceTime = DateTime.UtcNow;
        _history5 = CreateHistory(5, _referenceTime);
        _history10 = CreateHistory(10, _referenceTime);
        _history20 = CreateHistory(20, _referenceTime);
    }

    private static Queue<(double Y, DateTime Time)> CreateHistory(int count, DateTime refTime)
    {
        Queue<(double Y, DateTime Time)> q = new();
        for (int i = 0; i < count; i++)
        {
            double tMs = i * 16.6;
            double y = 100.0 + i * 2.5; // Slope = 2.5 / 16.6 = 0.1506 px/ms
            q.Enqueue((y, refTime.AddMilliseconds(tMs)));
        }
        return q;
    }

    [Benchmark(Baseline = true)]
    public double CalculateTrendSlope_Window5Points()
    {
        return ScrollDetector.CalculateTrendSlope(_history5, _referenceTime);
    }

    [Benchmark]
    public double CalculateTrendSlope_Window10Points()
    {
        return ScrollDetector.CalculateTrendSlope(_history10, _referenceTime);
    }

    [Benchmark]
    public double CalculateTrendSlope_Window20Points()
    {
        return ScrollDetector.CalculateTrendSlope(_history20, _referenceTime);
    }
}
