using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Radoub.Formats.Utc;
using Xunit;

namespace Radoub.Formats.Tests.Search.Providers.ReplaceTests;

/// <summary>
/// Tests for UtcSearchProvider.Replace() — replaces in creature/BIC fields.
/// </summary>
public class UtcReplaceTests
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
            Comment = "Main quest NPC merchant Louis",
            Subrace = "Illuskan",
            Deity = "Waukeen",
            Conversation = "louis_conv",
            ScriptAttacked = "nw_c2_default5",
            ScriptDamaged = "nw_c2_default6",
            ScriptDeath = "louis_death",
            VarTable = new List<Variable>
            {
                new Variable { Name = "sQuestNote", Type = VariableType.String, Value = "Find Louis at the docks" }
            }
        };
    }

    private static GffFile UtcToGff(UtcFile utc)
    {
        var bytes = UtcWriter.Write(utc);
        return GffReader.Read(bytes);
    }

    /// <summary>Search, then build replace ops from matches targeting specific field.</summary>
    private static (GffFile gff, IReadOnlyList<ReplaceOperation> ops) SearchAndBuildOps(
        string searchPattern, string replacement, string? fieldName = null)
    {
        var provider = new UtcSearchProvider();
        var gff = UtcToGff(CreateTestUtc());
        var criteria = new SearchCriteria { Pattern = searchPattern };
        var matches = provider.Search(gff, criteria);

        var filtered = fieldName != null
            ? matches.Where(m => m.Field.Name == fieldName).ToList()
            : matches.ToList();

        var ops = filtered.Select(m => new ReplaceOperation
        {
            Match = m,
            ReplacementText = replacement
        }).ToList();

        // Re-parse GFF since Search consumed it via round-trip
        gff = UtcToGff(CreateTestUtc());
        return (gff, ops);
    }

    [Fact]
    public void Replace_LocString_FirstName()
    {
        var (gff, ops) = SearchAndBuildOps("Louis Romain", "Marcel Iceberg", "First Name");

        var provider = new UtcSearchProvider();
        var results = provider.Replace(gff, ops);

        Assert.All(results, r => Assert.True(r.Success));

        // Verify the GFF was mutated — round-trip to typed model
        var bytes = GffWriter.Write(gff);
        var utc = UtcReader.Read(bytes);
        Assert.Equal("Marcel Iceberg", utc.FirstName.GetString(0));
    }

    [Fact]
    public void Replace_PlainString_Tag()
    {
        var (gff, ops) = SearchAndBuildOps("LOUIS_ROMAIN", "MARCEL_ICEBERG", "Tag");

        var provider = new UtcSearchProvider();
        var results = provider.Replace(gff, ops);

        Assert.All(results, r => Assert.True(r.Success));

        var bytes = GffWriter.Write(gff);
        var utc = UtcReader.Read(bytes);
        Assert.Equal("MARCEL_ICEBERG", utc.Tag);
    }

    [Fact]
    public void Replace_ResRef_TemplateResRef_SkippedBecauseFileRenameRequired()
    {
        var (gff, ops) = SearchAndBuildOps("louis_romain", "marcel_ice", "Template ResRef");

        var provider = new UtcSearchProvider();
        var results = provider.Replace(gff, ops);

        // ResRef fields are not replaceable — changing a ResRef without renaming the file breaks the reference
        Assert.All(results, r =>
        {
            Assert.True(r.Skipped);
            Assert.False(r.Success);
            Assert.Contains("file rename", r.SkipReason, StringComparison.OrdinalIgnoreCase);
        });

        // Verify the GFF was NOT mutated
        var bytes = GffWriter.Write(gff);
        var utc = UtcReader.Read(bytes);
        Assert.Equal("louis_romain", utc.TemplateResRef);
    }

    [Fact]
    public void Replace_Script_Field()
    {
        var (gff, ops) = SearchAndBuildOps("louis_death", "marcel_death", "ScriptDeath");

        var provider = new UtcSearchProvider();
        var results = provider.Replace(gff, ops);

        Assert.All(results, r => Assert.True(r.Success));

        var bytes = GffWriter.Write(gff);
        var utc = UtcReader.Read(bytes);
        Assert.Equal("marcel_death", utc.ScriptDeath);
    }

    [Fact]
    public void Replace_Comment()
    {
        var (gff, ops) = SearchAndBuildOps("Louis", "Marcel", "Comment");

        var provider = new UtcSearchProvider();
        var results = provider.Replace(gff, ops);

        Assert.All(results, r => Assert.True(r.Success));

        var bytes = GffWriter.Write(gff);
        var utc = UtcReader.Read(bytes);
        Assert.Equal("Main quest NPC merchant Marcel", utc.Comment);
    }

    [Fact]
    public void Replace_ReturnsOldAndNewValues()
    {
        var (gff, ops) = SearchAndBuildOps("LOUIS_ROMAIN", "MARCEL_ICEBERG", "Tag");

        var provider = new UtcSearchProvider();
        var results = provider.Replace(gff, ops);

        var result = Assert.Single(results);
        Assert.Equal("LOUIS_ROMAIN", result.OldValue);
        Assert.Equal("MARCEL_ICEBERG", result.NewValue);
    }

    [Fact]
    public void Replace_MultipleMatchesInSameField_ReverseOffsetOrder()
    {
        // "A weathered merchant from the western coast." contains no double matches
        // Use a pattern that matches multiple times in FirstName text
        var utc = new UtcFile
        {
            FirstName = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
            {
                [0] = "aa bb aa"
            }}
        };
        var gff = UtcToGff(utc);

        var provider = new UtcSearchProvider();
        var criteria = new SearchCriteria { Pattern = "aa" };
        var matches = provider.Search(gff, criteria);
        Assert.Equal(2, matches.Count);

        var ops = matches.Select(m => new ReplaceOperation
        {
            Match = m,
            ReplacementText = "cc"
        }).ToList();

        // Re-parse for replace
        gff = UtcToGff(utc);
        var results = provider.Replace(gff, ops);

        Assert.All(results, r => Assert.True(r.Success));

        var bytes = GffWriter.Write(gff);
        var result = UtcReader.Read(bytes);
        Assert.Equal("cc bb cc", result.FirstName.GetString(0));
    }

    [Fact]
    public void Replace_EmptyOperations_ReturnsEmpty()
    {
        var gff = UtcToGff(CreateTestUtc());
        var provider = new UtcSearchProvider();

        var results = provider.Replace(gff, Array.Empty<ReplaceOperation>());

        Assert.Empty(results);
    }
}
