using Radoub.Formats.Common;
using Xunit;

namespace Radoub.Formats.Tests;

public class TlkHelperTests
{
    #region IsValidTlkString Tests

    [Theory]
    [InlineData("Longsword")]
    [InlineData("Magic Weapon +1")]
    [InlineData("Ring of Protection")]
    [InlineData("Elminster")]
    public void IsValidTlkString_ValidStrings_ReturnsTrue(string value)
    {
        Assert.True(TlkHelper.IsValidTlkString(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidTlkString_NullOrEmpty_ReturnsFalse(string? value)
    {
        Assert.False(TlkHelper.IsValidTlkString(value));
    }

    [Theory]
    [InlineData("BadStrRef")]
    [InlineData("BADSTRREF")]
    [InlineData("badstrref")]
    [InlineData("BadStreff")]  // Common typo variant
    [InlineData("Bad Strref")]
    [InlineData("Bad Str 123")]
    public void IsValidTlkString_BadStrRefVariants_ReturnsFalse(string value)
    {
        Assert.False(TlkHelper.IsValidTlkString(value));
    }

    [Theory]
    [InlineData("DELETED")]
    [InlineData("deleted")]
    [InlineData("DELETE_ME")]
    [InlineData("This was deleted")]
    [InlineData("deleted_entry")]
    public void IsValidTlkString_DeletedVariants_ReturnsFalse(string value)
    {
        Assert.False(TlkHelper.IsValidTlkString(value));
    }

    [Theory]
    [InlineData("Padding")]
    [InlineData("PAdding")]  // BioWare typo
    [InlineData("PADDING")]
    public void IsValidTlkString_PaddingVariants_ReturnsFalse(string value)
    {
        Assert.False(TlkHelper.IsValidTlkString(value));
    }

    [Theory]
    [InlineData("Xp2spec1")]
    [InlineData("xp2spec99")]
    public void IsValidTlkString_Xp2specEntries_ReturnsFalse(string value)
    {
        Assert.False(TlkHelper.IsValidTlkString(value));
    }

    #endregion

    #region IsGarbageLabel Tests

    [Theory]
    [InlineData("BASE_ITEM_SHORTSWORD")]
    [InlineData("BASE_ITEM_LONGSWORD")]
    [InlineData("CreatureWeapon")]
    public void IsGarbageLabel_ValidLabels_ReturnsFalse(string label)
    {
        Assert.False(TlkHelper.IsGarbageLabel(label));
    }

    [Theory]
    [InlineData("deleted")]
    [InlineData("DELETED")]
    [InlineData("deleted_item")]
    [InlineData("BASE_ITEM_DELETED")]
    public void IsGarbageLabel_DeletedLabels_ReturnsTrue(string label)
    {
        Assert.True(TlkHelper.IsGarbageLabel(label));
    }

    [Theory]
    [InlineData("padding")]
    [InlineData("PADDING")]
    [InlineData("padding_entry")]
    public void IsGarbageLabel_PaddingLabels_ReturnsTrue(string label)
    {
        Assert.True(TlkHelper.IsGarbageLabel(label));
    }

    [Theory]
    [InlineData("xp2spec1")]
    [InlineData("Xp2spec99")]
    [InlineData("XP2SPEC_PLACEHOLDER")]
    public void IsGarbageLabel_Xp2specLabels_ReturnsTrue(string label)
    {
        Assert.True(TlkHelper.IsGarbageLabel(label));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsGarbageLabel_NullOrEmpty_ReturnsTrue(string? label)
    {
        Assert.True(TlkHelper.IsGarbageLabel(label!));
    }

    #endregion

    #region FormatBaseItemLabel Tests

    [Theory]
    [InlineData("BASE_ITEM_SHORTSWORD", "Shortsword")]
    [InlineData("BASE_ITEM_LONGSWORD", "Longsword")]
    [InlineData("BASE_ITEM_TWOBLADEDSWORD", "Twobladedsword")]
    [InlineData("BASE_ITEM_MAGIC_STAFF", "Magic Staff")]
    public void FormatBaseItemLabel_RemovesPrefix(string label, string expected)
    {
        Assert.Equal(expected, TlkHelper.FormatBaseItemLabel(label));
    }

    [Theory]
    [InlineData("CREATURE_WEAPON", "Creature Weapon")]
    [InlineData("SOME_ITEM_TYPE", "Some Item Type")]
    public void FormatBaseItemLabel_FormatsNonPrefixedLabels(string label, string expected)
    {
        Assert.Equal(expected, TlkHelper.FormatBaseItemLabel(label));
    }

    [Theory]
    [InlineData("shortsword", "Shortsword")]
    [InlineData("LONGSWORD", "Longsword")]
    [InlineData("LongSword", "Longsword")]
    public void FormatBaseItemLabel_TitleCasesSingleWord(string label, string expected)
    {
        Assert.Equal(expected, TlkHelper.FormatBaseItemLabel(label));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void FormatBaseItemLabel_NullOrEmpty_ReturnsEmpty(string? label, string expected)
    {
        Assert.Equal(expected, TlkHelper.FormatBaseItemLabel(label!));
    }

    #endregion
}
