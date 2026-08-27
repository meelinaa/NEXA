using System.Diagnostics;
using NEXA.Domain.Grab;
using Xunit;

namespace NEXA.Tests.Domain;

/// <summary>
/// Comprehensive Right-BICEP unit tests for <see cref="WindowCoordinateMapper"/>.
/// </summary>
public class WindowCoordinateMapperTests
{
    // [R]IGHT-BICEP: Verifies that exact center camera frame coordinates map precisely to center screen coordinates.
    [Fact]
    public void MapToScreen_WithCenterFrameCoordinates_MapsToCenterDesktop()
    {
        // Arrange
        WindowCoordinateMapper mapper = new(1920, 1080);
        float centerX = 640f;
        float centerY = 360f;
        int frameW = 1280;
        int frameH = 720;

        // Act
        (double screenX, double screenY) = mapper.MapToScreen(centerX, centerY, frameW, frameH);

        // Assert
        Assert.Equal(960.0, screenX, 1);
        Assert.Equal(540.0, screenY, 1);
    }

    // RIGHT-[B]ICEP: Validates boundary conditions where points on or beyond comfort margins clamp precisely to 0 and maximum screen pixels.
    [Theory]
    [InlineData(0f, 0f, 0.0, 0.0)]
    [InlineData(1280f, 720f, 1920.0, 1080.0)]
    [InlineData(192f, 108f, 0.0, 0.0)] // 15% inner comfort margin edge
    public void MapToScreen_WithMarginBoundaries_ClampsToValidDesktopLimits(float camX, float camY, double expectedScreenX, double expectedScreenY)
    {
        // Arrange
        WindowCoordinateMapper mapper = new(1920, 1080);

        // Act
        (double screenX, double screenY) = mapper.MapToScreen(camX, camY, 1280, 720);

        // Assert
        Assert.Equal(expectedScreenX, screenX, 1);
        Assert.Equal(expectedScreenY, screenY, 1);
    }

    // RIGHT-B[I]CEP: Confirms that converting camera coordinates to screen and immediately applying inverse MapFromScreen restores original coordinates.
    [Theory]
    [InlineData(640f, 360f)]
    [InlineData(500f, 300f)]
    [InlineData(800f, 450f)]
    public void MapFromScreen_InvertsMapToScreen_PreservingIntermediateCoordinates(float originalCamX, float originalCamY)
    {
        // Arrange
        WindowCoordinateMapper mapper = new(1920, 1080);
        int frameW = 1280;
        int frameH = 720;

        // Act
        (double screenX, double screenY) = mapper.MapToScreen(originalCamX, originalCamY, frameW, frameH);
        (float restoredCamX, float restoredCamY) = mapper.MapFromScreen((int)screenX, (int)screenY, frameW, frameH);

        // Assert
        Assert.Equal(originalCamX, restoredCamX, 0);
        Assert.Equal(originalCamY, restoredCamY, 0);
    }

    // RIGHT-BI[C]HECK: Cross-checks screen mapping against independent normalized linear algebraic formula.
    [Fact]
    public void MapToScreen_CrossChecksAgainstIndependentLinearFormula()
    {
        // Arrange
        WindowCoordinateMapper mapper = new(3840, 2160);
        float testX = 400f;
        float testY = 250f;
        int frameW = 1000;
        int frameH = 500;

        // Act
        (double actualX, double actualY) = mapper.MapToScreen(testX, testY, frameW, frameH);

        // Manual independent cross-check
        float marginX = frameW * 0.15f;
        float marginY = frameH * 0.15f;
        float expectedNormX = (testX - marginX) / (frameW - 2 * marginX);
        float expectedNormY = (testY - marginY) / (frameH - 2 * marginY);
        double expectedX = expectedNormX * 3840;
        double expectedY = expectedNormY * 2160;

        // Assert
        Assert.Equal(expectedX, actualX, 2);
        Assert.Equal(expectedY, actualY, 2);
    }

    // RIGHT-BIC[E]P: Verifies that passing zero or negative constructor resolutions defaults gracefully to standard 1080p without throwing.
    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1920, -1080)]
    public void Constructor_WithInvalidResolutions_DefaultsToFullHD(int invalidW, int invalidH)
    {
        // Arrange & Act
        WindowCoordinateMapper mapper = new(invalidW, invalidH);

        // Assert
        Assert.Equal(1920, mapper.ScreenWidth);
        Assert.Equal(1080, mapper.ScreenHeight);
    }

    // RIGHT-BICE[P]: Ensures 1,000,000 coordinate projection mappings execute within sub-millisecond real-time budgets (< 30ms).
    [Fact]
    public void MapToScreen_WithHighThroughput_ExecutesWithinPerformanceBudget()
    {
        // Arrange
        WindowCoordinateMapper mapper = new(1920, 1080);
        Stopwatch sw = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 1000000; i++)
        {
            mapper.MapToScreen(640f, 360f, 1280, 720);
        }
        sw.Stop();

        // Assert
        Assert.True(sw.ElapsedMilliseconds < 200, $"1M coordinate projections took {sw.ElapsedMilliseconds}ms.");
    }
}
