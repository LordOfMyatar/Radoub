using Radoub.Formats.Plt;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Tests for PLT (Packed Layered Texture) reader.
/// </summary>
public class PltReaderTests
{
    [Fact]
    public void Read_ValidMinimalPlt_ReturnsCorrectDimensions()
    {
        // Create minimal valid PLT: 2x2 pixels
        var data = new byte[24 + 8]; // Header + 4 pixels * 2 bytes each

        // Signature "PLT "
        data[0] = (byte)'P';
        data[1] = (byte)'L';
        data[2] = (byte)'T';
        data[3] = (byte)' ';

        // Version "V1  "
        data[4] = (byte)'V';
        data[5] = (byte)'1';
        data[6] = (byte)' ';
        data[7] = (byte)' ';

        // Unused bytes 8-15

        // Width = 2 (little-endian at offset 16)
        data[16] = 2;
        data[17] = 0;
        data[18] = 0;
        data[19] = 0;

        // Height = 2 (little-endian at offset 20)
        data[20] = 2;
        data[21] = 0;
        data[22] = 0;
        data[23] = 0;

        // Pixel data: 4 pixels, each with grayscale and layer ID
        data[24] = 128; // Pixel 0 grayscale
        data[25] = 0;   // Pixel 0 layer (skin)
        data[26] = 200; // Pixel 1 grayscale
        data[27] = 1;   // Pixel 1 layer (hair)
        data[28] = 50;  // Pixel 2 grayscale
        data[29] = 2;   // Pixel 2 layer (metal1)
        data[30] = 255; // Pixel 3 grayscale
        data[31] = 4;   // Pixel 3 layer (cloth1)

        var result = PltReader.Read(data);

        Assert.Equal(2, result.Width);
        Assert.Equal(2, result.Height);
        Assert.Equal(4, result.Pixels.Length);
    }

    [Fact]
    public void Read_ValidPlt_ReturnsCorrectPixelData()
    {
        var data = CreateMinimalPlt(2, 2);

        // Set specific pixel values
        data[24] = 128; data[25] = 0;  // Pixel 0: grayscale 128, layer skin
        data[26] = 200; data[27] = 1;  // Pixel 1: grayscale 200, layer hair
        data[28] = 50;  data[29] = 2;  // Pixel 2: grayscale 50, layer metal1
        data[30] = 255; data[31] = 4;  // Pixel 3: grayscale 255, layer cloth1

        var result = PltReader.Read(data);

        Assert.Equal(128, result.Pixels[0].Grayscale);
        Assert.Equal(0, result.Pixels[0].LayerId);
        Assert.Equal(200, result.Pixels[1].Grayscale);
        Assert.Equal(1, result.Pixels[1].LayerId);
        Assert.Equal(50, result.Pixels[2].Grayscale);
        Assert.Equal(2, result.Pixels[2].LayerId);
        Assert.Equal(255, result.Pixels[3].Grayscale);
        Assert.Equal(4, result.Pixels[3].LayerId);
    }

    [Fact]
    public void Read_InvalidSignature_ThrowsArgumentException()
    {
        var data = CreateMinimalPlt(2, 2);
        data[0] = (byte)'X'; // Invalid signature

        Assert.Throws<ArgumentException>(() => PltReader.Read(data));
    }

    [Fact]
    public void Read_TooSmallData_ThrowsArgumentException()
    {
        var data = new byte[10]; // Too small for header

        Assert.Throws<ArgumentException>(() => PltReader.Read(data));
    }

    [Fact]
    public void Read_DataTooSmallForPixels_ThrowsArgumentException()
    {
        var data = CreateMinimalPlt(10, 10); // Would need 200 pixels
        Array.Resize(ref data, 24 + 10); // But only provide data for 5 pixels

        Assert.Throws<ArgumentException>(() => PltReader.Read(data));
    }

    [Fact]
    public void GetPaletteResRef_ReturnsCorrectNames()
    {
        Assert.Equal("pal_skin01", PltLayers.GetPaletteResRef(PltLayers.Skin));
        Assert.Equal("pal_hair01", PltLayers.GetPaletteResRef(PltLayers.Hair));
        Assert.Equal("pal_armor01", PltLayers.GetPaletteResRef(PltLayers.Metal1));
        Assert.Equal("pal_armor02", PltLayers.GetPaletteResRef(PltLayers.Metal2));
        Assert.Equal("pal_cloth01", PltLayers.GetPaletteResRef(PltLayers.Cloth1));
        Assert.Equal("pal_cloth02", PltLayers.GetPaletteResRef(PltLayers.Cloth2));
        Assert.Equal("pal_leath01", PltLayers.GetPaletteResRef(PltLayers.Leather1));
        Assert.Equal("pal_leath02", PltLayers.GetPaletteResRef(PltLayers.Leather2));
        Assert.Equal("pal_tattoo01", PltLayers.GetPaletteResRef(PltLayers.Tattoo1));
        Assert.Equal("pal_tattoo02", PltLayers.GetPaletteResRef(PltLayers.Tattoo2));
    }

    [Fact]
    public void Render_WithNoPalettes_ReturnsGrayscale()
    {
        var data = CreateMinimalPlt(2, 1);
        data[24] = 128; data[25] = 0;  // Pixel 0: grayscale 128
        data[26] = 255; data[27] = 0;  // Pixel 1: grayscale 255

        var plt = PltReader.Read(data);
        var palettes = new Dictionary<int, PaletteData>(); // No palettes
        var colorIndices = new Dictionary<int, int>();

        var pixels = PltReader.Render(plt, palettes, colorIndices);

        // First pixel should be grayscale 128
        Assert.Equal(128, pixels[0]); // R
        Assert.Equal(128, pixels[1]); // G
        Assert.Equal(128, pixels[2]); // B
        Assert.Equal(255, pixels[3]); // A

        // Second pixel should be grayscale 255
        Assert.Equal(255, pixels[4]); // R
        Assert.Equal(255, pixels[5]); // G
        Assert.Equal(255, pixels[6]); // B
        Assert.Equal(255, pixels[7]); // A
    }

    private static byte[] CreateMinimalPlt(int width, int height)
    {
        var size = 24 + (width * height * 2);
        var data = new byte[size];

        // Signature "PLT "
        data[0] = (byte)'P';
        data[1] = (byte)'L';
        data[2] = (byte)'T';
        data[3] = (byte)' ';

        // Version "V1  "
        data[4] = (byte)'V';
        data[5] = (byte)'1';
        data[6] = (byte)' ';
        data[7] = (byte)' ';

        // Width
        BitConverter.TryWriteBytes(data.AsSpan(16), (uint)width);

        // Height
        BitConverter.TryWriteBytes(data.AsSpan(20), (uint)height);

        return data;
    }
}
