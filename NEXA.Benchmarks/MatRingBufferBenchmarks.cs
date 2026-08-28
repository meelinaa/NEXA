using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using NEXA.Common;
using OpenCvSharp;

namespace NEXA.Benchmarks;

/// <summary>
/// Micro-benchmarks validating Zero-Allocation native matrix pooling via <see cref="MatRingBuffer"/> compared to cyclic heap allocations.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class MatRingBufferBenchmarks
{
    private MatRingBuffer _pool = null!;

    [Params(100, 1000)]
    public int Cycles { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _pool = new MatRingBuffer(capacity: 8);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _pool.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void MatRingBuffer_RentLease_ZeroAlloc()
    {
        for (int i = 0; i < Cycles; i++)
        {
            using MatLease lease = _pool.RentLease();
            _ = lease.Mat.Rows;
        }
    }

    [Benchmark]
    public void MatRingBuffer_RentAndReturn_Manual()
    {
        for (int i = 0; i < Cycles; i++)
        {
            Mat mat = _pool.Rent();
            _ = mat.Rows;
            _pool.Return(mat);
        }
    }

    [Benchmark]
    public void HeapAllocation_NewMatAndDispose()
    {
        for (int i = 0; i < Cycles; i++)
        {
            using Mat mat = new(720, 1280, MatType.CV_8UC3);
            _ = mat.Rows;
        }
    }
}
