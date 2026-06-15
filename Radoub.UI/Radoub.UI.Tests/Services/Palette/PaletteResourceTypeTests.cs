using Radoub.UI.Services.Palette;
using Xunit;

namespace Radoub.UI.Tests.Services.Palette;

public class PaletteResourceTypeTests
{
    [Theory]
    [InlineData(PaletteResourceType.Item, "itempalcus.itp", "itempalstd.itp", "uti")]
    [InlineData(PaletteResourceType.Creature, "creaturepalcus.itp", "creaturepalstd.itp", "utc")]
    [InlineData(PaletteResourceType.Placeable, "placeablepalcus.itp", "placeablepalstd.itp", "utp")]
    [InlineData(PaletteResourceType.Store, "storepalcus.itp", "storepalstd.itp", "utm")]
    public void Descriptor_maps_filenames_and_extension(
        PaletteResourceType type, string custom, string skeleton, string ext)
    {
        var d = PaletteResourceTypeInfo.For(type);
        Assert.Equal(custom, d.CustomPaletteFile);
        Assert.Equal(skeleton, d.SkeletonPaletteFile);
        Assert.Equal(ext, d.BlueprintExtension);
    }
}
