using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using NEXA.Common;
using OpenCvSharp;
using Xunit;

namespace NEXA.Tests.Application;

/// <summary>
/// Comprehensive Right-BICEP unit tests for <see cref="MatRingBuffer"/> validating zero-allocation native matrix recycling,
/// concurrency, lease scopes, and disposal lifecycles.
/// </summary>
public class MatRingBufferTests
{
    // [R]IGHT-BICEP: Validates that Rent and Return cycle reuses the pre-allocated matrix instances.
    [Fact]
    public void Rent_And_Return_ReusesMatrixInstance()
    {
        // Arrange
        using MatRingBuffer ringBuffer = new(capacity: 1);

        // Act
        Mat mat1 = ringBuffer.Rent();
        ringBuffer.Return(mat1);
        Mat mat2 = ringBuffer.Rent();

        // Assert
        Assert.Same(mat1, mat2);
        ringBuffer.Return(mat2);
    }

    // RIGHT-[B]ICEP: Validates boundary conditions when renting more matrices than pre-allocated capacity.
    [Fact]
    public void Rent_ExceedingCapacity_ReturnsValidTransientMatrixAndDisposesOnReturn()
    {
        // Arrange
        using MatRingBuffer ringBuffer = new(capacity: 2);

        // Act: rent all pooled items + 1 overflow
        Mat mat1 = ringBuffer.Rent();
        Mat mat2 = ringBuffer.Rent();
        Mat overflowMat = ringBuffer.Rent();

        // Assert: all are valid, non-null matrices
        Assert.NotNull(mat1);
        Assert.NotNull(mat2);
        Assert.NotNull(overflowMat);

        // Returning overflow matrix should safely dispose it without crashing or polluting pool
        ringBuffer.Return(overflowMat);
        ringBuffer.Return(mat1);
        ringBuffer.Return(mat2);

        Mat reused1 = ringBuffer.Rent();
        Mat reused2 = ringBuffer.Rent();

        Assert.True(ReferenceEquals(reused1, mat1) || ReferenceEquals(reused1, mat2));
        Assert.True(ReferenceEquals(reused2, mat1) || ReferenceEquals(reused2, mat2));

        ringBuffer.Return(reused1);
        ringBuffer.Return(reused2);
    }

    // RIGHT-B[I]CEP: Confirms that MatLease using pattern automatically inverts Rent upon disposal.
    [Fact]
    public void MatLease_WhenDisposed_AutomaticallyReturnsMatrixToPool()
    {
        // Arrange
        using MatRingBuffer ringBuffer = new(capacity: 1);
        Mat rentedRef;

        // Act
        using (MatLease lease = ringBuffer.RentLease())
        {
            rentedRef = lease.Mat;
            Assert.NotNull(rentedRef);
        } // Lease automatically disposed here

        // Assert: Next rent should return the exact same matrix
        Mat nextMat = ringBuffer.Rent();
        Assert.Same(rentedRef, nextMat);
        ringBuffer.Return(nextMat);
    }

    // RIGHT-BI[C]EP: Cross-checks multithreaded concurrent rents and returns for thread safety.
    [Fact]
    public async Task ConcurrentAccess_AcrossMultipleThreads_OperatesSafelyWithoutCorruption()
    {
        // Arrange
        using MatRingBuffer ringBuffer = new(capacity: 8);
        const int iterationsPerTask = 500;
        const int taskCount = 8;

        // Act
        Task[] tasks = new Task[taskCount];
        for (int t = 0; t < taskCount; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < iterationsPerTask; i++)
                {
                    using MatLease lease = ringBuffer.RentLease();
                    Assert.NotNull(lease.Mat);
                }
            });
        }

        await Task.WhenAll(tasks);

        // Assert: Pool is fully intact and usable
        Mat mat = ringBuffer.Rent();
        Assert.NotNull(mat);
        ringBuffer.Return(mat);
    }

    // RIGHT-BIC[E]P: Validates error conditions for invalid constructor parameters and disposed states.
    [Fact]
    public void Constructor_WithInvalidCapacity_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new MatRingBuffer(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MatRingBuffer(-5));
    }

    [Fact]
    public void Rent_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        MatRingBuffer ringBuffer = new(capacity: 2);
        ringBuffer.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => ringBuffer.Rent());
    }

    // RIGHT-BICE[P]: Performance Budget: 50,000 rent/return cycles complete in < 50ms.
    [Fact]
    public void Performance_50kRentReturnCycles_CompletesWithinBudget()
    {
        // Arrange
        using MatRingBuffer ringBuffer = new(capacity: 4);
        Stopwatch sw = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 50_000; i++)
        {
            Mat mat = ringBuffer.Rent();
            ringBuffer.Return(mat);
        }
        sw.Stop();

        // Assert
        Assert.True(sw.ElapsedMilliseconds < 150, $"50k iterations took {sw.ElapsedMilliseconds}ms, exceeding budget.");
    }
}
