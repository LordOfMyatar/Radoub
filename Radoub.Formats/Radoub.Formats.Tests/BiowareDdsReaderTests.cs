using Radoub.Formats.Dds;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Tests for the BioWare DDS reader. NWN:EE creature textures use BioWare's own DDS variant:
/// no "DDS " magic, a 16-byte header { u32 width, u32 height, u32 colors(3=DXT1,4=DXT5),
/// u32 reserved[2 - 1] }, followed by raw DXT1/DXT5 blocks. Standard Microsoft DDS (magic
/// "DDS ") is NOT handled here (Pfim owns that path). Layout per rollnw Image.cpp.
/// </summary>
public class BiowareDdsReaderTests
{
    /// <summary>
    /// Build a 16-byte BioWare DDS header (width, height, colors, then 4 reserved bytes
    /// to reach 16 — rollnw's struct is width/height/colors + reserved[2] = 20 bytes, but
    /// the pixel data offset it uses is sizeof(header). We mirror rollnw exactly: 20-byte header.
    /// </summary>
    private static byte[] Header(uint width, uint height, uint colors)
    {
        var h = new byte[20];
        BitConverter.GetBytes(width).CopyTo(h, 0);
        BitConverter.GetBytes(height).CopyTo(h, 4);
        BitConverter.GetBytes(colors).CopyTo(h, 8);
        // bytes 12..19 reserved (zero)
        return h;
    }

    [Fact]
    public void IsBiowareDds_StandardMicrosoftDds_ReturnsFalse()
    {
        // Microsoft DDS starts with the ASCII magic "DDS ".
        var msDds = new byte[] { (byte)'D', (byte)'D', (byte)'S', (byte)' ', 0, 0, 0, 0 };

        Assert.False(BiowareDdsReader.IsBiowareDds(msDds));
    }

    [Fact]
    public void Read_4x4Dxt1AllRed_DecodesToRedRgba()
    {
        // One 4x4 DXT1 block. Both endpoints = pure red in 565 (0xF800).
        // Index word = 0 => every texel selects color[0] = red.
        var header = Header(4, 4, 3); // colors=3 => DXT1
        var block = new byte[8];
        // c0 = 0xF800 (little-endian: 0x00, 0xF8)
        block[0] = 0x00; block[1] = 0xF8;
        // c1 = 0xF800
        block[2] = 0x00; block[3] = 0xF8;
        // index bits (bytes 4..7) all zero => all texels -> color index 0

        var data = new byte[header.Length + block.Length];
        header.CopyTo(data, 0);
        block.CopyTo(data, header.Length);

        var img = BiowareDdsReader.Read(data);

        Assert.NotNull(img);
        Assert.Equal(4, img!.Width);
        Assert.Equal(4, img.Height);
        Assert.Equal(4 * 4 * 4, img.Pixels.Length); // RGBA

        // Every pixel pure red, opaque.
        for (int i = 0; i < img.Pixels.Length; i += 4)
        {
            Assert.Equal(255, img.Pixels[i + 0]); // R
            Assert.Equal(0, img.Pixels[i + 1]);   // G
            Assert.Equal(0, img.Pixels[i + 2]);   // B
            Assert.Equal(255, img.Pixels[i + 3]); // A
        }
    }

    [Fact]
    public void Read_4x4Dxt5OpaqueRed_DecodesRedWithFullAlpha()
    {
        // DXT5 block = 8-byte alpha block + 8-byte color block.
        var header = Header(4, 4, 4); // colors=4 => DXT5

        var alpha = new byte[8];
        // alpha endpoints both 255 => every texel alpha 255 regardless of indices.
        alpha[0] = 255; alpha[1] = 255;

        var color = new byte[8];
        color[0] = 0x00; color[1] = 0xF8; // c0 = red 565
        color[2] = 0x00; color[3] = 0xF8; // c1 = red 565
        // index bits zero => color index 0 (red)

        var data = new byte[header.Length + alpha.Length + color.Length];
        header.CopyTo(data, 0);
        alpha.CopyTo(data, header.Length);
        color.CopyTo(data, header.Length + alpha.Length);

        var img = BiowareDdsReader.Read(data);

        Assert.NotNull(img);
        Assert.Equal(4, img!.Width);
        Assert.Equal(4, img.Height);
        for (int i = 0; i < img.Pixels.Length; i += 4)
        {
            Assert.Equal(255, img.Pixels[i + 0]); // R
            Assert.Equal(0, img.Pixels[i + 1]);   // G
            Assert.Equal(0, img.Pixels[i + 2]);   // B
            Assert.Equal(255, img.Pixels[i + 3]); // A
        }
    }

    [Fact]
    public void Read_TruncatedPixelData_ReturnsNull()
    {
        // Header claims 4x4 DXT1 (needs 8 bytes) but provides only 2.
        var header = Header(4, 4, 3);
        var data = new byte[header.Length + 2];
        header.CopyTo(data, 0);

        Assert.Null(BiowareDdsReader.Read(data));
    }
}
