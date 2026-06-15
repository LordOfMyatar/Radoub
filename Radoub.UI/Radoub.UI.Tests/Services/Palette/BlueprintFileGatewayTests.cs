using System.IO;
using Radoub.Formats.Uti;
using Radoub.Formats.Utc;
using Radoub.Formats.Utp;
using Radoub.Formats.Utm;
using Radoub.UI.Services.Palette;
using Xunit;

namespace Radoub.UI.Tests.Services.Palette;

public class BlueprintFileGatewayTests
{
    private static string WriteMinimal(PaletteResourceType type, string dir, byte paletteId)
    {
        string ext = PaletteResourceTypeInfo.For(type).BlueprintExtension;
        string path = Path.Combine(dir, "a." + ext);
        switch (type)
        {
            case PaletteResourceType.Item:      UtiWriter.Write(new UtiFile { PaletteID = paletteId }, path); break;
            case PaletteResourceType.Creature:  UtcWriter.Write(new UtcFile { PaletteID = paletteId }, path); break;
            case PaletteResourceType.Placeable: UtpWriter.Write(new UtpFile { PaletteID = paletteId }, path); break;
            case PaletteResourceType.Store:     UtmWriter.Write(new UtmFile { PaletteID = paletteId }, path); break;
        }
        return path;
    }

    [Theory]
    [InlineData(PaletteResourceType.Item)]
    [InlineData(PaletteResourceType.Creature)]
    [InlineData(PaletteResourceType.Placeable)]
    [InlineData(PaletteResourceType.Store)]
    public void Gateway_reads_and_rewrites_palette_id(PaletteResourceType type)
    {
        string dir = Path.Combine(Path.GetTempPath(), "ucet_" + Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            string path = WriteMinimal(type, dir, 5);
            var gw = new BlueprintFileGateway(type);
            Assert.Equal((byte)5, gw.ReadPaletteId(path));

            byte[] rewritten = gw.ProduceBytesWithPaletteId(path, 9);
            Assert.Equal((byte)9, gw.ReadPaletteIdFromBytes(rewritten));
            // original file untouched until the save transaction commits
            Assert.Equal((byte)5, gw.ReadPaletteId(path));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
