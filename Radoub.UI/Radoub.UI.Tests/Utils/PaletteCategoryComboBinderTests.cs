using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Radoub.Formats.Services;
using Radoub.UI.Utils;
using Xunit;

namespace Radoub.UI.Tests.Utils;

/// <summary>
/// Tests for the shared palette-category combo binder (#2416). Populate from a category list,
/// select by PaletteID, read the selected id back, and fall back when no categories exist.
/// </summary>
public class PaletteCategoryComboBinderTests
{
    private static List<PaletteCategory> SampleCategories() => new()
    {
        new() { Id = 0, Name = "Containers" },
        new() { Id = 5, Name = "Doors" },
        new() { Id = 9, Name = "Custom" },
    };

    [AvaloniaFact]
    public void Populate_AddsOneItemPerCategory_WithIdAsTag()
    {
        var combo = new ComboBox();

        PaletteCategoryComboBinder.Populate(combo, SampleCategories());

        Assert.Equal(3, combo.Items.Count);
        var first = Assert.IsType<ComboBoxItem>(combo.Items[0]);
        Assert.Equal("Containers", first.Content);
        Assert.Equal((byte)0, first.Tag);
        var third = Assert.IsType<ComboBoxItem>(combo.Items[2]);
        Assert.Equal((byte)9, third.Tag);
    }

    [AvaloniaFact]
    public void Populate_NullOrEmpty_UsesFallback()
    {
        var combo = new ComboBox();

        var loaded = PaletteCategoryComboBinder.Populate(combo, null);

        Assert.Equal(PaletteCategoryComboBinder.DefaultFallback.Count, combo.Items.Count);
        Assert.Same(PaletteCategoryComboBinder.DefaultFallback, loaded);
    }

    [AvaloniaFact]
    public void SelectById_SelectsMatchingTag()
    {
        var combo = new ComboBox();
        PaletteCategoryComboBinder.Populate(combo, SampleCategories());

        PaletteCategoryComboBinder.SelectById(combo, 5);

        Assert.Equal(1, combo.SelectedIndex);
        Assert.Equal((byte)5, PaletteCategoryComboBinder.GetSelectedId(combo));
    }

    [AvaloniaFact]
    public void SelectById_UnknownId_SelectsFirst()
    {
        var combo = new ComboBox();
        PaletteCategoryComboBinder.Populate(combo, SampleCategories());

        PaletteCategoryComboBinder.SelectById(combo, 200);

        Assert.Equal(0, combo.SelectedIndex);
    }

    [AvaloniaFact]
    public void GetSelectedId_NoSelection_ReturnsNull()
    {
        var combo = new ComboBox();
        PaletteCategoryComboBinder.Populate(combo, SampleCategories());

        Assert.Null(PaletteCategoryComboBinder.GetSelectedId(combo));
    }
}
