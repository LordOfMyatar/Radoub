using Quartermaster.Services;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for QM-specific creature-prefix construction (gender/race/phenotype → "pmh0", "pfe2", etc.).
/// The generic BuildBodyPartName helper moved to Radoub.UI in PR3a (#2159) — see
/// MdlPartNamingTests in Radoub.UI.Tests.
/// </summary>
public class ModelNameConstructionTests
{
    [Theory]
    [InlineData(0, "H", 0, "pmh0")]  // Male human phenotype 0
    [InlineData(1, "H", 0, "pfh0")]  // Female human phenotype 0
    [InlineData(0, "O", 0, "pmo0")]  // Male half-elf phenotype 0
    [InlineData(1, "E", 0, "pfe0")]  // Female elf phenotype 0
    [InlineData(0, "D", 0, "pmd0")]  // Male dwarf phenotype 0
    [InlineData(0, "H", 2, "pmh2")]  // Male human phenotype 2 (large)
    public void BuildModelPrefix_KnownRaces(int gender, string race, int phenotype, string expected)
    {
        var result = ModelService.BuildModelPrefix(gender, race, phenotype);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildModelPrefix_RaceIsLowercased()
    {
        var result = ModelService.BuildModelPrefix(0, "H", 0);
        Assert.Equal("pmh0", result);

        var result2 = ModelService.BuildModelPrefix(0, "h", 0);
        Assert.Equal("pmh0", result2);
    }

    [Fact]
    public void BuildModelPrefix_GenderZero_IsMale()
    {
        var result = ModelService.BuildModelPrefix(0, "h", 0);
        Assert.StartsWith("pm", result);
    }

    [Fact]
    public void BuildModelPrefix_GenderOne_IsFemale()
    {
        var result = ModelService.BuildModelPrefix(1, "h", 0);
        Assert.StartsWith("pf", result);
    }
}
