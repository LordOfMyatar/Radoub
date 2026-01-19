using Avalonia;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for ImageHelper service.
/// Issue #972 - Part of Epic #959 (UI Uniformity).
/// </summary>
public class ImageHelperTests
{
    #region Portrait Size Constants

    [Fact]
    public void PortraitSizes_HaveCorrectDimensions()
    {
        // Standard NWN portrait sizes
        Assert.Equal(32, ImageHelper.PortraitTiny.Width);
        Assert.Equal(40, ImageHelper.PortraitTiny.Height);

        Assert.Equal(64, ImageHelper.PortraitSmall.Width);
        Assert.Equal(100, ImageHelper.PortraitSmall.Height);

        Assert.Equal(128, ImageHelper.PortraitMedium.Width);
        Assert.Equal(200, ImageHelper.PortraitMedium.Height);

        Assert.Equal(256, ImageHelper.PortraitLarge.Width);
        Assert.Equal(400, ImageHelper.PortraitLarge.Height);

        Assert.Equal(512, ImageHelper.PortraitHuge.Width);
        Assert.Equal(800, ImageHelper.PortraitHuge.Height);
    }

    [Fact]
    public void PortraitSizes_MaintainConsistentAspectRatio()
    {
        // Standard portrait sizes use 0.64 ratio (16:25)
        // Note: Tiny has a different ratio (0.8) for inventory icons
        const double expectedRatio = 0.64;
        const double tolerance = 0.001;

        // Tiny is intentionally different (0.8 ratio for inventory icons)
        Assert.InRange(ImageHelper.PortraitTiny.Width / ImageHelper.PortraitTiny.Height,
            0.8 - tolerance, 0.8 + tolerance);

        // Standard portrait sizes all use 0.64 ratio
        Assert.InRange(ImageHelper.PortraitSmall.Width / ImageHelper.PortraitSmall.Height,
            expectedRatio - tolerance, expectedRatio + tolerance);
        Assert.InRange(ImageHelper.PortraitMedium.Width / ImageHelper.PortraitMedium.Height,
            expectedRatio - tolerance, expectedRatio + tolerance);
        Assert.InRange(ImageHelper.PortraitLarge.Width / ImageHelper.PortraitLarge.Height,
            expectedRatio - tolerance, expectedRatio + tolerance);
        Assert.InRange(ImageHelper.PortraitHuge.Width / ImageHelper.PortraitHuge.Height,
            expectedRatio - tolerance, expectedRatio + tolerance);
    }

    [Fact]
    public void PortraitAspectRatio_IsCorrect()
    {
        Assert.Equal(0.64, ImageHelper.PortraitAspectRatio);
    }

    #endregion

    #region CalculateFitSize

    [Fact]
    public void CalculateFitSize_SourceFitsInTarget_ReturnsScaledSize()
    {
        var source = new Size(100, 100);
        var target = new Size(200, 200);

        var result = ImageHelper.CalculateFitSize(source, target);

        // Should scale to fit within target while preserving aspect ratio
        Assert.Equal(200, result.Width);
        Assert.Equal(200, result.Height);
    }

    [Fact]
    public void CalculateFitSize_WideSource_ConstrainsByWidth()
    {
        var source = new Size(200, 100); // 2:1 aspect
        var target = new Size(100, 100);

        var result = ImageHelper.CalculateFitSize(source, target);

        // Should constrain by width
        Assert.Equal(100, result.Width);
        Assert.Equal(50, result.Height);
    }

    [Fact]
    public void CalculateFitSize_TallSource_ConstrainsByHeight()
    {
        var source = new Size(100, 200); // 1:2 aspect
        var target = new Size(100, 100);

        var result = ImageHelper.CalculateFitSize(source, target);

        // Should constrain by height
        Assert.Equal(50, result.Width);
        Assert.Equal(100, result.Height);
    }

    [Fact]
    public void CalculateFitSize_PreserveAspectFalse_ReturnsTargetSize()
    {
        var source = new Size(100, 200);
        var target = new Size(300, 300);

        var result = ImageHelper.CalculateFitSize(source, target, preserveAspect: false);

        Assert.Equal(300, result.Width);
        Assert.Equal(300, result.Height);
    }

    [Fact]
    public void CalculateFitSize_ZeroSourceSize_ReturnsTargetSize()
    {
        var source = new Size(0, 0);
        var target = new Size(100, 100);

        var result = ImageHelper.CalculateFitSize(source, target);

        Assert.Equal(target, result);
    }

    #endregion

    #region CalculatePortraitSize

    [Fact]
    public void CalculatePortraitSize_ReturnsCorrectAspectRatio()
    {
        var result = ImageHelper.CalculatePortraitSize(64);

        Assert.Equal(64, result.Width);
        // Height should be width / 0.64 = width * 1.5625
        Assert.Equal(100, result.Height); // 64/0.64 = 100
    }

    [Theory]
    [InlineData(32, 50)]   // 32/0.64 = 50
    [InlineData(64, 100)]  // 64/0.64 = 100
    [InlineData(128, 200)] // 128/0.64 = 200
    public void CalculatePortraitSize_VariousWidths_MaintainsAspectRatio(double width, double expectedHeight)
    {
        var result = ImageHelper.CalculatePortraitSize(width);

        Assert.Equal(width, result.Width);
        Assert.Equal(expectedHeight, result.Height);
    }

    #endregion

    #region GetNearestPortraitSize

    [Theory]
    [InlineData(16, 32, 40)]   // Very small -> Tiny
    [InlineData(32, 32, 40)]   // At Tiny threshold
    [InlineData(48, 32, 40)]   // Between Tiny and Small
    [InlineData(50, 64, 100)]  // Just above Tiny threshold -> Small
    [InlineData(64, 64, 100)]  // At Small
    [InlineData(96, 64, 100)]  // At Small threshold
    [InlineData(100, 128, 200)] // Just above Small -> Medium
    [InlineData(192, 128, 200)] // At Medium threshold
    [InlineData(200, 256, 400)] // Just above Medium -> Large
    [InlineData(384, 256, 400)] // At Large threshold
    [InlineData(400, 512, 800)] // Above Large -> Huge
    public void GetNearestPortraitSize_ReturnsCorrectSize(double width, double expectedWidth, double expectedHeight)
    {
        var result = ImageHelper.GetNearestPortraitSize(width);

        Assert.Equal(expectedWidth, result.Width);
        Assert.Equal(expectedHeight, result.Height);
    }

    #endregion

    #region Bitmap Utility Methods

    [Fact]
    public void IsValidBitmap_NullBitmap_ReturnsFalse()
    {
        Assert.False(ImageHelper.IsValidBitmap(null));
    }

    [Fact]
    public void GetAspectRatio_NullBitmap_ReturnsOne()
    {
        Assert.Equal(1.0, ImageHelper.GetAspectRatio(null));
    }

    [Fact]
    public void IsPortraitAspect_NullBitmap_ReturnsFalse()
    {
        Assert.False(ImageHelper.IsPortraitAspect(null));
    }

    #endregion

    #region Missing Portrait Placeholder

    // Note: Bitmap creation tests require Avalonia render platform.
    // These are tested via integration tests or require headless Avalonia setup.
    // Unit tests validate non-Bitmap behavior only.

    [Fact]
    public void ClearPlaceholderCache_DoesNotThrow_WhenEmpty()
    {
        // Clear should not throw even when cache is empty (no Avalonia required)
        var exception = Record.Exception(() => ImageHelper.ClearPlaceholderCache());
        Assert.Null(exception);
    }

    // Bitmap-dependent tests covered in integration tests:
    // - GetMissingPortraitPlaceholder_ReturnsNonNullBitmap
    // - GetMissingPortraitPlaceholder_DefaultsToSmallSize
    // - GetMissingPortraitPlaceholder_RespectsRequestedSize
    // - ResizeBitmap_* tests

    #endregion
}
