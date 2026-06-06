using System.Linq;
using PlaceableEditor.Services;
using Radoub.Formats.TwoDA;
using Radoub.TestUtilities.Mocks;
using Radoub.UI.Services;

namespace PlaceableEditor.Tests.Services;

/// <summary>
/// ListPortraits padding-row filtering for Reliquary's portrait context (#2370), mirroring the
/// Quartermaster coverage: real portraits.2da uses "****" for empty cells, custom/CEP content can
/// use shorter asterisk runs, and any all-asterisk/blank BaseResRef must be skipped.
/// </summary>
public class ReliquaryPortraitBrowserContextTests
{
    private static ReliquaryPortraitBrowserContext BuildContext(MockGameDataService mock)
        => new(mock, new ItemIconService(mock));

    [Fact]
    public void ListPortraits_SkipsAsteriskAndBlankBaseResRefs()
    {
        var mock = new MockGameDataService(includeSampleData: false);
        var twoDA = new TwoDAFile { Columns = new() { "BaseResRef", "Race", "Sex" } };
        twoDA.Rows.Add(new TwoDARow { Values = new() { "hu_m_01_", "6", "0" } }); // real
        twoDA.Rows.Add(new TwoDARow { Values = new() { "****", "****", "****" } }); // pad
        twoDA.Rows.Add(new TwoDARow { Values = new() { "***", "****", "4" } });      // short pad
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
