using Radoub.UI.Controls;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Pure-logic tests for FileBrowserPanelBase sort + search across the three
/// <see cref="BrowserSortMode"/> values. See #2198 (epic #2186).
/// </summary>
public class BrowserSortLogicTests
{
    private static List<FileBrowserEntry> SampleEntries() => new()
    {
        new() { Name = "mod_b", DisplayLabel = "Beta Sword", Tag = "TAG_BETA", IsFromHak = false, Source = "Module" },
        new() { Name = "mod_a", DisplayLabel = "Alpha Shield", Tag = "TAG_ALPHA", IsFromHak = false, Source = "Module" },
        new() { Name = "hak_c", DisplayLabel = "Charlie Ring", Tag = "TAG_CHARLIE", IsFromHak = true, Source = "HAK: cep.hak" },
        new() { Name = "hak_d", DisplayLabel = null, Tag = null, IsFromHak = true, Source = "HAK: cep.hak" }
    };

    [Fact]
    public void SortByResRef_ModuleFirst_ThenAlphabetic()
    {
        var result = BrowserSortLogic.FilterAndSort(SampleEntries(), null, BrowserSortMode.ResRef);

        Assert.Equal(new[] { "mod_a", "mod_b", "hak_c", "hak_d" }, result.Select(e => e.Name));
    }

    [Fact]
    public void SortByName_ModuleFirst_ThenByDisplayLabel_NullsLast()
    {
        var result = BrowserSortLogic.FilterAndSort(SampleEntries(), null, BrowserSortMode.Name);

        // Module tier: Alpha Shield, Beta Sword. HAK tier: Charlie Ring, then null-label hak_d last.
        Assert.Equal(new[] { "mod_a", "mod_b", "hak_c", "hak_d" }, result.Select(e => e.Name));
    }

    [Fact]
    public void SortByTag_ModuleFirst_ThenByTag_NullsLast()
    {
        var result = BrowserSortLogic.FilterAndSort(SampleEntries(), null, BrowserSortMode.Tag);

        Assert.Equal(new[] { "mod_a", "mod_b", "hak_c", "hak_d" }, result.Select(e => e.Name));
    }

    [Fact]
    public void SearchByResRef_MatchesNameField()
    {
        var result = BrowserSortLogic.FilterAndSort(SampleEntries(), "mod_", BrowserSortMode.ResRef);

        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.StartsWith("mod_", e.Name));
    }

    [Fact]
    public void SearchByName_MatchesDisplayLabel_NotResRef()
    {
        // "Sword" appears in DisplayLabel of mod_b ("Beta Sword") but NOT in its ResRef.
        var result = BrowserSortLogic.FilterAndSort(SampleEntries(), "sword", BrowserSortMode.Name);

        Assert.Single(result);
        Assert.Equal("mod_b", result[0].Name);
    }

    [Fact]
    public void SearchByName_NullDisplayLabel_DoesNotMatchEmptySearch()
    {
        var entries = new List<FileBrowserEntry>
        {
            new() { Name = "indexed", DisplayLabel = "Hello", Tag = null, IsFromHak = false },
            new() { Name = "unindexed", DisplayLabel = null, Tag = null, IsFromHak = false }
        };

        var result = BrowserSortLogic.FilterAndSort(entries, "hello", BrowserSortMode.Name);

        Assert.Single(result);
        Assert.Equal("indexed", result[0].Name);
    }

    [Fact]
    public void SearchByTag_MatchesTagField_NotResRef()
    {
        var result = BrowserSortLogic.FilterAndSort(SampleEntries(), "alpha", BrowserSortMode.Tag);

        Assert.Single(result);
        Assert.Equal("mod_a", result[0].Name);
        Assert.Equal("TAG_ALPHA", result[0].Tag);
    }

    [Fact]
    public void EmptySearch_ReturnsAllEntries()
    {
        var result = BrowserSortLogic.FilterAndSort(SampleEntries(), "", BrowserSortMode.ResRef);
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void WhitespaceSearch_ReturnsAllEntries()
    {
        var result = BrowserSortLogic.FilterAndSort(SampleEntries(), "   ", BrowserSortMode.Name);
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void NullSearch_ReturnsAllEntries()
    {
        var result = BrowserSortLogic.FilterAndSort(SampleEntries(), null, BrowserSortMode.Tag);
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void SearchIsCaseInsensitive_ForAllModes()
    {
        Assert.Single(BrowserSortLogic.FilterAndSort(SampleEntries(), "MOD_A", BrowserSortMode.ResRef));
        Assert.Single(BrowserSortLogic.FilterAndSort(SampleEntries(), "ALPHA SHIELD", BrowserSortMode.Name));
        Assert.Single(BrowserSortLogic.FilterAndSort(SampleEntries(), "tag_alpha", BrowserSortMode.Tag));
    }

    [Fact]
    public void ModuleTierAlwaysFirst_EvenWhenSortedByName()
    {
        // HAK entry with display label "AAA" should still sort AFTER module entries
        // because module-first tier is preserved.
        var entries = new List<FileBrowserEntry>
        {
            new() { Name = "hak_aaa", DisplayLabel = "AAA First Alphabetically", IsFromHak = true },
            new() { Name = "mod_zzz", DisplayLabel = "ZZZ Last Alphabetically", IsFromHak = false }
        };

        var result = BrowserSortLogic.FilterAndSort(entries, null, BrowserSortMode.Name);

        Assert.Equal(new[] { "mod_zzz", "hak_aaa" }, result.Select(e => e.Name));
    }

    [Fact]
    public void NullDisplayLabel_SortsAfterPopulated_WithinTier()
    {
        var entries = new List<FileBrowserEntry>
        {
            new() { Name = "first", DisplayLabel = null, IsFromHak = false },
            new() { Name = "second", DisplayLabel = "Real Name", IsFromHak = false }
        };

        var result = BrowserSortLogic.FilterAndSort(entries, null, BrowserSortMode.Name);

        Assert.Equal(new[] { "second", "first" }, result.Select(e => e.Name));
    }

    [Fact]
    public void NullTag_SortsAfterPopulated_WithinTier()
    {
        var entries = new List<FileBrowserEntry>
        {
            new() { Name = "first", Tag = null, IsFromHak = false },
            new() { Name = "second", Tag = "TAG_X", IsFromHak = false }
        };

        var result = BrowserSortLogic.FilterAndSort(entries, null, BrowserSortMode.Tag);

        Assert.Equal(new[] { "second", "first" }, result.Select(e => e.Name));
    }

    [Fact]
    public void EmptyInput_ReturnsEmpty()
    {
        var result = BrowserSortLogic.FilterAndSort(new List<FileBrowserEntry>(), "search", BrowserSortMode.Name);
        Assert.Empty(result);
    }

    [Fact]
    public void DefaultEntry_BehavesAsResRefMode()
    {
        var entries = new List<FileBrowserEntry>
        {
            new() { Name = "zzz", IsFromHak = false },
            new() { Name = "aaa", IsFromHak = false }
        };

        var result = BrowserSortLogic.FilterAndSort(entries, null, BrowserSortMode.ResRef);

        Assert.Equal(new[] { "aaa", "zzz" }, result.Select(e => e.Name));
    }
}
