using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Radoub.Formats.Uti;
using Xunit;

namespace Radoub.Formats.Tests.Search.Providers;

public class UtiSearchProviderTests
{
    private static UtiFile CreateTestUti()
    {
        return new UtiFile
        {
            LocalizedName = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
            {
                [0] = "Louis Romain's Scythe"
            }},
            Description = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
            {
                [0] = "A mysterious weapon of unknown origin."
            }},
            DescIdentified = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
            {
                [0] = "Forged for Louis Romain by the dwarves of Icewind Dale."
            }},
            Tag = "LOUIS_SCYTHE",
            TemplateResRef = "louis_scythe",
            Comment = "Quest reward item for main plot",
            VarTable = new List<Variable>
            {
                new Variable { Name = "nEnchantLevel", Type = VariableType.Int, Value = 3 },
                new Variable { Name = "sCreator", Type = VariableType.String, Value = "Dwarven Forge of Icewind" }
            }
        };
    }

    private static GffFile UtiToGff(UtiFile uti)
    {
        var bytes = UtiWriter.Write(uti);
        return GffReader.Read(bytes);
    }

    [Fact]
    public void Search_FindsLocalizedName()
    {
        var provider = new UtiSearchProvider();
        var gff = UtiToGff(CreateTestUti());
        var criteria = new SearchCriteria { Pattern = "Louis Romain" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Name" && m.MatchedText == "Louis Romain");
    }

    [Fact]
    public void Search_FindsDescription()
    {
        var provider = new UtiSearchProvider();
        var gff = UtiToGff(CreateTestUti());
        var criteria = new SearchCriteria { Pattern = "unknown origin" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Description");
    }

    [Fact]
    public void Search_FindsDescIdentified()
    {
        var provider = new UtiSearchProvider();
        var gff = UtiToGff(CreateTestUti());
        var criteria = new SearchCriteria { Pattern = "Icewind Dale" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Identified Description");
    }

    [Fact]
    public void Search_FindsTag()
    {
        var provider = new UtiSearchProvider();
        var gff = UtiToGff(CreateTestUti());
        var criteria = new SearchCriteria { Pattern = "LOUIS_SCYTHE" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Tag");
    }

    [Fact]
    public void Search_FindsTemplateResRef()
    {
        var provider = new UtiSearchProvider();
        var gff = UtiToGff(CreateTestUti());
        var criteria = new SearchCriteria { Pattern = "louis_scythe" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Template ResRef");
    }

    [Fact]
    public void Search_FindsComment()
    {
        var provider = new UtiSearchProvider();
        var gff = UtiToGff(CreateTestUti());
        var criteria = new SearchCriteria { Pattern = "Quest reward" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Comment");
    }

    [Fact]
    public void Search_ContentCategoryFilter()
    {
        var provider = new UtiSearchProvider();
        var gff = UtiToGff(CreateTestUti());
        var criteria = new SearchCriteria
        {
            Pattern = "Louis",
            CategoryFilter = new[] { SearchFieldCategory.Content }
        };

        var matches = provider.Search(gff, criteria);

        Assert.All(matches, m => Assert.Equal(SearchFieldCategory.Content, m.Field.Category));
        Assert.True(matches.Count >= 2); // Name + DescIdentified
    }

    [Fact]
    public void Search_FieldNameFilter()
    {
        var provider = new UtiSearchProvider();
        var gff = UtiToGff(CreateTestUti());
        var criteria = new SearchCriteria
        {
            Pattern = "louis",
            FieldFilter = new[] { "Tag" },
            CaseSensitive = false
        };

        var matches = provider.Search(gff, criteria);

        Assert.All(matches, m => Assert.Equal("Tag", m.Field.Name));
    }

    [Fact]
    public void Search_LocationIsFieldName()
    {
        var provider = new UtiSearchProvider();
        var gff = UtiToGff(CreateTestUti());
        var criteria = new SearchCriteria { Pattern = "Quest reward" };

        var matches = provider.Search(gff, criteria);

        var match = Assert.Single(matches);
        Assert.Equal("Comment", match.Location as string);
    }

    [Fact]
    public void FileType_IsUti()
    {
        var provider = new UtiSearchProvider();
        Assert.Equal(Radoub.Formats.Common.ResourceTypes.Uti, provider.FileType);
    }

    [Fact]
    public void Extensions_ContainsUti()
    {
        var provider = new UtiSearchProvider();
        Assert.Contains(".uti", provider.Extensions);
    }

    [Fact]
    public void Search_FindsVarTableName()
    {
        var provider = new UtiSearchProvider();
        var gff = UtiToGff(CreateTestUti());
        var criteria = new SearchCriteria { Pattern = "nEnchantLevel" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Local Variables");
    }

    [Fact]
    public void Search_FindsVarTableStringValue()
    {
        var provider = new UtiSearchProvider();
        var gff = UtiToGff(CreateTestUti());
        var criteria = new SearchCriteria { Pattern = "Dwarven Forge" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Local Variables");
    }

    [Fact]
    public void Search_VariableCategoryFilter()
    {
        var provider = new UtiSearchProvider();
        var gff = UtiToGff(CreateTestUti());
        var criteria = new SearchCriteria
        {
            Pattern = "nEnchantLevel",
            CategoryFilter = new[] { SearchFieldCategory.Variable }
        };

        var matches = provider.Search(gff, criteria);

        Assert.All(matches, m => Assert.Equal(SearchFieldCategory.Variable, m.Field.Category));
    }
}
