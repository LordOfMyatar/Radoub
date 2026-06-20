using System.Linq;
using Quartermaster.Services;
using Radoub.Formats.TwoDA;
using Radoub.TestUtilities.Mocks;
using Radoub.UI.Services;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for QuartermasterPortraitBrowserContext.ListPortraits — padding-row
/// filtering. Real portraits.2da uses "****" for empty cells, but custom/CEP
/// content can use shorter asterisk runs like "***" (#2291). Any all-asterisk
/// or blank BaseResRef must be skipped so blank tiles don't lead the list.
/// </summary>
public class PortraitBrowserContextTests
{
    private static QuartermasterPortraitBrowserContext BuildContext(MockGameDataService mock)
        => new QuartermasterPortraitBrowserContext(mock, new ItemIconService(mock));

    [Fact]
    public void ListPortraits_SkipsAsteriskAndBlankBaseResRefs()
    {
        var mock = new MockGameDataService(includeSampleData: false);
        var twoDA = new TwoDAFile { Columns = new() { "BaseResRef", "Race", "Sex" } };
        twoDA.Rows.Add(new TwoDARow { Values = new() { "hu_m_01_", "6", "0" } }); // real
        twoDA.Rows.Add(new TwoDARow { Values = new() { "****", "****", "****" } }); // 4-star pad
        twoDA.Rows.Add(new TwoDARow { Values = new() { "***", "****", "4" } });      // 3-star pad (#2291)
        twoDA.Rows.Add(new TwoDARow { Values = new() { "*", "****", "4" } });        // 1-star pad
        twoDA.Rows.Add(new TwoDARow { Values = new() { "  ", "****", "4" } });       // whitespace
        twoDA.Rows.Add(new TwoDARow { Values = new() { "el_f_02_", "1", "1" } });    // real
        mock.With2DA("portraits", twoDA);

        var result = BuildContext(mock).ListPortraits().ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, p => Assert.DoesNotContain('*', p.ResRef));
        Assert.Contains(result, p => p.ResRef == "hu_m_01_");
        Assert.Contains(result, p => p.ResRef == "el_f_02_");
    }

    [Fact]
    public void ListPortraits_DedupesRepeatedBaseResRef()
    {
        // portraits.2da carries the same BaseResRef across multiple rows for
        // race/sex variants. The browser must list each portrait once (#2329).
        var mock = new MockGameDataService(includeSampleData: false);
        var twoDA = new TwoDAFile { Columns = new() { "BaseResRef", "Race", "Sex" } };
        twoDA.Rows.Add(new TwoDARow { Values = new() { "hu_m_01_", "6", "0" } });
        twoDA.Rows.Add(new TwoDARow { Values = new() { "hu_m_01_", "4", "0" } }); // dup (half-elf male)
        twoDA.Rows.Add(new TwoDARow { Values = new() { "hu_m_01_", "5", "0" } }); // dup (half-orc male)
        twoDA.Rows.Add(new TwoDARow { Values = new() { "el_f_02_", "1", "1" } });
        mock.With2DA("portraits", twoDA);

        var result = BuildContext(mock).ListPortraits().ToList();

        Assert.Equal(2, result.Count);
        Assert.Single(result, p => p.ResRef == "hu_m_01_");
        Assert.Single(result, p => p.ResRef == "el_f_02_");
    }

    [Fact]
    public void ListPortraits_DedupesCaseInsensitively()
    {
        // ResRefs are case-insensitive in Aurora; variants differing only in case
        // must not slip past the dedupe (#2329).
        var mock = new MockGameDataService(includeSampleData: false);
        var twoDA = new TwoDAFile { Columns = new() { "BaseResRef", "Race", "Sex" } };
        twoDA.Rows.Add(new TwoDARow { Values = new() { "hu_m_01_", "6", "0" } });
        twoDA.Rows.Add(new TwoDARow { Values = new() { "Hu_M_01_", "6", "1" } }); // same ResRef, different case
        mock.With2DA("portraits", twoDA);

        var result = BuildContext(mock).ListPortraits().ToList();

        Assert.Single(result);
    }

    [Fact]
    public void ListPortraits_KeepsFirstOccurrenceOrder()
    {
        // Dedupe keeps the first row seen and preserves 2DA order.
        var mock = new MockGameDataService(includeSampleData: false);
        var twoDA = new TwoDAFile { Columns = new() { "BaseResRef", "Race", "Sex" } };
        twoDA.Rows.Add(new TwoDARow { Values = new() { "el_f_02_", "1", "1" } });
        twoDA.Rows.Add(new TwoDARow { Values = new() { "hu_m_01_", "6", "0" } });
        twoDA.Rows.Add(new TwoDARow { Values = new() { "el_f_02_", "4", "1" } }); // dup of row 0
        mock.With2DA("portraits", twoDA);

        var result = BuildContext(mock).ListPortraits().ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal("el_f_02_", result[0].ResRef);
        Assert.Equal("hu_m_01_", result[1].ResRef);
    }
}
