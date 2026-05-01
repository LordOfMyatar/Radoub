using System.Globalization;
using Radoub.Formats.Services;
using Radoub.Formats.Uti;
using Radoub.UI.Converters;
using Radoub.UI.ViewModels;
using Xunit;

namespace Radoub.UI.Tests.Converters;

/// <summary>
/// Tests for the converters powering ItemDetailsPanel display logic.
/// These cover the Source/Properties fallback rules previously implemented imperatively
/// in MainWindow.ItemDetails.cs (Fence) and InventoryPanel.axaml.cs (Quartermaster).
/// </summary>
public class ItemDetailsConvertersTests
{
    private static ItemViewModel MakeVm(
        string sourceLocation = "",
        GameResourceSource source = GameResourceSource.Bif,
        string propertiesDisplay = "",
        int propertyCount = 0)
    {
        var properties = new System.Collections.Generic.List<Radoub.Formats.Uti.ItemProperty>();
        for (var i = 0; i < propertyCount; i++)
            properties.Add(new Radoub.Formats.Uti.ItemProperty { PropertyName = (ushort)i });

        var item = new UtiFile { Properties = properties };
        var vm = new ItemViewModel(item, "Test", "Type", propertiesDisplay, source)
        {
            SourceLocation = sourceLocation
        };
        return vm;
    }

    #region SourceDisplayConverter

    [Fact]
    public void SourceDisplay_NonEmptyLocation_ReturnsLocation()
    {
        var vm = MakeVm(sourceLocation: "templates.bif", source: GameResourceSource.Bif);

        var result = ItemViewModelSourceDisplayConverter.Instance.Convert(
            vm, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("templates.bif", result);
    }

    [Fact]
    public void SourceDisplay_EmptyLocation_FallsBackToSourceEnumName()
    {
        var vm = MakeVm(sourceLocation: "", source: GameResourceSource.Override);

        var result = ItemViewModelSourceDisplayConverter.Instance.Convert(
            vm, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("Override", result);
    }

    [Fact]
    public void SourceDisplay_NullViewModel_ReturnsEmpty()
    {
        var result = ItemViewModelSourceDisplayConverter.Instance.Convert(
            null, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void SourceDisplay_WhitespaceLocation_FallsBackToSourceEnumName()
    {
        var vm = MakeVm(sourceLocation: "   ", source: GameResourceSource.Hak);

        var result = ItemViewModelSourceDisplayConverter.Instance.Convert(
            vm, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("Hak", result);
    }

    #endregion

    #region PropertiesDisplayConverter

    [Fact]
    public void PropertiesDisplay_NonEmptyDisplay_ReturnsDisplay()
    {
        var vm = MakeVm(propertiesDisplay: "Enhancement Bonus +1; Damage Bonus: Fire 1d4");

        var result = ItemViewModelPropertiesDisplayConverter.Instance.Convert(
            vm, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("Enhancement Bonus +1; Damage Bonus: Fire 1d4", result);
    }

    [Fact]
    public void PropertiesDisplay_EmptyDisplayWithCount_ReturnsCountSummary()
    {
        var vm = MakeVm(propertiesDisplay: "", propertyCount: 3);

        var result = ItemViewModelPropertiesDisplayConverter.Instance.Convert(
            vm, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("3 properties", result);
    }

    [Fact]
    public void PropertiesDisplay_EmptyDisplayAndZeroCount_ReturnsNone()
    {
        var vm = MakeVm(propertiesDisplay: "", propertyCount: 0);

        var result = ItemViewModelPropertiesDisplayConverter.Instance.Convert(
            vm, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("None", result);
    }

    [Fact]
    public void PropertiesDisplay_NullViewModel_ReturnsEmpty()
    {
        var result = ItemViewModelPropertiesDisplayConverter.Instance.Convert(
            null, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal(string.Empty, result);
    }

    #endregion

    #region TagOrEmDashConverter (display "—" for empty tag, like Fence's existing behavior)

    [Fact]
    public void TagOrEmDash_NonEmptyTag_ReturnsTag()
    {
        var result = StringOrEmDashConverter.Instance.Convert(
            "MERCHANT_01", typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("MERCHANT_01", result);
    }

    [Fact]
    public void TagOrEmDash_EmptyString_ReturnsEmDash()
    {
        var result = StringOrEmDashConverter.Instance.Convert(
            "", typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("—", result);
    }

    [Fact]
    public void TagOrEmDash_Null_ReturnsEmDash()
    {
        var result = StringOrEmDashConverter.Instance.Convert(
            null, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("—", result);
    }

    [Fact]
    public void TagOrEmDash_Whitespace_ReturnsEmDash()
    {
        var result = StringOrEmDashConverter.Instance.Convert(
            "   ", typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("—", result);
    }

    #endregion
}
