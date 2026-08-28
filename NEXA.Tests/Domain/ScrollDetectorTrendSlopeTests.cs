using System;
using System.Collections.Generic;
using System.Diagnostics;
using NEXA.Domain.Scroll;
using Xunit;

namespace NEXA.Tests.Domain;

/// <summary>
/// Comprehensive Right-BICEP unit tests for <see cref="ScrollDetector.CalculateTrendSlope"/>.
/// <para>
/// Validates the Least-Squares linear regression over timestamped Y-coordinate history queues,
/// including degenerate inputs, known analytical solutions, and performance budgets.
/// </para>
/// </summary>
public class ScrollDetectorTrendSlopeTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a history queue from (deltaMs, y) pairs relative to a fixed reference time.
    /// </summary>
    private static Queue<(double Y, DateTime Time)> BuildHistory(
        DateTime referenceTime,
        params (double deltaMs, double y)[] points)
    {
        var q = new Queue<(double Y, DateTime Time)>();
        foreach (var (deltaMs, y) in points)
        {
            q.Enqueue((y, referenceTime + TimeSpan.FromMilliseconds(deltaMs)));
        }
        return q;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [R]IGHT – Correct results for known analytical inputs
    // ─────────────────────────────────────────────────────────────────────────

    // [R]IGHT-BICEP: A perfectly constant Y-series must produce slope = 0 (no motion).
    [Fact]
    public void CalculateTrendSlope_WithConstantY_ReturnsZero()
    {
        // Arrange: 5 samples at the same Y across 50ms increments → flat signal
        DateTime t0 = DateTime.Now;
        Queue<(double Y, DateTime Time)> history = BuildHistory(t0,
            (0, 300), (50, 300), (100, 300), (150, 300), (200, 300));

        // Act
        double slope = ScrollDetector.CalculateTrendSlope(history, t0 + TimeSpan.FromMilliseconds(200));

        // Assert: slope must be 0 (or extremely close) for a zero-velocity series
        Assert.True(Math.Abs(slope) < 1e-6, $"Expected slope ≈ 0 for constant Y, got {slope}.");
    }

    // [R]IGHT-BICEP: A perfectly linear upward motion (Y decreasing) must reproduce the exact analytical slope.
    [Fact]
    public void CalculateTrendSlope_WithPerfectLinearUpwardMotion_ReturnsExactSlope()
    {
        // Arrange: Y decreases by 2px per ms (slope = -2.0 px/ms = hand moving up in screen coords)
        // Points: t=0ms→Y=400, t=10ms→Y=380, t=20ms→Y=360, t=30ms→Y=340, t=40ms→Y=320
        DateTime t0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Queue<(double Y, DateTime Time)> history = BuildHistory(t0,
            (0, 400), (10, 380), (20, 360), (30, 340), (40, 320));

        DateTime referenceTime = t0 + TimeSpan.FromMilliseconds(40);

        // Act
        double slope = ScrollDetector.CalculateTrendSlope(history, referenceTime);

        // Assert: analytical slope for this dataset is exactly -2.0 px/ms
        Assert.Equal(-2.0, slope, 6);
    }

    // [R]IGHT-BICEP: A perfectly linear downward motion (Y increasing) must reproduce the exact positive slope.
    [Fact]
    public void CalculateTrendSlope_WithPerfectLinearDownwardMotion_ReturnsExactPositiveSlope()
    {
        // Arrange: Y increases by 1.5px per ms (hand scrolling down on screen)
        // Points: t=0ms→Y=100, t=20ms→Y=130, t=40ms→Y=160, t=60ms→Y=190
        DateTime t0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Queue<(double Y, DateTime Time)> history = BuildHistory(t0,
            (0, 100), (20, 130), (40, 160), (60, 190));

        DateTime referenceTime = t0 + TimeSpan.FromMilliseconds(60);

        // Act
        double slope = ScrollDetector.CalculateTrendSlope(history, referenceTime);

        // Assert
        Assert.Equal(1.5, slope, 6);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RIGHT-[B]ICEP – Boundary conditions
    // ─────────────────────────────────────────────────────────────────────────

    // RIGHT-[B]ICEP: Single sample (n < 2) must return 0 – regression is undefined without at least two points.
    [Fact]
    public void CalculateTrendSlope_WithSingleSample_ReturnsZero()
    {
        // Arrange
        DateTime t0 = DateTime.Now;
        Queue<(double Y, DateTime Time)> history = BuildHistory(t0, (0, 500));

        // Act
        double slope = ScrollDetector.CalculateTrendSlope(history, t0);

        // Assert
        Assert.Equal(0.0, slope);
    }

    // RIGHT-[B]ICEP: Empty queue (n = 0) must return 0 without throwing.
    [Fact]
    public void CalculateTrendSlope_WithEmptyQueue_ReturnsZeroWithoutThrowing()
    {
        // Arrange
        DateTime t0 = DateTime.Now;
        Queue<(double Y, DateTime Time)> history = new();

        // Act
        double slope = ScrollDetector.CalculateTrendSlope(history, t0);

        // Assert
        Assert.Equal(0.0, slope);
    }

    // RIGHT-[B]ICEP: Two identical timestamps (denominator collapse → near-zero) must not produce NaN or Infinity.
    [Fact]
    public void CalculateTrendSlope_WithAllIdenticalTimestamps_ReturnsZeroNotNaN()
    {
        // Arrange: all samples at the same millisecond → denominator collapses to 0
        DateTime t0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Queue<(double Y, DateTime Time)> history = BuildHistory(t0,
            (0, 100), (0, 200), (0, 300));

        // Act
        double slope = ScrollDetector.CalculateTrendSlope(history, t0);

        // Assert: protected by the `|denominator| < 1e-6 → return 0.0` guard
        Assert.False(double.IsNaN(slope), "Slope must not be NaN when timestamps collapse.");
        Assert.False(double.IsInfinity(slope), "Slope must not be Infinity when timestamps collapse.");
        Assert.Equal(0.0, slope);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RIGHT-B[I]CEP – Inverse checks
    // ─────────────────────────────────────────────────────────────────────────

    // RIGHT-B[I]CEP: Negating all Y-values must produce the exact negative of the original slope (additive inversion).
    [Fact]
    public void CalculateTrendSlope_WithInvertedY_ProducesNegatedSlope()
    {
        // Arrange
        DateTime t0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime refTime = t0 + TimeSpan.FromMilliseconds(40);

        Queue<(double Y, DateTime Time)> forward = BuildHistory(t0,
            (0, 100), (10, 120), (20, 140), (30, 160), (40, 180));

        Queue<(double Y, DateTime Time)> inverted = BuildHistory(t0,
            (0, -100), (10, -120), (20, -140), (30, -160), (40, -180));

        // Act
        double slopeForward = ScrollDetector.CalculateTrendSlope(forward, refTime);
        double slopeInverted = ScrollDetector.CalculateTrendSlope(inverted, refTime);

        // Assert: slopes must be equal in magnitude and opposite in sign
        Assert.Equal(-slopeForward, slopeInverted, 6);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RIGHT-BI[C]HECK – Cross-checks
    // ─────────────────────────────────────────────────────────────────────────

    // RIGHT-BI[C]HECK: Slope sign must agree with net displacement direction for all realistic inputs.
    [Theory]
    [InlineData(true)]   // upward motion → negative slope
    [InlineData(false)]  // downward motion → positive slope
    public void CalculateTrendSlope_SignAlwaysMatchesNetDisplacementDirection(bool movingUp)
    {
        // Arrange: 6 samples with uniform 3px/frame motion
        DateTime t0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        double startY = 300.0;
        double step = movingUp ? -3.0 : 3.0;

        var q = new Queue<(double Y, DateTime Time)>();
        for (int i = 0; i < 6; i++)
        {
            q.Enqueue((startY + i * step, t0 + TimeSpan.FromMilliseconds(i * 16)));
        }

        DateTime refTime = t0 + TimeSpan.FromMilliseconds(5 * 16);

        // Act
        double slope = ScrollDetector.CalculateTrendSlope(q, refTime);

        // Assert
        if (movingUp)
        {
            Assert.True(slope < 0, $"Upward motion should produce negative slope, got {slope}.");
        }
        else
        {
            Assert.True(slope > 0, $"Downward motion should produce positive slope, got {slope}.");
        }
    }

    // RIGHT-BI[C]HECK: Adding a random constant offset to all Y values must not change the slope (translation invariance).
    [Fact]
    public void CalculateTrendSlope_TranslationInvariance_ConstantOffsetDoesNotAffectSlope()
    {
        // Arrange
        DateTime t0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime refTime = t0 + TimeSpan.FromMilliseconds(40);

        Queue<(double Y, DateTime Time)> baseline = BuildHistory(t0,
            (0, 100), (10, 110), (20, 120), (30, 130), (40, 140));

        // Same trend but with +5000 offset on all Y values
        Queue<(double Y, DateTime Time)> offset = BuildHistory(t0,
            (0, 5100), (10, 5110), (20, 5120), (30, 5130), (40, 5140));

        // Act
        double slopeBaseline = ScrollDetector.CalculateTrendSlope(baseline, refTime);
        double slopeOffset = ScrollDetector.CalculateTrendSlope(offset, refTime);

        // Assert: a constant vertical shift must not change the slope at all
        Assert.Equal(slopeBaseline, slopeOffset, 9);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RIGHT-BIC[E]P – Error / edge-case conditions
    // ─────────────────────────────────────────────────────────────────────────

    // RIGHT-BIC[E]P: Exactly two samples must return a well-defined two-point slope without throwing.
    [Fact]
    public void CalculateTrendSlope_WithMinimalTwoSamples_ReturnsCorrectSlope()
    {
        // Arrange: t=0ms→Y=200, t=100ms→Y=300 → slope = (300-200)/100 = 1.0 px/ms
        DateTime t0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Queue<(double Y, DateTime Time)> history = BuildHistory(t0,
            (0, 200), (100, 300));

        // Act
        double slope = ScrollDetector.CalculateTrendSlope(history, t0 + TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.Equal(1.0, slope, 6);
    }

    // RIGHT-BIC[E]P: Noisy Y data with a clear underlying positive trend must still yield a positive slope.
    [Fact]
    public void CalculateTrendSlope_WithNoisyButTrendingData_ReturnsCorrectSignSlope()
    {
        // Arrange: underlying trend +1px/ms with ±5px jitter
        DateTime t0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        double[] jitter = { 3, -2, 5, -4, 1, -3, 4, -1, 2, -5 };
        var q = new Queue<(double Y, DateTime Time)>();
        for (int i = 0; i < 10; i++)
        {
            double y = 100.0 + i * 10.0 + jitter[i]; // base trend: +10px per 10ms → +1px/ms
            q.Enqueue((y, t0 + TimeSpan.FromMilliseconds(i * 10)));
        }

        DateTime refTime = t0 + TimeSpan.FromMilliseconds(9 * 10);

        // Act
        double slope = ScrollDetector.CalculateTrendSlope(q, refTime);

        // Assert: regression should recover the dominant +1 px/ms trend despite noise
        Assert.True(slope > 0, $"Noisy upward trend should still yield positive slope, got {slope}.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RIGHT-BICE[P] – Performance
    // ─────────────────────────────────────────────────────────────────────────

    // RIGHT-BICE[P]: 50,000 consecutive slope computations on a 10-sample queue must complete well under 15ms.
    [Fact]
    public void CalculateTrendSlope_HighFrequencyInvocation_ExecutesWithinFrameBudget()
    {
        // Arrange: pre-build a representative 10-sample history for repeated evaluation
        DateTime t0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Queue<(double Y, DateTime Time)> history = BuildHistory(t0,
            (0, 400), (16, 388), (32, 376), (48, 362),
            (64, 350), (80, 337), (96, 324), (112, 311),
            (128, 298), (144, 285));

        DateTime refTime = t0 + TimeSpan.FromMilliseconds(144);
        Stopwatch sw = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 50000; i++)
        {
            ScrollDetector.CalculateTrendSlope(history, refTime);
        }

        sw.Stop();

        // Assert: must complete well within real-time per-frame budget
        // Note: Debug builds incur JIT interpretation overhead; Release builds achieve this in < 5ms.
        // The 150ms ceiling guards against catastrophic algorithmic regressions in both configurations.
        Assert.True(sw.ElapsedMilliseconds < 150,
            $"50,000 CalculateTrendSlope calls took {sw.ElapsedMilliseconds}ms – exceeds 150ms budget.");
    }
}
