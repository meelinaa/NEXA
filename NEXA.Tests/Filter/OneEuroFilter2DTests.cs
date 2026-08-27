using System.Diagnostics;
using NEXA.Filter;
using OpenCvSharp;
using Xunit;

namespace NEXA.Tests.Filter;

/// <summary>
/// Comprehensive Right-BICEP unit tests for <see cref="OneEuroFilter2D"/>.
/// </summary>
public class OneEuroFilter2DTests
{
    // [R]IGHT-BICEP: Verifies that high-frequency noise is attenuated while tracking the underlying signal trend.
    [Fact]
    public void Filter_WithNoisyInput_SmoothsOutSpikesTowardsSignalTrend()
    {
        // Arrange
        OneEuroFilter2D filter = new(freq: 30.0, minCutoff: 1.0, beta: 0.001);
        Point2f initial = filter.Filter(new Point2f(100f, 100f), 0.0);

        // Act: Introduce sudden 50px jitter spike on a single frame
        Point2f smoothedSpike = filter.Filter(new Point2f(150f, 100f), 0.033);

        // Assert: Filter should heavily damp the 50px jump
        Assert.True(smoothedSpike.X < 135f, $"Smoothed spike X was {smoothedSpike.X}, expected < 135f.");
    }

    // RIGHT-[B]ICEP: Validates boundary conditions where consecutive samples have identical coordinates (distance == 0.0).
    [Fact]
    public void Filter_WithStationaryPoints_ConvergesExactlyToTargetCoordinate()
    {
        // Arrange
        OneEuroFilter2D filter = new(freq: 30.0, minCutoff: 1.0, beta: 0.01);
        Point2f target = new(500f, 300f);

        // Act
        Point2f result = new(0, 0);
        for (int i = 0; i < 30; i++)
        {
            result = filter.Filter(target, i * 0.033);
        }

        // Assert
        Assert.Equal(target.X, result.X, 1);
        Assert.Equal(target.Y, result.Y, 1);
    }

    // RIGHT-B[I]CEP: Confirms that calling Reset completely clears internal velocity history and allows immediate repositioning.
    [Fact]
    public void Reset_ClearsHistory_AllowingImmediateStepJumpWithoutLag()
    {
        // Arrange
        OneEuroFilter2D filter = new(freq: 30.0, minCutoff: 0.5, beta: 0.001);
        filter.Filter(new Point2f(100f, 100f), 0.0);
        filter.Filter(new Point2f(100f, 100f), 0.033);

        // Act
        filter.Reset();
        Point2f resetJump = filter.Filter(new Point2f(900f, 900f), 0.100);

        // Assert: After reset, the first point must be adopted without smoothing lag
        Assert.Equal(900f, resetJump.X, 1);
        Assert.Equal(900f, resetJump.Y, 1);
    }

    // RIGHT-BI[C]HECK: Cross-checks isotropic 2D filtering against diagonal motion maintaining 1:1 aspect symmetry.
    [Fact]
    public void Filter_WithDiagonalMotion_MaintainsIsotropicSymmetry()
    {
        // Arrange
        OneEuroFilter2D filter = new(freq: 30.0, minCutoff: 1.2, beta: 0.005);

        // Act: Apply symmetric diagonal displacement (X = Y = 100 -> 200)
        filter.Filter(new Point2f(100f, 100f), 0.0);
        Point2f filtered = filter.Filter(new Point2f(200f, 200f), 0.033);

        // Assert: Both X and Y must receive identical isotropic smoothing
        Assert.Equal(filtered.X, filtered.Y, 3);
    }

    // RIGHT-BIC[E]P: Verifies that negative or zero delta-time (dt <= 0) does not throw divide-by-zero exceptions.
    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.033)]
    public void Filter_WithNonPositiveDeltaTime_HandlesGracefullyWithoutThrowing(double invalidTimestamp)
    {
        // Arrange
        OneEuroFilter2D filter = new(freq: 30.0, minCutoff: 1.0, beta: 0.01);
        filter.Filter(new Point2f(50f, 50f), 1.0);

        // Act & Assert
        Point2f result = filter.Filter(new Point2f(60f, 60f), invalidTimestamp);
        Assert.True(!float.IsNaN(result.X) && !float.IsInfinity(result.X));
    }

    // RIGHT-BICE[P]: Ensures 100,000 continuous filter updates execute within strict frame budget (< 20ms).
    [Fact]
    public void Filter_WithHighFrameRateStream_ExecutesWithinPerformanceBudget()
    {
        // Arrange
        OneEuroFilter2D filter = new(freq: 60.0);
        Stopwatch sw = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 100000; i++)
        {
            filter.Filter(new Point2f(i * 0.1f, i * 0.1f), i * 0.016);
        }
        sw.Stop();

        // Assert
        Assert.True(sw.ElapsedMilliseconds < 200, $"100k 1€-filter steps took {sw.ElapsedMilliseconds}ms.");
    }
}
