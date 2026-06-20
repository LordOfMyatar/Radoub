using System.Linq;
using PlaceableEditor.Services;
using Radoub.Formats.TwoDA;
using Radoub.TestUtilities.Mocks;
using Radoub.UI.Services;
using Xunit;

namespace PlaceableEditor.Tests;

/// <summary>
/// Tests for ReliquaryPortraitBrowserContext.ListPortraits dedupe (#2329).
/// portraits.2da repeats the same BaseResRef across race/sex variant rows; the
/// browser must list each portrait once. Mirrors Quartermaster's context.
/// </summary>
public class PortraitBrowserContextTests
{
    private static ReliquaryPortraitBrowserContext BuildContext(MockGameDataService mock)
        => new ReliquaryPortraitBrowserContext(mock, new ItemIconService(mock));

    [Fact]
    public void ListPortraits_DedupesRepeatedBaseResRef()
    {
        var mock = new MockGameDataService(includeSampleData: false);
        var twoDA = new TwoDAFile { Columns = new() { "BaseResRef", "Race", "Sex" } };
        twoDA.Rows.Add(new TwoDARow { Values = new() { "hu_m_01_", "6", "0" } });
        twoDA.Rows.Add(new TwoDARow { Values = new() { "hu_m_01_", "4", "0" } }); // dup
        twoDA.Rows.Add(new TwoDARow { Values = new() { "el_f_02_", "1", "1" } });
        mock.With2DA("portraits", twoDA);

        var result = BuildContext(mock).ListPortraits().ToList();

        Assert.Equal(2, result.Count);
        Assert.Single(result, p => p.ResRef == "hu_m_01_");
        Assert.Single(result, p => p.ResRef == "el_f_02_");
    }

    [Fact]
    public void ListPortraits_DuplicateWithDifferingRace_CollapsesToAllRaces()
    {
        // Duplicate ResRef rows that disagree on Race must collapse to -1 so a
        // race pre-filter can't hide a portrait a later row marks valid (#2329).
        var mock = new MockGameDataService(includeSampleData: false);
        var twoDA = new TwoDAFile { Columns = new() { "BaseResRef", "Race", "Sex" } };
        twoDA.Rows.Add(new TwoDARow { Values = new() { "po_shared_", "1", "0" } });
        twoDA.Rows.Add(new TwoDARow { Values = new() { "po_shared_", "4", "0" } });
        mock.With2DA("portraits", twoDA);

        var entry = Assert.Single(BuildContext(mock).ListPortraits().ToList());

        Assert.Equal(-1, entry.Race);
    }

    [Fact]
    public void ListPortraits_DedupesCaseInsensitively()
    {
        var mock = new MockGameDataService(includeSampleData: false);
        var twoDA = new TwoDAFile { Columns = new() { "BaseResRef", "Race", "Sex" } };
        twoDA.Rows.Add(new TwoDARow { Values = new() { "hu_m_01_", "6", "0" } });
        twoDA.Rows.Add(new TwoDARow { Values = new() { "Hu_M_01_", "6", "1" } }); // same ResRef, different case
        mock.With2DA("portraits", twoDA);

        var result = BuildContext(mock).ListPortraits().ToList();

        Assert.Single(result);
    }
}
