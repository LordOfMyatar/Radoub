using System.Linq;
using PlaceableEditor.ViewModels;

namespace PlaceableEditor.Tests.ViewModels;

/// <summary>
/// The Initial State combo (#2376) lists the engine-fixed placeable animation states from
/// BioWare's Door/Placeable GFF spec Table 4.1.2 (default/open/closed/destroyed/activated/
/// deactivated = 0..5). These values are engine-internal, not 2DA/module data, so a static
/// catalog is authoritative rather than hardcoded game data.
/// </summary>
public class PlaceableAnimationStateTests
{
    [Fact]
    public void Catalog_HasSixStatesInValueOrder()
    {
        var states = PlaceableAnimationState.All;

        Assert.Equal(6, states.Count);
        Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5 }, states.Select(s => s.Value).ToArray());
    }

    [Theory]
    [InlineData(0, "Default")]
    [InlineData(1, "Open")]
    [InlineData(2, "Closed")]
    [InlineData(3, "Destroyed")]
    [InlineData(4, "Activated")]
    [InlineData(5, "Deactivated")]
    public void Catalog_MapsValueToFriendlyName(byte value, string expectedName)
    {
        var state = PlaceableAnimationState.All.Single(s => s.Value == value);
        Assert.Equal(expectedName, state.Name);
    }

    [Fact]
    public void Display_CombinesNameAndValue()
    {
        var open = PlaceableAnimationState.All.Single(s => s.Value == 1);
        Assert.Equal("Open (1)", open.Display);
    }
}
