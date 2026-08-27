using System.Diagnostics;
using NEXA.Configuration;
using Xunit;

namespace NEXA.Tests.Configuration;

/// <summary>
/// Comprehensive Right-BICEP unit tests for <see cref="NexaConfiguration"/> and domain option models.
/// </summary>
public class NexaConfigurationTests
{
    // [R]IGHT-BICEP: Verifies that default configuration values match expected production ergonomics.
    [Fact]
    public void Constructor_SetsStandardProductionDefaults()
    {
        // Arrange & Act
        NexaConfiguration config = new();

        // Assert
        Assert.Equal(1280, config.Camera.FrameWidth);
        Assert.Equal(720, config.Camera.FrameHeight);
        Assert.Equal(30, config.Camera.TargetFps);
        Assert.Equal(850, config.Gestures.DwellClickMilliseconds);
        Assert.Equal(18f, config.Gestures.DwellRadiusPixels);
        Assert.Equal(0.035, config.Gestures.ResizeDeadzonePercent);
        Assert.Equal(0.15f, config.Gestures.ScreenComfortMarginPercent);
        Assert.Equal(42.0, config.Gestures.UndoRotationThresholdDegrees);
        Assert.Equal(1.2, config.Filtering.MinCutoffFrequency);
        Assert.Equal(0.05, config.Filtering.BetaSpeedCoefficient);
        Assert.True(config.Tracking.EnableGpuAcceleration);
    }

    // RIGHT-[B]OUNDARY: Validates boundary values assigned to gesture options remain accessible without truncation.
    [Theory]
    [InlineData(0, 0f, 0.0)]
    [InlineData(5000, 100f, 0.50)]
    public void GestureOptions_WithBoundaryValues_PreservesAssignedMetrics(
        int dwellMs,
        float dwellRadius,
        double deadzone)
    {
        // Arrange
        GestureOptions options = new();

        // Act
        options.DwellClickMilliseconds = dwellMs;
        options.DwellRadiusPixels = dwellRadius;
        options.ResizeDeadzonePercent = deadzone;

        // Assert
        Assert.Equal(dwellMs, options.DwellClickMilliseconds);
        Assert.Equal(dwellRadius, options.DwellRadiusPixels);
        Assert.Equal(deadzone, options.ResizeDeadzonePercent);
    }

    // RIGHT-B[I]CEP: Confirms that disabling and re-enabling GPU acceleration inverts the boolean setting correctly.
    [Fact]
    public void TrackingOptions_TogglingGpuAcceleration_InvertsSetting()
    {
        // Arrange
        TrackingOptions options = new() { EnableGpuAcceleration = true };

        // Act
        options.EnableGpuAcceleration = false;
        bool disabledState = options.EnableGpuAcceleration;

        options.EnableGpuAcceleration = true;
        bool restoredState = options.EnableGpuAcceleration;

        // Assert
        Assert.False(disabledState);
        Assert.True(restoredState);
    }

    // RIGHT-BI[C]HECK: Cross-checks SectionName constant against expected appsettings.json section hierarchy.
    [Fact]
    public void SectionName_MatchesExpectedAppsettingsKey()
    {
        // Arrange & Act
        string section = NexaConfiguration.SectionName;

        // Assert
        Assert.Equal("Nexa", section);
    }

    // RIGHT-BIC[E]P: Verifies that passing empty or custom model paths does not cause unhandled exceptions during property assignment.
    [Theory]
    [InlineData("")]
    [InlineData("custom/models/custom_palm.onnx")]
    public void TrackingOptions_WithCustomPaths_AssignsSafely(string customPath)
    {
        // Arrange
        TrackingOptions options = new();

        // Act
        options.PalmModelPath = customPath;

        // Assert
        Assert.Equal(customPath, options.PalmModelPath);
    }

    // RIGHT-BICE[P]: Ensures 100,000 configuration property accesses execute well within sub-millisecond budgets (< 15ms).
    [Fact]
    public void Configuration_HighThroughputPropertyAccess_ExecutesWithinPerformanceBudget()
    {
        // Arrange
        NexaConfiguration config = new();
        Stopwatch sw = Stopwatch.StartNew();

        // Act
        int totalWidth = 0;
        for (int i = 0; i < 100000; i++)
        {
            totalWidth += config.Camera.FrameWidth;
        }
        sw.Stop();

        // Assert
        Assert.True(sw.ElapsedMilliseconds < 15, $"100k property accesses took {sw.ElapsedMilliseconds}ms.");
        Assert.True(totalWidth > 0);
    }
}
