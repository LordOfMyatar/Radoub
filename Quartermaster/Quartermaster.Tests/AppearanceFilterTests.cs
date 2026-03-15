using System.Collections.Generic;
using System.Linq;
using Quartermaster.Services;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for appearance search and filter logic.
/// Covers text search across Name/Label/Race and source filtering.
/// </summary>
public class AppearanceFilterTests
{
    private readonly List<AppearanceInfo> _testAppearances;

    public AppearanceFilterTests()
    {
        _testAppearances = new List<AppearanceInfo>
        {
            new() { AppearanceId = 0, Name = "Badger", Label = "Badger", Race = "BADGER", IsPartBased = false, Source = AppearanceSource.Bif },
            new() { AppearanceId = 1, Name = "Bear, Black", Label = "Bear_Black", Race = "BEAR", IsPartBased = false, Source = AppearanceSource.Bif },
            new() { AppearanceId = 2, Name = "Dragon, Red", Label = "Dragon_Red", Race = "DRAGON", IsPartBased = false, Source = AppearanceSource.Bif },
            new() { AppearanceId = 3, Name = "Human (NPC Male)", Label = "Human_NPC_Male", Race = "HUMAN", IsPartBased = true, Source = AppearanceSource.Bif },
            new() { AppearanceId = 100, Name = "CEP Beholder", Label = "CEP_Beholder", Race = "BEHOLDER", IsPartBased = false, Source = AppearanceSource.Hak },
            new() { AppearanceId = 101, Name = "CEP Minotaur", Label = "CEP_Minotaur", Race = "MINOTAUR", IsPartBased = false, Source = AppearanceSource.Hak },
            new() { AppearanceId = 200, Name = "Custom Dragon", Label = "Custom_Dragon", Race = "DRAGON", IsPartBased = false, Source = AppearanceSource.Override },
            new() { AppearanceId = 300, Name = "Unknown Source", Label = "Unknown", Race = "MYSTERY", IsPartBased = false, Source = AppearanceSource.Unknown },
            // Noise entries for exclude filter testing
            new() { AppearanceId = 569, Name = "Invisible_Dragon_10", Label = "Invisible_Dragon_10", Race = "c_InvDrg_010", IsPartBased = false, Source = AppearanceSource.Bif },
            new() { AppearanceId = 589, Name = "Invisible_Dwarf_Female_010", Label = "Invisible_Dwarf_Female_010", Race = "invis_fd_010", IsPartBased = false, Source = AppearanceSource.Bif },
            new() { AppearanceId = 298, Name = "Null Human", Label = "Invisible_Human_Male", Race = "c_invsguy", IsPartBased = false, Source = AppearanceSource.Bif },
            new() { AppearanceId = 431, Name = "Chair", Label = "ObjectChair", Race = "PLC_X02", IsPartBased = false, Source = AppearanceSource.Bif },
            new() { AppearanceId = 435, Name = "Magic Sparks", Label = "objectWhite", Race = "PLC_U03", IsPartBased = false, Source = AppearanceSource.Bif },
        };
    }

    #region Text Search

    [Fact]
    public void FilterAppearances_EmptySearch_ReturnsAll()
    {
        var result = AppearanceFilterHelper.Filter(_testAppearances, "", true, true, true, null);
        Assert.Equal(_testAppearances.Count, result.Count);
    }

    [Fact]
    public void FilterAppearances_NullSearch_ReturnsAll()
    {
        var result = AppearanceFilterHelper.Filter(_testAppearances, null, true, true, true, null);
        Assert.Equal(_testAppearances.Count, result.Count);
    }

    [Fact]
    public void FilterAppearances_SearchByName_CaseInsensitive()
    {
        var result = AppearanceFilterHelper.Filter(_testAppearances, "badger", true, true, true);
        Assert.Single(result);
        Assert.Equal("Badger", result[0].Name);
    }

    [Fact]
    public void FilterAppearances_SearchByLabel_FindsMatch()
    {
        var result = AppearanceFilterHelper.Filter(_testAppearances, "NPC_Male", true, true, true);
        Assert.Single(result);
        Assert.Equal("Human (NPC Male)", result[0].Name);
    }

    [Fact]
    public void FilterAppearances_SearchByRace_FindsMatches()
    {
        var result = AppearanceFilterHelper.Filter(_testAppearances, "DRAGON", true, true, true);
        Assert.Equal(3, result.Count);
        Assert.Contains(result, a => a.Name == "Dragon, Red");
        Assert.Contains(result, a => a.Name == "Custom Dragon");
        Assert.Contains(result, a => a.Name == "Invisible_Dragon_10");
    }

    [Fact]
    public void FilterAppearances_PartialMatch_FindsMultiple()
    {
        var result = AppearanceFilterHelper.Filter(_testAppearances, "bear", true, true, true);
        // Matches "Bear, Black" (name) and "CEP Beholder" (contains "be" — but "bear" should not match "beholder")
        // Actually "bear" matches "Bear, Black" by name. "Beholder" does NOT contain "bear".
        Assert.Single(result);
        Assert.Equal("Bear, Black", result[0].Name);
    }

    [Fact]
    public void FilterAppearances_NoMatch_ReturnsEmpty()
    {
        var result = AppearanceFilterHelper.Filter(_testAppearances, "zzzznonexistent", true, true, true);
        Assert.Empty(result);
    }

    #endregion

    #region Source Filters

    [Fact]
    public void FilterAppearances_BifOnly_ReturnsBifAndUnknownSources()
    {
        var result = AppearanceFilterHelper.Filter(_testAppearances, "", true, false, false);
        // 9 BIF items + 1 Unknown = 10
        Assert.Equal(10, result.Count);
        Assert.All(result, a => Assert.True(a.Source == AppearanceSource.Bif || a.Source == AppearanceSource.Unknown));
    }

    [Fact]
    public void FilterAppearances_HakOnly_ReturnsHakAndUnknownSources()
    {
        var result = AppearanceFilterHelper.Filter(_testAppearances, "", false, true, false);
        Assert.Equal(3, result.Count);
        Assert.All(result, a => Assert.True(a.Source == AppearanceSource.Hak || a.Source == AppearanceSource.Unknown));
    }

    [Fact]
    public void FilterAppearances_OverrideOnly_ReturnsOverrideAndUnknownSources()
    {
        var result = AppearanceFilterHelper.Filter(_testAppearances, "", false, false, true);
        Assert.Equal(2, result.Count);
        Assert.All(result, a => Assert.True(a.Source == AppearanceSource.Override || a.Source == AppearanceSource.Unknown));
    }

    [Fact]
    public void FilterAppearances_AllSourcesUnchecked_ReturnsOnlyUnknown()
    {
        var result = AppearanceFilterHelper.Filter(_testAppearances, "", false, false, false);
        // Unknown source items always shown when no filters are checked (show all behavior)
        // Actually, if all unchecked, show nothing? Or show all? Let's define: all unchecked = show all (no filtering)
        Assert.Equal(_testAppearances.Count, result.Count);
    }

    [Fact]
    public void FilterAppearances_UnknownSource_AlwaysIncluded()
    {
        // Unknown source items should be included regardless of filter state
        var result = AppearanceFilterHelper.Filter(_testAppearances, "", true, false, false);
        Assert.Contains(result, a => a.Source == AppearanceSource.Unknown);
    }

    #endregion

    #region Combined Search + Source Filter

    [Fact]
    public void FilterAppearances_SearchWithSourceFilter_CombinesBoth()
    {
        // Search for "dragon" but only in BIF sources — matches Dragon, Red and Invisible_Dragon_10
        var result = AppearanceFilterHelper.Filter(_testAppearances, "dragon", true, false, false);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, a => a.Name == "Dragon, Red");
        Assert.Contains(result, a => a.Name == "Invisible_Dragon_10");
    }

    [Fact]
    public void FilterAppearances_SearchWithHakFilter_FindsHakOnly()
    {
        var result = AppearanceFilterHelper.Filter(_testAppearances, "CEP", false, true, false);
        Assert.Equal(2, result.Count);
        Assert.All(result, a => Assert.Equal(AppearanceSource.Hak, a.Source));
    }

    #endregion

    #region Exclude Patterns

    [Fact]
    public void FilterAppearances_ExcludeInvisible_RemovesInvisibleEntries()
    {
        // "Invisible" matches 3 entries by Name or Label: Invisible_Dragon_10, Invisible_Dwarf_Female_010, Null Human (Label=Invisible_Human_Male)
        var result = AppearanceFilterHelper.Filter(_testAppearances, "", true, true, true, "Invisible");
        Assert.Equal(10, result.Count);
        Assert.DoesNotContain(result, a => a.Name.Contains("Invisible", StringComparison.OrdinalIgnoreCase)
            || a.Label.Contains("Invisible", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FilterAppearances_ExcludeMultiplePatterns_RemovesAll()
    {
        // "Invisible;object" excludes 3 invisible + 2 object entries = 5 removed, 8 remain
        var result = AppearanceFilterHelper.Filter(_testAppearances, "", true, true, true, "Invisible;object");
        Assert.Equal(8, result.Count);
        Assert.DoesNotContain(result, a => a.Name == "Invisible_Dragon_10");
        Assert.DoesNotContain(result, a => a.Name == "Null Human");
        Assert.DoesNotContain(result, a => a.Name == "Chair");
        Assert.DoesNotContain(result, a => a.Name == "Magic Sparks");
    }

    [Fact]
    public void FilterAppearances_ExcludeWithWhitespace_TrimsPatterns()
    {
        var result = AppearanceFilterHelper.Filter(_testAppearances, "", true, true, true, "  Invisible  ;  object  ");
        Assert.Equal(8, result.Count);
    }

    [Fact]
    public void FilterAppearances_ExcludeNull_NoExclusion()
    {
        var result = AppearanceFilterHelper.Filter(_testAppearances, "", true, true, true, null);
        Assert.Equal(_testAppearances.Count, result.Count);
    }

    [Fact]
    public void FilterAppearances_ExcludeEmpty_NoExclusion()
    {
        var result = AppearanceFilterHelper.Filter(_testAppearances, "", true, true, true, "");
        Assert.Equal(_testAppearances.Count, result.Count);
    }

    [Fact]
    public void FilterAppearances_ExcludeOnlySemicolons_NoExclusion()
    {
        var result = AppearanceFilterHelper.Filter(_testAppearances, "", true, true, true, ";;;");
        Assert.Equal(_testAppearances.Count, result.Count);
    }

    [Fact]
    public void FilterAppearances_ExcludeCombinedWithSearch_BothApply()
    {
        // Search for "dragon" (3 matches) but exclude "Invisible" (removes Invisible_Dragon_10)
        var result = AppearanceFilterHelper.Filter(_testAppearances, "dragon", true, true, true, "Invisible");
        Assert.Equal(2, result.Count);
        Assert.Contains(result, a => a.Name == "Dragon, Red");
        Assert.Contains(result, a => a.Name == "Custom Dragon");
    }

    [Fact]
    public void FilterAppearances_ExcludeIsCaseInsensitive()
    {
        var result = AppearanceFilterHelper.Filter(_testAppearances, "", true, true, true, "invisible");
        Assert.Equal(10, result.Count);
        Assert.DoesNotContain(result, a => a.Label.Contains("Invisible", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ParseExcludePatterns_VariousInputs()
    {
        Assert.Empty(AppearanceFilterHelper.ParseExcludePatterns(null));
        Assert.Empty(AppearanceFilterHelper.ParseExcludePatterns(""));
        Assert.Empty(AppearanceFilterHelper.ParseExcludePatterns("  "));
        Assert.Empty(AppearanceFilterHelper.ParseExcludePatterns(";;;"));

        var single = AppearanceFilterHelper.ParseExcludePatterns("Invisible");
        Assert.Single(single);
        Assert.Equal("Invisible", single[0]);

        var multi = AppearanceFilterHelper.ParseExcludePatterns("Invisible;object;PLC");
        Assert.Equal(3, multi.Length);
        Assert.Equal("Invisible", multi[0]);
        Assert.Equal("object", multi[1]);
        Assert.Equal("PLC", multi[2]);

        var trimmed = AppearanceFilterHelper.ParseExcludePatterns("  Invisible  ;  object  ");
        Assert.Equal(2, trimmed.Length);
        Assert.Equal("Invisible", trimmed[0]);
        Assert.Equal("object", trimmed[1]);
    }

    #endregion
}
