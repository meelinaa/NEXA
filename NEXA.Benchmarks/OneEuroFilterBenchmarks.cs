using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using NEXA.Filter;
using OpenCvSharp;

namespace NEXA.Benchmarks;

/// <summary>
/// Micro-benchmarks for 1D and 2D adaptive 1€-filter signal smoothing across high-throughput frame feeds.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class OneEuroFilterBenchmarks
{
    private OneEuroFilter _filter1D = null!;
    private OneEuroFilter2D _filter2D = null!;
    private Point2f[] _handLandmarks21 = null!;
    private OneEuroFilter2D[] _handFilters21 = null!;

    [Params(100, 1000)]
    public int IterationCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _filter1D = new OneEuroFilter(minCutoff: 1.2, beta: 0.05, dCutoff: 1.0);
        _filter2D = new OneEuroFilter2D(minCutoff: 1.2, beta: 0.05, dCutoff: 1.0);

        _handLandmarks21 = new Point2f[21];
        _handFilters21 = new OneEuroFilter2D[21];
        for (int i = 0; i < 21; i++)
        {
            _handLandmarks21[i] = new Point2f(100.0f + i * 5, 200.0f + i * 3);
            _handFilters21[i] = new OneEuroFilter2D(minCutoff: 1.2, beta: 0.05, dCutoff: 1.0);
        }
    }

    [Benchmark(Baseline = true)]
    public double Filter1D_SingleSample()
    {
        double result = 0.0;
        for (int i = 0; i < IterationCount; i++)
        {
            double val = Math.Sin(i * 0.05) * 100.0 + (i % 5 == 0 ? 2.0 : -2.0);
            result = _filter1D.Filter(val, 0.016);
        }
        return result;
    }

    [Benchmark]
    public Point2f Filter2D_SinglePoint()
    {
        Point2f result = default;
        for (int i = 0; i < IterationCount; i++)
        {
            Point2f input = new((float)(Math.Sin(i * 0.05) * 100.0), (float)(Math.Cos(i * 0.05) * 100.0));
            result = _filter2D.Filter(input, 0.016);
        }
        return result;
    }

    [Benchmark]
    public Point2f Filter2D_FullHandSkeleton_21Joints()
    {
        Point2f lastJoint = default;
        for (int frame = 0; frame < IterationCount; frame++)
        {
            float delta = (float)Math.Sin(frame * 0.1);
            for (int j = 0; j < 21; j++)
            {
                Point2f input = new(_handLandmarks21[j].X + delta, _handLandmarks21[j].Y + delta);
                lastJoint = _handFilters21[j].Filter(input, 0.016);
            }
        }
        return lastJoint;
    }
}
