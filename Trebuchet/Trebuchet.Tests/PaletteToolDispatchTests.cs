using Radoub.UI.Services.Palette;
using RadoubLauncher.Controls;
using Xunit;

namespace Trebuchet.Tests;

/// <summary>
/// The palette editor double-click dispatch (#2485): each blueprint resource type launches the
/// correct Radoub tool (.uti→Relique, .utc→Quartermaster, .utp→Reliquary, .utm→Fence).
/// </summary>
public class PaletteToolDispatchTests
{
    [Theory]
    [InlineData(PaletteResourceType.Item, "Relique")]
    [InlineData(PaletteResourceType.Creature, "Quartermaster")]
    [InlineData(PaletteResourceType.Placeable, "Reliquary")]
    [InlineData(PaletteResourceType.Store, "Fence")]
    public void ToolNameFor_maps_each_type_to_its_editor(PaletteResourceType type, string expected)
    {
        Assert.Equal(expected, PaletteToolDispatch.ToolNameFor(type));
    }
}
