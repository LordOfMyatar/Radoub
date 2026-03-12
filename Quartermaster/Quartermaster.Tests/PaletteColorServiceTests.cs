using Avalonia.Media;
using Quartermaster.Services;
using Radoub.Formats.Common;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for PaletteColorService — NWN palette TGA color extraction.
///
/// NWN palettes are 256x256 TGA images where:
///   - Each row (Y) is a color index (0-255)
///   - Each column (X) is a shading variant (0=darkest, 255=lightest)
///   - X=127 is used as the "representative" color for a given index
///
/// Desired behaviors:
///   - Return the correct color from a palette TGA for a given index
///   - Return gray fallback when palette resource is unavailable
///   - Cache palettes so the same TGA isn't parsed repeatedly
///   - Generate gradient stops by sampling across the X-axis
///   - Handle edge cases: numStops less than 2, out-of-bounds color index
/// </summary>
public class PaletteColorServiceTests
{
    private readonly MockGameDataService _mockGameData;

    public PaletteColorServiceTests()
    {
        _mockGameData = new MockGameDataService(includeSampleData: false);
    }

    /// <summary>
    /// Creates a minimal uncompressed 32-bit RGBA TGA with controllable pixel data.
    /// TGA stores pixels as BGRA in bottom-to-top order by default.
    /// </summary>
    private static byte[] CreateTestTga(int width, int height, Func<int, int, (byte r, byte g, byte b, byte a)> pixelFunc)
    {
        // TGA header: 18 bytes
        var data = new byte[18 + width * height * 4];

        // Header
        data[0] = 0;   // ID length
        data[1] = 0;   // No color map
        data[2] = 2;   // Uncompressed true-color
        // Bytes 3-11: color map spec + origin (all zeros)
        data[12] = (byte)(width & 0xFF);
        data[13] = (byte)(width >> 8);
        data[14] = (byte)(height & 0xFF);
        data[15] = (byte)(height >> 8);
        data[16] = 32;  // 32-bit (BGRA)
        data[17] = 0x20; // Top-to-bottom origin (bit 5 set)

        // Pixel data — TGA stores BGRA, top-to-bottom since we set bit 5
        int offset = 18;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var (r, g, b, a) = pixelFunc(x, y);
                data[offset++] = b;  // B
                data[offset++] = g;  // G
                data[offset++] = r;  // R
                data[offset++] = a;  // A
            }
        }

        return data;
    }

    /// <summary>
    /// Creates a 256x256 test palette where the representative color (X=127)
    /// for row Y is (Y, Y/2, Y/3, 255).
    /// </summary>
    private static byte[] CreateTestPalette()
    {
        return CreateTestTga(256, 256, (x, y) =>
        {
            // Create a gradient across X for each row Y
            byte r = (byte)((y + x) % 256);
            byte g = (byte)(y / 2);
            byte b = (byte)(y / 3);
            return (r, g, b, 255);
        });
    }

    private PaletteColorService CreateServiceWithPalette(string paletteName = "pal_skin01")
    {
        var tgaData = CreateTestPalette();
        _mockGameData.SetResource(paletteName, ResourceTypes.Tga, tgaData);
        return new PaletteColorService(_mockGameData);
    }

    private PaletteColorService CreateServiceWithoutPalette()
    {
        return new PaletteColorService(_mockGameData);
    }

    #region GetPaletteColor — Palette Found

    [Fact]
    public void GetPaletteColor_ValidPalette_ReturnsColorFromTga()
    {
        var service = CreateServiceWithPalette();

        var color = service.GetPaletteColor("pal_skin01", 0);

        // Row 0, X=127: r = (0+127)%256 = 127, g = 0/2 = 0, b = 0/3 = 0, a = 255
        Assert.Equal(127, color.R);
        Assert.Equal(0, color.G);
        Assert.Equal(0, color.B);
        Assert.Equal(255, color.A);
    }

    [Fact]
    public void GetPaletteColor_DifferentRow_ReturnsCorrectColor()
    {
        var service = CreateServiceWithPalette();

        var color = service.GetPaletteColor("pal_skin01", 100);

        // Row 100, X=127: r = (100+127)%256 = 227, g = 100/2 = 50, b = 100/3 = 33
        Assert.Equal(227, color.R);
        Assert.Equal(50, color.G);
        Assert.Equal(33, color.B);
        Assert.Equal(255, color.A);
    }

    #endregion

    #region GetPaletteColor — Palette Not Found

    [Fact]
    public void GetPaletteColor_NoPalette_ReturnsGray()
    {
        var service = CreateServiceWithoutPalette();

        var color = service.GetPaletteColor("pal_skin01", 0);

        Assert.Equal(Colors.Gray, color);
    }

    [Fact]
    public void GetPaletteColor_UnknownPaletteName_ReturnsGray()
    {
        var service = CreateServiceWithPalette("pal_skin01");

        var color = service.GetPaletteColor("pal_nonexistent", 0);

        Assert.Equal(Colors.Gray, color);
    }

    #endregion

    #region Convenience Methods

    [Fact]
    public void GetSkinColor_UsesSkinPalette()
    {
        var tga = CreateTestPalette();
        _mockGameData.SetResource("pal_skin01", ResourceTypes.Tga, tga);
        var service = new PaletteColorService(_mockGameData);

        var color = service.GetSkinColor(0);

        // Should use pal_skin01 — same as GetPaletteColor("pal_skin01", 0)
        Assert.NotEqual(Colors.Gray, color);
        Assert.Equal(255, color.A);
    }

    [Fact]
    public void GetHairColor_UsesHairPalette()
    {
        var tga = CreateTestPalette();
        _mockGameData.SetResource("pal_hair01", ResourceTypes.Tga, tga);
        var service = new PaletteColorService(_mockGameData);

        var color = service.GetHairColor(10);

        Assert.NotEqual(Colors.Gray, color);
    }

    [Fact]
    public void GetTattooColor_UsesTattooPalette()
    {
        var tga = CreateTestPalette();
        _mockGameData.SetResource("pal_tattoo01", ResourceTypes.Tga, tga);
        var service = new PaletteColorService(_mockGameData);

        var color = service.GetTattooColor(5);

        Assert.NotEqual(Colors.Gray, color);
    }

    #endregion

    #region GetPaletteGradient

    [Fact]
    public void GetPaletteGradient_ValidPalette_ReturnsRequestedStops()
    {
        var service = CreateServiceWithPalette();

        var stops = service.GetPaletteGradient("pal_skin01", 0, numStops: 8);

        Assert.Equal(8, stops.Count);
    }

    [Fact]
    public void GetPaletteGradient_StopsSpanFullRange()
    {
        var service = CreateServiceWithPalette();

        var stops = service.GetPaletteGradient("pal_skin01", 0, numStops: 3);

        // First stop at 0.0, last stop at 1.0
        Assert.Equal(0.0, stops[0].offset, 5);
        Assert.Equal(1.0, stops[2].offset, 5);
    }

    [Fact]
    public void GetPaletteGradient_MiddleStopAtCorrectOffset()
    {
        var service = CreateServiceWithPalette();

        var stops = service.GetPaletteGradient("pal_skin01", 0, numStops: 3);

        Assert.Equal(0.5, stops[1].offset, 5);
    }

    [Fact]
    public void GetPaletteGradient_NoPalette_ReturnsGrayGradient()
    {
        var service = CreateServiceWithoutPalette();

        var stops = service.GetPaletteGradient("pal_skin01", 0);

        Assert.Equal(2, stops.Count);
        Assert.Equal(Colors.DarkGray, stops[0].color);
        Assert.Equal(Colors.LightGray, stops[1].color);
    }

    [Fact]
    public void GetPaletteGradient_TwoStops_ReturnsStartAndEnd()
    {
        var service = CreateServiceWithPalette();

        var stops = service.GetPaletteGradient("pal_skin01", 0, numStops: 2);

        Assert.Equal(2, stops.Count);
        Assert.Equal(0.0, stops[0].offset, 5);
        Assert.Equal(1.0, stops[1].offset, 5);
    }

    [Fact]
    public void GetPaletteGradient_OneStop_DoesNotCrash()
    {
        // numStops=1 causes division by zero in offset calculation: i / (numStops - 1) = 0/0
        // This is a known edge case — should handle gracefully
        var service = CreateServiceWithPalette();

        // Should not throw — NaN offset is nonsensical but not a crash
        var stops = service.GetPaletteGradient("pal_skin01", 0, numStops: 1);

        Assert.Single(stops);
    }

    [Fact]
    public void GetPaletteGradient_ZeroStops_ReturnsEmpty()
    {
        var service = CreateServiceWithPalette();

        var stops = service.GetPaletteGradient("pal_skin01", 0, numStops: 0);

        Assert.Empty(stops);
    }

    #endregion

    #region CreateGradientBrush

    [Fact]
    public void CreateGradientBrush_ValidPalette_ReturnsBrushWithStops()
    {
        var service = CreateServiceWithPalette();

        var brush = service.CreateGradientBrush("pal_skin01", 0);

        Assert.NotNull(brush);
        Assert.Equal(8, brush.GradientStops.Count); // Default numStops = 8
    }

    [Fact]
    public void CreateGradientBrush_NoPalette_ReturnsBrushWithGrayStops()
    {
        var service = CreateServiceWithoutPalette();

        var brush = service.CreateGradientBrush("pal_skin01", 0);

        Assert.NotNull(brush);
        Assert.Equal(2, brush.GradientStops.Count); // Gray fallback has 2 stops
    }

    [Fact]
    public void CreateGradientBrush_IsHorizontal()
    {
        var service = CreateServiceWithPalette();

        var brush = service.CreateGradientBrush("pal_skin01", 0);

        // Horizontal gradient: start Y=0.5, end Y=0.5
        Assert.Equal(0.5, brush.StartPoint.Point.Y, 5);
        Assert.Equal(0.5, brush.EndPoint.Point.Y, 5);
        Assert.Equal(0, brush.StartPoint.Point.X, 5);
        Assert.Equal(1, brush.EndPoint.Point.X, 5);
    }

    #endregion

    #region Caching

    [Fact]
    public void GetPaletteColor_SamePaletteTwice_ReturnsSameResult()
    {
        var service = CreateServiceWithPalette();

        var color1 = service.GetPaletteColor("pal_skin01", 50);
        var color2 = service.GetPaletteColor("pal_skin01", 50);

        Assert.Equal(color1, color2);
    }

    [Fact]
    public void ClearCache_ThenGetColor_ReloadsFromResource()
    {
        var service = CreateServiceWithPalette();

        // Load palette into cache
        var color1 = service.GetPaletteColor("pal_skin01", 0);
        Assert.NotEqual(Colors.Gray, color1);

        // Clear cache and remove resource
        service.ClearCache();
        // Resource still exists, so re-fetch should work
        var color2 = service.GetPaletteColor("pal_skin01", 0);
        Assert.Equal(color1, color2);
    }

    [Fact]
    public void ClearCache_WhenEmpty_DoesNotThrow()
    {
        var service = CreateServiceWithoutPalette();

        // Should be idempotent
        service.ClearCache();
        service.ClearCache();
    }

    #endregion

    #region Palette Constants

    [Fact]
    public void PaletteConstants_AreCorrectFileNames()
    {
        Assert.Equal("pal_skin01", PaletteColorService.Palettes.Skin);
        Assert.Equal("pal_hair01", PaletteColorService.Palettes.Hair);
        Assert.Equal("pal_tattoo01", PaletteColorService.Palettes.Tattoo1);
        Assert.Equal("pal_tattoo01", PaletteColorService.Palettes.Tattoo2);
    }

    [Fact]
    public void Tattoo1And2_UseSamePalette()
    {
        // Both tattoo channels use the same palette file in NWN
        Assert.Equal(PaletteColorService.Palettes.Tattoo1, PaletteColorService.Palettes.Tattoo2);
    }

    #endregion
}
