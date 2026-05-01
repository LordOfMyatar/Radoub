using System.Globalization;
using MerchantEditor.Converters;
using Xunit;

namespace MerchantEditor.Tests.Controls;

/// <summary>
/// Tests for the converters powering StoreItemExtrasPanel display logic.
/// The panel renders Sell/Buy price (currency), Infinite (Yes ∞ / No), and StorePanel (name).
/// </summary>
public class StoreItemExtrasDisplayTests
{
    #region StorePanelNameConverter

    [Theory]
    [InlineData(0, "Armor")]
    [InlineData(1, "Miscellaneous")]
    [InlineData(2, "Potions/Scrolls")]
    [InlineData(3, "Rings/Amulets")]
    [InlineData(4, "Weapons")]
    public void StorePanelName_KnownIds_ReturnExpectedNames(int panelId, string expected)
    {
        var result = StorePanelNameConverter.Instance.Convert(
            panelId, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StorePanelName_UnknownId_ReturnsUnknownLabel()
    {
        var result = StorePanelNameConverter.Instance.Convert(
            99, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("Unknown (99)", result);
    }

    [Fact]
    public void StorePanelName_NonInt_ReturnsEmpty()
    {
        var result = StorePanelNameConverter.Instance.Convert(
            "not-an-int", typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal(string.Empty, result);
    }

    #endregion

    #region InfiniteDisplayConverter

    [Fact]
    public void InfiniteDisplay_True_ReturnsYesInfinity()
    {
        var result = InfiniteDisplayConverter.Instance.Convert(
            true, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("Yes ∞", result);
    }

    [Fact]
    public void InfiniteDisplay_False_ReturnsNo()
    {
        var result = InfiniteDisplayConverter.Instance.Convert(
            false, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("No", result);
    }

    [Fact]
    public void InfiniteDisplay_NonBool_ReturnsEmpty()
    {
        var result = InfiniteDisplayConverter.Instance.Convert(
            null, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal(string.Empty, result);
    }

    #endregion
}
