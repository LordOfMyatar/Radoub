using ItemEditor.Services;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ItemEditor.Tests.Services;

public class CompositeWeaponPartCatalogServiceTests
{
    [Theory]
    [InlineData("wdbsw_b_011", "wdbsw", "b", 11)]
    [InlineData("wdbsw_b_021", "wdbsw", "b", 21)]
    [InlineData("wdbsw_m_031", "wdbsw", "m", 31)]
    [InlineData("wdbsw_t_001", "wdbsw", "t", 1)]
    [InlineData("WBLAD_B_011", "wblad", "b", 11)] // case-insensitive
    public void TryParseCompositeResRef_ExtractsItemClassAndPosition(string resRef, string itemClass, string position, int partNumber)
    {
        var ok = CompositeWeaponPartCatalogService.TryParseCompositeResRef(
            resRef, itemClass, position, out var parsed);

        Assert.True(ok);
        Assert.Equal(partNumber, parsed);
    }

    [Theory]
    [InlineData("wdbsw_b_011", "wblad", "b")] // wrong itemClass
    [InlineData("wdbsw_x_011", "wdbsw", "b")] // wrong position
    [InlineData("wdbsw_b_xyz", "wdbsw", "b")] // non-numeric suffix
    [InlineData("wdbsw_b", "wdbsw", "b")]     // missing suffix
    [InlineData("", "wdbsw", "b")]            // empty
    [InlineData("wdbsw_005", "wdbsw", "b")]   // simple (non-composite) naming
    public void TryParseCompositeResRef_RejectsBadFormats(string resRef, string itemClass, string position)
    {
        var ok = CompositeWeaponPartCatalogService.TryParseCompositeResRef(
            resRef, itemClass, position, out var parsed);

        Assert.False(ok);
        Assert.Equal(0, parsed);
    }

    [Fact]
    public void GetAvailableParts_FiltersByItemClassAndPosition_DeduplicatesAndSorts()
    {
        var resources = new[]
        {
            "wdbsw_b_021",
            "wdbsw_b_011",
            "wdbsw_b_031",
            "wdbsw_m_011",  // wrong position
            "wblad_b_011",  // wrong itemClass
            "wdbsw_b_011",  // duplicate
        };

        var parts = CompositeWeaponPartCatalogService
            .ExtractPartNumbers(resources, "wdbsw", "b")
            .ToList();

        // Expected: 11, 21, 31 (sorted asc, no duplicate)
        Assert.Equal(new[] { 11, 21, 31 }, parts);
    }

    [Fact]
    public void GetAvailableParts_EmptyItemClass_ReturnsEmpty()
    {
        var parts = CompositeWeaponPartCatalogService
            .ExtractPartNumbers(new[] { "wdbsw_b_011" }, "", "b");

        Assert.Empty(parts);
    }

    [Fact]
    public void GetAvailableParts_NullItemClass_ReturnsEmpty()
    {
        var parts = CompositeWeaponPartCatalogService
            .ExtractPartNumbers(new[] { "wdbsw_b_011" }, null, "b");

        Assert.Empty(parts);
    }

    [Fact]
    public void PositionForPartIndex_MapsPartNumbers()
    {
        Assert.Equal("b", CompositeWeaponPartCatalogService.PositionForPartIndex(1));
        Assert.Equal("m", CompositeWeaponPartCatalogService.PositionForPartIndex(2));
        Assert.Equal("t", CompositeWeaponPartCatalogService.PositionForPartIndex(3));
    }

    [Fact]
    public void PositionForPartIndex_OutOfRange_ReturnsNull()
    {
        Assert.Null(CompositeWeaponPartCatalogService.PositionForPartIndex(0));
        Assert.Null(CompositeWeaponPartCatalogService.PositionForPartIndex(4));
    }
}
