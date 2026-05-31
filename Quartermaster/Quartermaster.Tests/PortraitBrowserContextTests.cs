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
}
