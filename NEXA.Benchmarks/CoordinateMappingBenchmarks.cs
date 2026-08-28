using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using NEXA.Domain.Grab;

namespace NEXA.Benchmarks;

/// <summary>
/// Micro-benchmarks for camera-to-screen coordinate normalization, boundary margins, and physical display transformations.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class CoordinateMappingBenchmarks
{
    private WindowCoordinateMapper _mapperFhd = null!;
    private WindowCoordinateMapper _mapper4K = null!;

    [Params(1000, 10000)]
    public int OperationsCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _mapperFhd = new WindowCoordinateMapper(1920, 1080);
        _mapper4K = new WindowCoordinateMapper(3840, 2160);
    }

    [Benchmark(Baseline = true)]
    public (double, double) MapToScreen_1080p_ForwardTransform()
    {
        (double screenX, double screenY) result = default;
        for (int i = 0; i < OperationsCount; i++)
        {
            float x = (float)(i % 1280);
            float y = (float)(i % 720);
            result = _mapperFhd.MapToScreen(x, y, 1280, 720);
        }
        return result;
    }

    [Benchmark]
    public (double, double) MapToScreen_4K_ForwardTransform()
    {
        (double screenX, double screenY) result = default;
        for (int i = 0; i < OperationsCount; i++)
        {
            float x = (float)(i % 1280);
            float y = (float)(i % 720);
            result = _mapper4K.MapToScreen(x, y, 1280, 720);
        }
        return result;
    }

    [Benchmark]
    public (float, float) MapFromScreen_InverseTransform()
    {
        (float camX, float camY) result = default;
        for (int i = 0; i < OperationsCount; i++)
        {
            int sx = i % 1920;
            int sy = i % 1080;
            result = _mapperFhd.MapFromScreen(sx, sy, 1280, 720);
        }
        return result;
    }
}
