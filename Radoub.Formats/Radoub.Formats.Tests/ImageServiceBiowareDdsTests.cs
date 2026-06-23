using Radoub.Formats.Services;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Integration tests: ImageService.DecodeImage("dds") must route BioWare-format DDS to the
/// BioWare decoder (Pfim only handles Microsoft DDS). This is the path the model preview uses
/// (#1765 — blurry textures were caused by these DDS failing to decode and falling back to TGA).
/// </summary>
public class ImageServiceBiowareDdsTests
{
    private static ImageService NewService() => new ImageService(new MockGameDataService(includeSampleData: false));

    /// <summary>4x4 DXT1 all-red BioWare DDS (20-byte header + one 8-byte block).</summary>
    private static byte[] BuildBiowareDxt1Red()
    {
        var data = new byte[20 + 8];
        BitConverter.GetBytes((uint)4).CopyTo(data, 0);  // width
        BitConverter.GetBytes((uint)4).CopyTo(data, 4);  // height
        BitConverter.GetBytes((uint)3).CopyTo(data, 8);  // colors => DXT1
        // block: c0 = c1 = red565 (0xF800), index bits 0
        data[20] = 0x00; data[21] = 0xF8;
        data[22] = 0x00; data[23] = 0xF8;
        return data;
    }

    [Fact]
    public void DecodeImage_BiowareDds_DecodesToHighResRgba()
    {
        var svc = NewService();

        var img = svc.DecodeImage(BuildBiowareDxt1Red(), "dds");

        Assert.NotNull(img);
        Assert.Equal(4, img!.Width);
        Assert.Equal(4, img.Height);
        Assert.Equal(255, img.Pixels[0]); // R
        Assert.Equal(0, img.Pixels[1]);   // G
        Assert.Equal(0, img.Pixels[2]);   // B
        Assert.Equal(255, img.Pixels[3]); // A
    }
}
