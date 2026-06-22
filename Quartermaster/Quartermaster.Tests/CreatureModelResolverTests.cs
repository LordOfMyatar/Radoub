using System.Collections.Generic;
using Quartermaster.Services;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for the pure, GL-free part-resolution chain (#2541 Phase 1a): gender→model-flavor
/// resolution and the race → same-gender-human → cross-gender fallback chain. These replace
/// the inline <c>gender == 1 ? 'f' : 'm'</c> literal and the human-only fallback in
/// <see cref="ModelService.AppendPart"/>.
/// </summary>
public class CreatureModelResolverTests
{
    // ---- Gender → model flavor (NWN ships only 'm'/'f' body-model flavors) ----

    [Theory]
    [InlineData(0, 'm', 'f')]  // Male  → primary m, cross f
    [InlineData(1, 'f', 'm')]  // Female → primary f, cross m
    [InlineData(2, 'm', 'f')]  // Both   → map to a real flavor (m) + cross-gender fallback
    [InlineData(3, 'm', 'f')]  // Other  → map to a real flavor (m) + cross-gender fallback
    [InlineData(4, 'm', 'f')]  // None   → map to a real flavor (m) + cross-gender fallback
    [InlineData(99, 'm', 'f')] // Unknown/custom → never throw; default to m + cross fallback
    public void ResolveGenderFlavors_MapsToRealFlavorPlusCross(int gender, char primary, char cross)
    {
        var flavors = CreatureModelResolver.ResolveGenderFlavors(gender);
        Assert.Equal(primary, flavors[0]);
        Assert.Contains(cross, flavors);
    }

    // ---- Part resolution chain ----

    [Fact]
    public void ResolvePart_RaceSpecificModel_PreferredFirst()
    {
        var exists = Set("pfe0_head001");
        var result = CreatureModelResolver.ResolvePart("pfe0", "head", 1, exists);

        Assert.NotNull(result);
        Assert.Equal("pfe0_head001", result!.ResRef);
        Assert.Equal(PartResolutionPath.RaceSpecific, result.Path);
    }

    [Fact]
    public void ResolvePart_FallsBackToSameGenderHuman()
    {
        // Elf female part missing → human female (pfh0) exists.
        var exists = Set("pfh0_head001");
        var result = CreatureModelResolver.ResolvePart("pfe0", "head", 1, exists);

        Assert.NotNull(result);
        Assert.Equal("pfh0_head001", result!.ResRef);
        Assert.Equal(PartResolutionPath.SameGenderHuman, result.Path);
    }

    [Fact]
    public void ResolvePart_FallsBackToOtherGender_WhenSameGenderMissingEverywhere()
    {
        // Appearance authored ONLY in male; viewed as female. Today this renders a missing limb.
        // Cross-gender fallback must find the male race-specific variant.
        var exists = Set("pme0_head001");
        var result = CreatureModelResolver.ResolvePart("pfe0", "head", 1, exists);

        Assert.NotNull(result);
        Assert.Equal("pme0_head001", result!.ResRef);
        Assert.Equal(PartResolutionPath.CrossGender, result.Path);
    }

    [Fact]
    public void ResolvePart_FallsBackToOtherGenderHuman_AsLastResort()
    {
        // Only the human male variant exists; creature is a female elf.
        var exists = Set("pmh0_head001");
        var result = CreatureModelResolver.ResolvePart("pfe0", "head", 1, exists);

        Assert.NotNull(result);
        Assert.Equal("pmh0_head001", result!.ResRef);
        Assert.Equal(PartResolutionPath.CrossGenderHuman, result.Path);
    }

    [Fact]
    public void ResolvePart_ReturnsNull_WhenNothingResolves()
    {
        var exists = Set(/* nothing */);
        var result = CreatureModelResolver.ResolvePart("pfe0", "head", 1, exists);

        Assert.Null(result);
    }

    [Fact]
    public void ResolvePart_RaceSpecificBeatsCrossGender_WhenBothExist()
    {
        // Both the female-elf and male-elf parts exist — must not jump to cross-gender.
        var exists = Set("pfe0_head001", "pme0_head001");
        var result = CreatureModelResolver.ResolvePart("pfe0", "head", 1, exists);

        Assert.Equal("pfe0_head001", result!.ResRef);
        Assert.Equal(PartResolutionPath.RaceSpecific, result.Path);
    }

    [Fact]
    public void ResolvePart_HumanInput_DoesNotDuplicateHumanStep()
    {
        // For a human female whose part is missing, the same-gender-human step IS the race step;
        // resolution should proceed to cross-gender (male human) without a redundant lookup.
        var exists = Set("pmh0_head001");
        var result = CreatureModelResolver.ResolvePart("pfh0", "head", 1, exists);

        Assert.NotNull(result);
        Assert.Equal("pmh0_head001", result!.ResRef);
        Assert.Equal(PartResolutionPath.CrossGender, result.Path);
    }

    private static System.Func<string, bool> Set(params string[] existing)
    {
        var set = new HashSet<string>(existing, System.StringComparer.OrdinalIgnoreCase);
        return resref => set.Contains(resref);
    }
}
