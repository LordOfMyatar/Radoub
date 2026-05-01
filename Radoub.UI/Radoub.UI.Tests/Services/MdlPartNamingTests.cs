using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests.Services;

/// <summary>
/// Tests for MdlPartNaming.BuildBodyPartName — pure string formatting.
/// Originally in Quartermaster's ModelNameConstructionTests; migrated to Radoub.UI.Tests
/// when the helper moved into MdlPartNaming (PR3a, #2159). The QM-specific
/// BuildModelPrefix helper (gender/race/phenotype) stays in QM with its own tests.
/// </summary>
public class MdlPartNamingTests
{
    [Theory]
    [InlineData("pfo0", "head", 1, "pfo0_head001")]
    [InlineData("pmh0", "chest", 1, "pmh0_chest001")]
    [InlineData("pmd0", "neck", 2, "pmd0_neck002")]
    [InlineData("pfe0", "footl", 1, "pfe0_footl001")]
    [InlineData("pmh0", "bicepl", 10, "pmh0_bicepl010")]
    [InlineData("pmh0", "shinr", 100, "pmh0_shinr100")]
    public void BuildBodyPartName_FormatsCorrectly(string prefix, string partType, byte partNumber, string expected)
    {
        var result = MdlPartNaming.BuildBodyPartName(prefix, partType, partNumber);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildBodyPartName_PartNumberPaddedToThreeDigits()
    {
        var result = MdlPartNaming.BuildBodyPartName("pmh0", "head", 1);
        Assert.EndsWith("001", result);

        var result2 = MdlPartNaming.BuildBodyPartName("pmh0", "head", 99);
        Assert.EndsWith("099", result2);
    }
}
