using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Radoub.Formats.Utc;
using Xunit;

namespace Radoub.Formats.Tests.Search.Providers;

public class UtcSearchProviderTests
{
    private static UtcFile CreateTestUtc()
    {
        return new UtcFile
        {
            FirstName = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
            {
                [0] = "Louis Romain"
            }},
            LastName = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
            {
                [0] = "the Merchant"
            }},
            Description = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
            {
                [0] = "A weathered merchant from the western coast."
            }},
            Tag = "LOUIS_ROMAIN",
            TemplateResRef = "louis_romain",
            Comment = "Main quest NPC merchant",
            Subrace = "Illuskan",
            Deity = "Waukeen",
            Conversation = "louis_conv",
            ScriptAttacked = "nw_c2_default5",
            ScriptDamaged = "nw_c2_default6",
            ScriptDeath = "nw_c2_default7",
            ScriptDialogue = "nw_c2_default4",
            ScriptDisturbed = "nw_c2_default8",
            ScriptEndRound = "nw_c2_default3",
            ScriptHeartbeat = "nw_c2_default1",
            ScriptOnBlocked = "nw_c2_defaultb",
            ScriptOnNotice = "nw_c2_default2",
            ScriptRested = "nw_c2_defaulta",
            ScriptSpawn = "nw_c2_default9",
            ScriptSpellAt = "nw_c2_defaultd",
            ScriptUserDefine = "nw_c2_defaulte",
            VarTable = new List<Variable>
            {
                new Variable { Name = "nBossState", Type = VariableType.Int, Value = 0 },
                new Variable { Name = "sQuestNote", Type = VariableType.String, Value = "Find the lost amulet" }
            }
        };
    }

    private static GffFile UtcToGff(UtcFile utc)
    {
        var bytes = UtcWriter.Write(utc);
        return GffReader.Read(bytes);
    }

    [Fact]
    public void Search_FindsFirstName()
    {
        var provider = new UtcSearchProvider();
        var gff = UtcToGff(CreateTestUtc());
        var criteria = new SearchCriteria { Pattern = "Louis Romain" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "First Name" && m.MatchedText == "Louis Romain");
    }

    [Fact]
    public void Search_FindsLastName()
    {
        var provider = new UtcSearchProvider();
        var gff = UtcToGff(CreateTestUtc());
        var criteria = new SearchCriteria { Pattern = "the Merchant" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Last Name");
    }

    [Fact]
    public void Search_FindsDescription()
    {
        var provider = new UtcSearchProvider();
        var gff = UtcToGff(CreateTestUtc());
        var criteria = new SearchCriteria { Pattern = "western coast" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Description");
    }

    [Fact]
    public void Search_FindsTag()
    {
        var provider = new UtcSearchProvider();
        var gff = UtcToGff(CreateTestUtc());
        var criteria = new SearchCriteria { Pattern = "LOUIS_ROMAIN" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Tag");
    }

    [Fact]
    public void Search_FindsTemplateResRef()
    {
        var provider = new UtcSearchProvider();
        var gff = UtcToGff(CreateTestUtc());
        var criteria = new SearchCriteria { Pattern = "louis_romain" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Template ResRef");
    }

    [Fact]
    public void Search_FindsComment()
    {
        var provider = new UtcSearchProvider();
        var gff = UtcToGff(CreateTestUtc());
        var criteria = new SearchCriteria { Pattern = "Main quest" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Comment");
    }

    [Fact]
    public void Search_FindsSubrace()
    {
        var provider = new UtcSearchProvider();
        var gff = UtcToGff(CreateTestUtc());
        var criteria = new SearchCriteria { Pattern = "Illuskan" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Subrace");
    }

    [Fact]
    public void Search_FindsDeity()
    {
        var provider = new UtcSearchProvider();
        var gff = UtcToGff(CreateTestUtc());
        var criteria = new SearchCriteria { Pattern = "Waukeen" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Deity");
    }

    [Fact]
    public void Search_FindsConversation()
    {
        var provider = new UtcSearchProvider();
        var gff = UtcToGff(CreateTestUtc());
        var criteria = new SearchCriteria { Pattern = "louis_conv" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Conversation");
    }

    [Fact]
    public void Search_FindsScriptFields()
    {
        var provider = new UtcSearchProvider();
        var gff = UtcToGff(CreateTestUtc());
        var criteria = new SearchCriteria { Pattern = "nw_c2_default5" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "ScriptAttacked");
    }

    [Fact]
    public void Search_FindsAllScripts_WithPrefix()
    {
        var provider = new UtcSearchProvider();
        var gff = UtcToGff(CreateTestUtc());
        var criteria = new SearchCriteria
        {
            Pattern = "nw_c2_",
            FieldTypeFilter = new[] { SearchFieldType.Script }
        };

        var matches = provider.Search(gff, criteria);

        Assert.Equal(13, matches.Count);
        Assert.All(matches, m => Assert.Equal(SearchFieldType.Script, m.Field.FieldType));
    }

    [Fact]
    public void Search_FindsVarTableName()
    {
        var provider = new UtcSearchProvider();
        var gff = UtcToGff(CreateTestUtc());
        var criteria = new SearchCriteria { Pattern = "nBossState" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Local Variables");
    }

    [Fact]
    public void Search_FindsVarTableStringValue()
    {
        var provider = new UtcSearchProvider();
        var gff = UtcToGff(CreateTestUtc());
        var criteria = new SearchCriteria { Pattern = "lost amulet" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Local Variables");
    }

    [Fact]
    public void Search_FieldFilter_ContentOnly()
    {
        var provider = new UtcSearchProvider();
        var gff = UtcToGff(CreateTestUtc());
        var criteria = new SearchCriteria
        {
            Pattern = "Louis",
            CategoryFilter = new[] { SearchFieldCategory.Content }
        };

        var matches = provider.Search(gff, criteria);

        Assert.All(matches, m => Assert.Equal(SearchFieldCategory.Content, m.Field.Category));
        Assert.True(matches.Count >= 1);
    }

    [Fact]
    public void Search_FieldNameFilter_TagOnly()
    {
        var provider = new UtcSearchProvider();
        var gff = UtcToGff(CreateTestUtc());
        var criteria = new SearchCriteria
        {
            Pattern = "LOUIS",
            FieldFilter = new[] { "Tag" }
        };

        var matches = provider.Search(gff, criteria);

        Assert.All(matches, m => Assert.Equal("Tag", m.Field.Name));
    }

    [Fact]
    public void Search_LocationIsFieldName()
    {
        var provider = new UtcSearchProvider();
        var gff = UtcToGff(CreateTestUtc());
        var criteria = new SearchCriteria { Pattern = "Waukeen" };

        var matches = provider.Search(gff, criteria);

        var match = Assert.Single(matches);
        Assert.Equal("Deity", match.Location as string);
    }

    [Fact]
    public void FileType_IsUtc()
    {
        var provider = new UtcSearchProvider();
        Assert.Equal(Radoub.Formats.Common.ResourceTypes.Utc, provider.FileType);
    }

    [Fact]
    public void Extensions_ContainsUtcAndBic()
    {
        var provider = new UtcSearchProvider();
        Assert.Contains(".utc", provider.Extensions);
        Assert.Contains(".bic", provider.Extensions);
    }
}
