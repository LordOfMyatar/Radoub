using Radoub.UI.Services;
using Radoub.Formats.Plt;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for TextureService pure byte/pixel helpers (internal methods).
/// </summary>
public class TextureServiceHelperTests
{
    [Fact]
    public void SwapRedBlue_SwapsRAndBChannels()
    {
        // RGBA: R=10, G=20, B=30, A=40
        byte[] rgba = { 10, 20, 30, 40 };

        TextureService.SwapRedBlue(rgba);

        Assert.Equal(30, rgba[0]); // R ← B
        Assert.Equal(20, rgba[1]); // G unchanged
        Assert.Equal(10, rgba[2]); // B ← R
        Assert.Equal(40, rgba[3]); // A unchanged
    }

    [Fact]
    public void SwapRedBlue_MultiplePixels()
    {
        byte[] rgba = { 10, 20, 30, 40, 100, 110, 120, 130 };

        TextureService.SwapRedBlue(rgba);

        // Pixel 1
        Assert.Equal(30, rgba[0]);
        Assert.Equal(20, rgba[1]);
        Assert.Equal(10, rgba[2]);
        Assert.Equal(40, rgba[3]);
        // Pixel 2
        Assert.Equal(120, rgba[4]);
        Assert.Equal(110, rgba[5]);
        Assert.Equal(100, rgba[6]);
        Assert.Equal(130, rgba[7]);
    }

    [Fact]
    public void SwapRedBlue_EmptyArray_NoException()
    {
        byte[] rgba = { };
        TextureService.SwapRedBlue(rgba); // Should not throw
    }

    [Fact]
    public void FlipVertically_TwoRows_SwapsRows()
    {
        // 2x2 image: row 0 = [R,G,B,A, R,G,B,A], row 1 = [...]
        byte[] rgba = {
            10, 20, 30, 40, 11, 21, 31, 41, // row 0 (top)
            50, 60, 70, 80, 51, 61, 71, 81  // row 1 (bottom)
        };

        TextureService.FlipVertically(rgba, 2, 2);

        // After flip: row 0 should be old row 1, row 1 should be old row 0
        Assert.Equal(50, rgba[0]); // row 0 pixel 0 R = old row 1
        Assert.Equal(10, rgba[8]); // row 1 pixel 0 R = old row 0
    }

    [Fact]
    public void FlipVertically_SingleRow_NoChange()
    {
        byte[] rgba = { 10, 20, 30, 40 };
        var copy = (byte[])rgba.Clone();

        TextureService.FlipVertically(rgba, 1, 1);

        Assert.Equal(copy, rgba); // Should be unchanged
    }

    [Fact]
    public void BuildLayerColors_AllTenLayersMappedCorrectly()
    {
        var colors = new PltColorIndices
        {
            Skin = 1, Hair = 2, Metal1 = 3, Metal2 = 4,
            Cloth1 = 5, Cloth2 = 6, Leather1 = 7, Leather2 = 8,
            Tattoo1 = 9, Tattoo2 = 10
        };

        var result = TextureService.BuildLayerColors(colors);

        Assert.Equal(10, result.Count);
        Assert.Equal(1, result[PltLayers.Skin]);
        Assert.Equal(2, result[PltLayers.Hair]);
        Assert.Equal(3, result[PltLayers.Metal1]);
        Assert.Equal(4, result[PltLayers.Metal2]);
        Assert.Equal(5, result[PltLayers.Cloth1]);
        Assert.Equal(6, result[PltLayers.Cloth2]);
        Assert.Equal(7, result[PltLayers.Leather1]);
        Assert.Equal(8, result[PltLayers.Leather2]);
        Assert.Equal(9, result[PltLayers.Tattoo1]);
        Assert.Equal(10, result[PltLayers.Tattoo2]);
    }

    [Fact]
    public void ConvertBiowareDdsToStandard_TooShort_ReturnsNull()
    {
        byte[] tooShort = new byte[10];
        var result = TextureService.ConvertBiowareDdsToStandard(tooShort);
        Assert.Null(result);
    }

    [Fact]
    public void ConvertBiowareDdsToStandard_ValidHeader_ReturnsStandardDds()
    {
        // Build a minimal 20-byte BioWare DDS header:
        // uint width=4, uint height=4, byte channels=3, byte unused=0, short pitch=12, int alpha=0, short unknown=0
        var bioware = new byte[20 + 48]; // 20-byte header + minimal pixel data (4*12=48)
        using var ms = new System.IO.MemoryStream(bioware);
        using var bw = new System.IO.BinaryWriter(ms);
        bw.Write((uint)4);   // width
        bw.Write((uint)4);   // height
        bw.Write((byte)3);   // channels (RGB)
        bw.Write((byte)0);   // unused
        bw.Write((short)12); // pitch = width * channels
        bw.Write((int)0);    // alpha
        bw.Write((short)0);  // unknown

        var result = TextureService.ConvertBiowareDdsToStandard(bioware);

        // Should produce a valid DDS with "DDS " magic
        Assert.NotNull(result);
        Assert.Equal((byte)'D', result![0]);
        Assert.Equal((byte)'D', result[1]);
        Assert.Equal((byte)'S', result[2]);
        Assert.Equal((byte)' ', result[3]);
    }
}
