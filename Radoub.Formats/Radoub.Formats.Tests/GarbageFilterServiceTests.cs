using Radoub.Formats.Services;
using Xunit;

namespace Radoub.Formats.Tests;

public class GarbageFilterServiceTests
{
    #region Substring Match Tests

    [Theory]
    [InlineData("deleted_item", true)]
    [InlineData("DELETED", true)]
    [InlineData("BASE_ITEM_DELETED", true)]
    [InlineData("padding", true)]
    [InlineData("PAdding", true)]
    [InlineData("bio_reserved", true)]
    [InlineData("CEP Reserved", true)]
    [InlineData("xp2spec1", true)]
    [InlineData("XP2SPEC_PLACEHOLDER", true)]
    public void IsGarbageLabel_SubstringPatterns_MatchCorrectly(string label, bool expected)
    {
        var service = CreateServiceWithDefaults();
        Assert.Equal(expected, service.IsGarbageLabel(label));
    }

    #endregion

    #region Exact Match Tests

    [Theory]
    [InlineData("User", true)]
    [InlineData("user", true)]
    [InlineData("USER", true)]
    [InlineData("UserDefined", false)]
    [InlineData("PowerUser", false)]
    [InlineData("****", true)]
    [InlineData("blank", true)]
    [InlineData("BLANK", true)]
    [InlineData("BlankItem", false)]
    [InlineData("blank_weapon", false)]
    [InlineData("invalid", true)]
    [InlineData("InvalidEntry", false)]
    public void IsGarbageLabel_ExactPatterns_MatchCorrectly(string label, bool expected)
    {
        var service = CreateServiceWithDefaults();
        Assert.Equal(expected, service.IsGarbageLabel(label));
    }

    #endregion

    #region Valid Labels

    [Theory]
    [InlineData("BASE_ITEM_SHORTSWORD")]
    [InlineData("BASE_ITEM_LONGSWORD")]
    [InlineData("CreatureWeapon")]
    [InlineData("Armor")]
    public void IsGarbageLabel_ValidLabels_ReturnsFalse(string label)
    {
        var service = CreateServiceWithDefaults();
        Assert.False(service.IsGarbageLabel(label));
    }

    #endregion

    #region Null/Empty

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsGarbageLabel_NullOrEmpty_ReturnsTrue(string? label)
    {
        var service = CreateServiceWithDefaults();
        Assert.True(service.IsGarbageLabel(label!));
    }

    #endregion

    #region Custom Filters

    [Fact]
    public void IsGarbageLabel_CustomFilter_SubstringAdded()
    {
        var service = new GarbageFilterService(new List<string> { "custom_junk" });
        Assert.True(service.IsGarbageLabel("my_custom_junk_item"));
        Assert.False(service.IsGarbageLabel("BASE_ITEM_SHORTSWORD"));
    }

    [Fact]
    public void IsGarbageLabel_CustomFilter_ExactAdded()
    {
        var service = new GarbageFilterService(new List<string> { "=Placeholder" });
        Assert.True(service.IsGarbageLabel("Placeholder"));
        Assert.True(service.IsGarbageLabel("placeholder"));
        Assert.False(service.IsGarbageLabel("PlaceholderItem"));
    }

    [Fact]
    public void IsGarbageLabel_EmptyFilterList_OnlyRejectsNullEmpty()
    {
        var service = new GarbageFilterService(new List<string>());
        Assert.False(service.IsGarbageLabel("deleted_item"));
        Assert.False(service.IsGarbageLabel("User"));
        Assert.True(service.IsGarbageLabel(""));
    }

    [Fact]
    public void IsGarbageLabel_WhitespaceOnly_ReturnsTrue()
    {
        var service = CreateServiceWithDefaults();
        Assert.True(service.IsGarbageLabel("  "));
    }

    [Fact]
    public void IsGarbageLabel_BareEqualsFilter_Ignored()
    {
        var service = new GarbageFilterService(new List<string> { "=" });
        Assert.False(service.IsGarbageLabel("anything"));
        Assert.True(service.IsGarbageLabel(""));
    }

    [Fact]
    public void IsGarbageLabel_FourStars_CaughtByDefault()
    {
        var service = CreateServiceWithDefaults();
        Assert.True(service.IsGarbageLabel("****"));
    }

    #endregion

    #region GetFilters

    [Fact]
    public void GetFilters_ReturnsCurrentList()
    {
        var filters = new List<string> { "foo", "=bar" };
        var service = new GarbageFilterService(filters);
        Assert.Equal(2, service.GetFilters().Count);
        Assert.Contains("foo", service.GetFilters());
        Assert.Contains("=bar", service.GetFilters());
    }

    #endregion

    private static GarbageFilterService CreateServiceWithDefaults()
    {
        return new GarbageFilterService(new List<string>
        {
            "deleted", "padding", "reserved", "xp2spec",
            "=User", "=****", "=blank", "=invalid"
        });
    }
}
