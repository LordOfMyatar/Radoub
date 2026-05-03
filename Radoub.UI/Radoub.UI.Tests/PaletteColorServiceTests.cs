using Avalonia.Media;
using Radoub.Formats.Common;
using Radoub.TestUtilities.Mocks;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for PaletteColorService — NWN palette TGA color extraction.
/// Migrated from Quartermaster.Tests and extended with item palette constants.
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
        var data = new byte[18 + width * height * 4];

        data[0] = 0;   // ID length
        data[1] = 0;   // No color map
        data[2] = 2;   // Uncompressed true-color
        data[12] = (byte)(width & 0xFF);
        data[13] = (byte)(width >> 8);
        data[14] = (byte)(height & 0xFF);
        data[15] = (byte)(height >> 8);
        data[16] = 32;  // 32-bit (BGRA)
        data[17] = 0x20; // Top-to-bottom origin (bit 5 set)

        int offset = 18;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var (r, g, b, a) = pixelFunc(x, y);
                data[offset++] = b;
                data[offset++] = g;
                data[offset++] = r;
                data[offset++] = a;
            }
        }

        return data;
    }

    private static byte[] CreateTestPalette()
    {
        return CreateTestTga(256, 256, (x, y) =>
        {
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

    #region GetPaletteColor

    [Fact]
    public void GetPaletteColor_ValidPalette_ReturnsColorFromTga()
    {
        var service = CreateServiceWithPalette();
        var color = service.GetPaletteColor("pal_skin01", 0);

        Assert.Equal(127, color.R);
        Assert.Equal(0, color.G);
        Assert.Equal(0, color.B);
        Assert.Equal(255, color.A);
    }

    [Fact]
    public void GetPaletteColor_NoPalette_ReturnsGray()
    {
        var service = CreateServiceWithoutPalette();
        var color = service.GetPaletteColor("pal_skin01", 0);
        Assert.Equal(Colors.Gray, color);
    }

    #endregion

    #region Character Palette Constants

    [Fact]
    public void CharacterPaletteConstants_AreCorrectFileNames()
    {
        Assert.Equal("pal_skin01", PaletteColorService.Palettes.Skin);
        Assert.Equal("pal_hair01", PaletteColorService.Palettes.Hair);
        Assert.Equal("pal_tattoo01", PaletteColorService.Palettes.Tattoo1);
        Assert.Equal("pal_tattoo01", PaletteColorService.Palettes.Tattoo2);
    }

    #endregion

    #region Item Palette Constants

    [Fact]
    public void ItemPaletteConstants_AreCorrectFileNames()
    {
        // Per Aurora item format spec Section 2.1.2.4: each material has ONE palette
        // file shared between its "1" and "2" color slots. There is no pal_*02 in NWN.
        Assert.Equal("pal_cloth01", PaletteColorService.Palettes.Cloth1);
        Assert.Equal("pal_cloth01", PaletteColorService.Palettes.Cloth2);
        Assert.Equal("pal_leath01", PaletteColorService.Palettes.Leather1);
        Assert.Equal("pal_leath01", PaletteColorService.Palettes.Leather2);
        Assert.Equal("pal_armor01", PaletteColorService.Palettes.Metal1);
        Assert.Equal("pal_armor01", PaletteColorService.Palettes.Metal2);
    }

    [Fact]
    public void GetPaletteColor_ItemPalette_ReturnsColorFromTga()
    {
        var tgaData = CreateTestPalette();
        _mockGameData.SetResource("pal_cloth01", ResourceTypes.Tga, tgaData);
        var service = new PaletteColorService(_mockGameData);

        var color = service.GetPaletteColor(PaletteColorService.Palettes.Cloth1, 50);

        Assert.NotEqual(Colors.Gray, color);
        Assert.Equal(255, color.A);
    }

    [Fact]
    public void CreateGradientBrush_ItemPalette_ReturnsBrushWithStops()
    {
        var tgaData = CreateTestPalette();
        _mockGameData.SetResource("pal_armor01", ResourceTypes.Tga, tgaData);
        var service = new PaletteColorService(_mockGameData);

        var brush = service.CreateGradientBrush(PaletteColorService.Palettes.Metal1, 10);

        Assert.NotNull(brush);
        Assert.Equal(8, brush.GradientStops.Count);
    }

    #endregion

    #region Convenience Methods

    [Fact]
    public void GetSkinColor_UsesSkinPalette()
    {
        var service = CreateServiceWithPalette("pal_skin01");
        var color = service.GetSkinColor(0);
        Assert.NotEqual(Colors.Gray, color);
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
    public void GetPaletteGradient_NoPalette_ReturnsGrayGradient()
    {
        var service = CreateServiceWithoutPalette();
        var stops = service.GetPaletteGradient("pal_skin01", 0);
        Assert.Equal(2, stops.Count);
        Assert.Equal(Colors.DarkGray, stops[0].color);
        Assert.Equal(Colors.LightGray, stops[1].color);
    }

    [Fact]
    public void GetPaletteGradient_OneStop_ClampsToTwo()
    {
        var service = CreateServiceWithPalette();
        var stops = service.GetPaletteGradient("pal_skin01", 0, numStops: 1);
        Assert.Equal(2, stops.Count);
    }

    #endregion

    #region CreateGradientBrush

    [Fact]
    public void CreateGradientBrush_ValidPalette_ReturnsBrushWithStops()
    {
        var service = CreateServiceWithPalette();
        var brush = service.CreateGradientBrush("pal_skin01", 0);
        Assert.NotNull(brush);
        Assert.Equal(8, brush.GradientStops.Count);
    }

    [Fact]
    public void CreateGradientBrush_IsHorizontal()
    {
        var service = CreateServiceWithPalette();
        var brush = service.CreateGradientBrush("pal_skin01", 0);
        Assert.Equal(0.5, brush.StartPoint.Point.Y, 5);
        Assert.Equal(0.5, brush.EndPoint.Point.Y, 5);
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
    public void ClearCache_WhenEmpty_DoesNotThrow()
    {
        var service = CreateServiceWithoutPalette();
        service.ClearCache();
        service.ClearCache();
    }

    #endregion
}
