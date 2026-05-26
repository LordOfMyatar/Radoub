using ItemEditor.Services;
using Radoub.TestUtilities.Mocks;
using System.Linq;
using Xunit;

namespace ItemEditor.Tests.Services;

public class ArmorPartCatalogServiceTests
{
    private static MockGameDataService BuildMockWithParts2DA(string twoDAName, params (int row, string? acBonus)[] rows)
    {
        var mock = new MockGameDataService(includeSampleData: false);
        foreach (var (row, acBonus) in rows)
        {
            mock.Set2DAValue(twoDAName, row, "ACBONUS", acBonus ?? "****");
        }
        return mock;
    }

    [Fact]
    public void GetAvailableParts_FiltersRowsWithEmptyACBONUS()
    {
        var mock = BuildMockWithParts2DA("parts_chest",
            (0, null),   // **** filtered out
            (1, "0"),
            (2, null),   // **** filtered out
            (3, "1"));
        var svc = new ArmorPartCatalogService(mock);

        var parts = svc.GetAvailableParts("Torso").ToList();

        Assert.Equal(2, parts.Count);
        Assert.Equal(1, parts[0].RowIndex);
        Assert.Equal(3, parts[1].RowIndex);
    }

    [Fact]
    public void GetAvailableParts_SortsByACBONUSAscThenRowAsc()
    {
        var mock = BuildMockWithParts2DA("parts_chest",
            (0, "2"),
            (1, "0"),
            (2, "1"),
            (3, "0"));
        var svc = new ArmorPartCatalogService(mock);

        var parts = svc.GetAvailableParts("Torso").Select(p => p.RowIndex).ToList();

        // Expected order: row 1 (ac=0), row 3 (ac=0), row 2 (ac=1), row 0 (ac=2)
        Assert.Equal(new[] { 1, 3, 2, 0 }, parts);
    }

    [Fact]
    public void GetAvailableParts_AssignsSequentialDisplayIndex_StartingAt1()
    {
        var mock = BuildMockWithParts2DA("parts_chest",
            (0, "0"),
            (1, "1"),
            (2, "2"));
        var svc = new ArmorPartCatalogService(mock);

        var parts = svc.GetAvailableParts("Torso").ToList();

        Assert.Equal(1, parts[0].DisplayIndex);
        Assert.Equal(2, parts[1].DisplayIndex);
        Assert.Equal(3, parts[2].DisplayIndex);
    }

    [Theory]
    [InlineData("Neck", "parts_neck")]
    [InlineData("Torso", "parts_chest")]
    [InlineData("Belt", "parts_belt")]
    [InlineData("Pelvis", "parts_pelvis")]
    [InlineData("RShoul", "parts_shoulder")]
    [InlineData("LShoul", "parts_shoulder")]
    [InlineData("RBicep", "parts_bicep")]
    [InlineData("LBicep", "parts_bicep")]
    [InlineData("RFArm", "parts_forearm")]
    [InlineData("LFArm", "parts_forearm")]
    [InlineData("RHand", "parts_hand")]
    [InlineData("LHand", "parts_hand")]
    [InlineData("RThigh", "parts_legs")]
    [InlineData("LThigh", "parts_legs")]
    [InlineData("RShin", "parts_shin")]
    [InlineData("LShin", "parts_shin")]
    [InlineData("RFoot", "parts_foot")]
    [InlineData("LFoot", "parts_foot")]
    [InlineData("Robe", "parts_robe")]
    public void ResolveTwoDAName_MapsPartNameToCorrect2DA(string partName, string expected2DA)
    {
        Assert.Equal(expected2DA, ArmorPartCatalogService.Resolve2DAName(partName));
    }

    [Fact]
    public void ResolveTwoDAName_UnknownPartName_ReturnsNull()
    {
        Assert.Null(ArmorPartCatalogService.Resolve2DAName("DoesNotExist"));
    }

    [Fact]
    public void GetAvailableParts_UnknownPartName_ReturnsEmpty()
    {
        var mock = new MockGameDataService(includeSampleData: false);
        var svc = new ArmorPartCatalogService(mock);

        Assert.Empty(svc.GetAvailableParts("BogusPart"));
    }

    [Fact]
    public void GetAvailableParts_Missing2DA_ReturnsEmpty()
    {
        var mock = new MockGameDataService(includeSampleData: false);
        var svc = new ArmorPartCatalogService(mock);

        Assert.Empty(svc.GetAvailableParts("Torso"));
    }

    [Fact]
    public void FormatDisplay_Default_OmitsAcBonus()
    {
        // Per engine behavior, only the Torso (parts_chest) ACBONUS contributes to item AC.
        // Showing (AC +X) on non-Torso slots is misleading. Default format omits it (#2164 v2).
        var entry = new ArmorPartEntry(RowIndex: 11, DisplayIndex: 1, ACBonus: 0);

        Assert.Equal("Part 1 — ID 11", entry.ToDisplayString());
    }

    [Fact]
    public void FormatDisplay_IncludeAc_ShowsAcBonus()
    {
        // Torso row opts in: passes includeAcBonus=true so the AC contribution is visible
        // where it actually matters.
        var entry = new ArmorPartEntry(RowIndex: 11, DisplayIndex: 1, ACBonus: 2);

        Assert.Equal("Part 1 — ID 11 (AC +2)", entry.ToDisplayString(includeAcBonus: true));
    }

    [Fact]
    public void FormatDisplay_IncludeAc_NegativeBonus_ShowsSign()
    {
        var entry = new ArmorPartEntry(RowIndex: 5, DisplayIndex: 3, ACBonus: -1);

        Assert.Equal("Part 3 — ID 5 (AC -1)", entry.ToDisplayString(includeAcBonus: true));
    }

    [Fact]
    public void FormatDisplay_IncludeAc_FractionalRounds()
    {
        var entry = new ArmorPartEntry(RowIndex: 7, DisplayIndex: 2, ACBonus: 0.5);

        Assert.Equal("Part 2 — ID 7 (AC +1)", entry.ToDisplayString(includeAcBonus: true));
    }

    [Fact]
    public void FormatDisplay_HandlesThreeDigitRowIndex()
    {
        var entry = new ArmorPartEntry(RowIndex: 123, DisplayIndex: 42, ACBonus: 5);

        Assert.Equal("Part 42 — ID 123", entry.ToDisplayString());
    }

    [Fact]
    public void GetAvailableParts_NonNumericACBONUS_TreatedAsZero()
    {
        // Defensive: malformed 2DA cells shouldn't crash.
        var mock = BuildMockWithParts2DA("parts_chest",
            (0, "notanumber"),
            (1, "0"));
        var svc = new ArmorPartCatalogService(mock);

        var parts = svc.GetAvailableParts("Torso").ToList();

        Assert.Equal(2, parts.Count);
        // Both treated as AC=0, sort by row index: row 0 first
        Assert.Equal(0, parts[0].RowIndex);
        Assert.Equal(1, parts[1].RowIndex);
    }
}
