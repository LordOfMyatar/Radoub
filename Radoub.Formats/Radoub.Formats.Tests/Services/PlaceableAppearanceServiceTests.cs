using Radoub.Formats.Services;
using Radoub.Formats.TwoDA;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace Radoub.Formats.Tests.Services;

/// <summary>
/// Tests for <see cref="PlaceableAppearanceService"/> — placeables.2da
/// model/display-name resolution (#2291, Reliquary epic #2289 pre-work).
/// </summary>
public class PlaceableAppearanceServiceTests
{
    // placeables.2da columns used by the service: LABEL, ModelName, StrRef
    private static MockGameDataService FakeGameData()
    {
        var mock = new MockGameDataService(includeSampleData: false);
        var twoDA = new TwoDAFile { Columns = new() { "LABEL", "ModelName", "StrRef" } };
        // Row 0 (id 0): Barrel — has StrRef 1000 → TLK name wins
        twoDA.Rows.Add(new TwoDARow { Values = new() { "Barrel", "plc_barrel", "1000" } });
        // Rows 1..63 padding so row index 64 is addressable (placeables.2da is dense by id)
        for (int i = 1; i < 64; i++)
            twoDA.Rows.Add(new TwoDARow { Values = new() { "****", "****", "****" } });
        // Row 64 (id 64): Boulder — StrRef "****" → LABEL fallback
        twoDA.Rows.Add(new TwoDARow { Values = new() { "Boulder", "plc_boulder01", "****" } });
        mock.With2DA("placeables", twoDA);
        mock.WithString(1000, "Barrel");
        return mock;
    }

    [Fact]
    public void GetModelName_ReturnsModelColumn()
        => Assert.Equal("plc_boulder01", new PlaceableAppearanceService(FakeGameData()).GetModelName(64));

    [Fact]
    public void GetDisplayName_FallsBackToLabelWhenStrRefMissing()
        => Assert.Equal("Boulder", new PlaceableAppearanceService(FakeGameData()).GetDisplayName(64));

    [Fact]
    public void GetDisplayName_PrefersTlkOverLabel()
        => Assert.Equal("Barrel", new PlaceableAppearanceService(FakeGameData()).GetDisplayName(0));

    [Fact]
    public void GetById_UnknownId_ReturnsNull()
        => Assert.Null(new PlaceableAppearanceService(FakeGameData()).GetById(9999));

    [Fact]
    public void GetAll_SkipsPaddingRows()
    {
        // GetAll filters rows whose LABEL is "****"/empty → 2 real entries (Barrel, Boulder)
        Assert.Equal(2, new PlaceableAppearanceService(FakeGameData()).GetAll().Count);
    }
}
