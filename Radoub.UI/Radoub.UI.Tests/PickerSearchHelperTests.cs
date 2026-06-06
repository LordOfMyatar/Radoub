using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Coverage for the shared picker search helper (#2360). One tested implementation
/// for the eight tool picker windows that previously hand-rolled the same
/// name-or-id contains filter with no test seam.
/// </summary>
public class PickerSearchHelperTests
{
    [Fact]
    public void EmptySearch_MatchesEverything()
    {
        Assert.True(PickerSearchHelper.Matches("", "Magic Missile", "42"));
        Assert.True(PickerSearchHelper.Matches(null, "Magic Missile", "42"));
    }

    [Fact]
    public void MatchesName_CaseInsensitive()
    {
        Assert.True(PickerSearchHelper.Matches("missile", "Magic Missile", "42"));
        Assert.True(PickerSearchHelper.Matches("MAGIC", "Magic Missile", "42"));
    }

    [Fact]
    public void MatchesId()
    {
        Assert.True(PickerSearchHelper.Matches("42", "Magic Missile", "42"));
    }

    [Fact]
    public void NoMatch_ReturnsFalse()
    {
        Assert.False(PickerSearchHelper.Matches("fireball", "Magic Missile", "42"));
    }

    [Fact]
    public void MatchesAnyField()
    {
        Assert.True(PickerSearchHelper.Matches("sword_tag", "Longsword", "5", "sword_tag"));
    }

    [Fact]
    public void NullFields_AreSkipped()
    {
        Assert.True(PickerSearchHelper.Matches("name", "the name", null, null));
        Assert.False(PickerSearchHelper.Matches("zzz", null, null));
    }

    [Fact]
    public void NoFields_NonEmptySearch_ReturnsFalse()
    {
        Assert.False(PickerSearchHelper.Matches("anything"));
    }

    [Fact]
    public void PartialSubstring_Matches()
    {
        Assert.True(PickerSearchHelper.Matches("ongsw", "Longsword", "5"));
    }
}
