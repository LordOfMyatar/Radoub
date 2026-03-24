using Quartermaster.Services;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for model name construction helpers in ModelService.
/// Fixture-free — tests pure string formatting logic.
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

        // Already lowercase
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

    [Theory]
    [InlineData("pfo0", "head", 1, "pfo0_head001")]
    [InlineData("pmh0", "chest", 1, "pmh0_chest001")]
    [InlineData("pmd0", "neck", 2, "pmd0_neck002")]
    [InlineData("pfe0", "footl", 1, "pfe0_footl001")]
    [InlineData("pmh0", "bicepl", 10, "pmh0_bicepl010")]
    [InlineData("pmh0", "shinr", 100, "pmh0_shinr100")]
    public void BuildBodyPartName_FormatsCorrectly(string prefix, string partType, byte partNumber, string expected)
    {
        var result = ModelService.BuildBodyPartName(prefix, partType, partNumber);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildBodyPartName_PartNumberPaddedToThreeDigits()
    {
        var result = ModelService.BuildBodyPartName("pmh0", "head", 1);
        Assert.EndsWith("001", result);

        var result2 = ModelService.BuildBodyPartName("pmh0", "head", 99);
        Assert.EndsWith("099", result2);
    }
}
