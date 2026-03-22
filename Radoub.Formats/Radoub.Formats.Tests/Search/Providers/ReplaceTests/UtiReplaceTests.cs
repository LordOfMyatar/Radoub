using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Radoub.Formats.Uti;
using Xunit;

namespace Radoub.Formats.Tests.Search.Providers.ReplaceTests;

public class UtiReplaceTests
{
    private static UtiFile CreateTestUti()
    {
        return new UtiFile
        {
            LocalizedName = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
            {
                [0] = "Louis's Scythe"
            }},
            Description = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
            {
                [0] = "A crude farming tool."
            }},
            DescIdentified = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
            {
                [0] = "Forged for Louis Romain by the western smiths."
            }},
            Tag = "LOUIS_SCYTHE",
            TemplateResRef = "louis_scythe",
            Comment = "Quest reward item for Louis"
        };
    }

    private static GffFile UtiToGff(UtiFile uti)
    {
        var bytes = UtiWriter.Write(uti);
        return GffReader.Read(bytes);
    }

    [Fact]
    public void Replace_LocString_Name()
    {
        var provider = new UtiSearchProvider();
        var gff = UtiToGff(CreateTestUti());
        var matches = provider.Search(gff, new SearchCriteria { Pattern = "Louis" });
        var nameMatch = matches.First(m => m.Field.Name == "Name");

        gff = UtiToGff(CreateTestUti());
        var results = provider.Replace(gff, new[] { new ReplaceOperation { Match = nameMatch, ReplacementText = "Marcel" } });

        Assert.All(results, r => Assert.True(r.Success));
        var bytes = GffWriter.Write(gff);
        var uti = UtiReader.Read(bytes);
        Assert.Equal("Marcel's Scythe", uti.LocalizedName.GetString(0));
    }

    [Fact]
    public void Replace_IdentifiedDescription()
    {
        var provider = new UtiSearchProvider();
        var gff = UtiToGff(CreateTestUti());
        var matches = provider.Search(gff, new SearchCriteria { Pattern = "Louis Romain" });
        var descMatch = matches.First(m => m.Field.Name == "Identified Description");

        gff = UtiToGff(CreateTestUti());
        var results = provider.Replace(gff, new[] { new ReplaceOperation { Match = descMatch, ReplacementText = "Marcel Iceberg" } });

        Assert.All(results, r => Assert.True(r.Success));
        var bytes = GffWriter.Write(gff);
        var uti = UtiReader.Read(bytes);
        Assert.Contains("Marcel Iceberg", uti.DescIdentified.GetString(0));
    }

    [Fact]
    public void Replace_Tag()
    {
        var provider = new UtiSearchProvider();
        var gff = UtiToGff(CreateTestUti());
        var matches = provider.Search(gff, new SearchCriteria { Pattern = "LOUIS_SCYTHE" });
        var tagMatch = matches.First(m => m.Field.Name == "Tag");

        gff = UtiToGff(CreateTestUti());
        var results = provider.Replace(gff, new[] { new ReplaceOperation { Match = tagMatch, ReplacementText = "MARCEL_SCYTHE" } });

        Assert.All(results, r => Assert.True(r.Success));
        var bytes = GffWriter.Write(gff);
        var uti = UtiReader.Read(bytes);
        Assert.Equal("MARCEL_SCYTHE", uti.Tag);
    }

    [Fact]
    public void Replace_ResRef_WithTruncation()
    {
        var provider = new UtiSearchProvider();
        var gff = UtiToGff(CreateTestUti());
        var matches = provider.Search(gff, new SearchCriteria { Pattern = "louis_scythe" });
        var resRefMatch = matches.First(m => m.Field.Name == "Template ResRef");

        gff = UtiToGff(CreateTestUti());
        var results = provider.Replace(gff, new[] { new ReplaceOperation { Match = resRefMatch, ReplacementText = "very_long_resref_name" } });

        var result = Assert.Single(results);
        Assert.True(result.Success);
        Assert.NotNull(result.Warning);

        var bytes = GffWriter.Write(gff);
        var uti = UtiReader.Read(bytes);
        Assert.Equal(16, uti.TemplateResRef.Length);
    }
}
