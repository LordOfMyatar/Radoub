using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests.Services;

/// <summary>
/// The mannequin prefix selects the body the armor/clothing preview is composed on.
/// Gender lives in the prefix (pmh0 male / pfh0 female), race=h, phenotype=0.
/// Mirrors Quartermaster's ModelService.BuildModelPrefix gender rule (#2407).
/// </summary>
public class MannequinPrefixTests
{
    [Fact]
    public void ForGender_Male_ReturnsMalePrefix()
    {
        Assert.Equal("pmh0", MannequinPrefix.ForGender(0));
    }

    [Fact]
    public void ForGender_Female_ReturnsFemalePrefix()
    {
        Assert.Equal("pfh0", MannequinPrefix.ForGender(1));
    }

    [Fact]
    public void ForGender_UnknownGender_DefaultsToMale()
    {
        Assert.Equal("pmh0", MannequinPrefix.ForGender(99));
    }
}
