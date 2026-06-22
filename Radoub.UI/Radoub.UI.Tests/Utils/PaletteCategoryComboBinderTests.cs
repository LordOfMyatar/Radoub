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

    // --- Duplicate/nested name disambiguation (#2488) ---

    [Fact]
    public void BuildDisplayLabels_UniqueNames_ReturnsBareNames()
    {
        var cats = new List<PaletteCategory>
        {
            new() { Id = 0, Name = "Containers" },
            new() { Id = 1, Name = "Doors" },
        };

        var labels = PaletteCategoryComboBinder.BuildDisplayLabels(cats);

        Assert.Equal(new[] { "Containers", "Doors" }, labels);
    }

    [Fact]
    public void BuildDisplayLabels_DuplicateName_WithParentPath_QualifiesByPath()
    {
        // CEP case: two "Custom 1" categories under different branches.
        var cats = new List<PaletteCategory>
        {
            new() { Id = 10, Name = "Custom 1", ParentPath = "Weapons" },
            new() { Id = 20, Name = "Custom 1", ParentPath = "Armor" },
            new() { Id = 30, Name = "Doors" },
        };

        var labels = PaletteCategoryComboBinder.BuildDisplayLabels(cats);

        Assert.Equal("Weapons › Custom 1", labels[0]);
        Assert.Equal("Armor › Custom 1", labels[1]);
        Assert.Equal("Doors", labels[2]); // unique name stays bare
    }

    [Fact]
    public void BuildDisplayLabels_DuplicateName_NoParentPath_QualifiesById()
    {
        // Both duplicates are top-level (no ParentPath) — fall back to id suffix.
        var cats = new List<PaletteCategory>
        {
            new() { Id = 41, Name = "Custom 1" },
            new() { Id = 42, Name = "Custom 1" },
        };

        var labels = PaletteCategoryComboBinder.BuildDisplayLabels(cats);

        Assert.Equal("Custom 1 (id 41)", labels[0]);
        Assert.Equal("Custom 1 (id 42)", labels[1]);
    }

    [Fact]
    public void BuildDisplayLabels_DuplicateNameAndDuplicatePath_FallsBackToId()
    {
        // Pathological: same name AND same parent path — path can't disambiguate, use id.
        var cats = new List<PaletteCategory>
        {
            new() { Id = 7, Name = "Custom 1", ParentPath = "Weapons" },
            new() { Id = 8, Name = "Custom 1", ParentPath = "Weapons" },
        };

        var labels = PaletteCategoryComboBinder.BuildDisplayLabels(cats);

        Assert.Equal("Weapons › Custom 1 (id 7)", labels[0]);
        Assert.Equal("Weapons › Custom 1 (id 8)", labels[1]);
    }

    [AvaloniaFact]
    public void Populate_DuplicateNames_UsesDisambiguatedContent()
    {
        var combo = new ComboBox();
        var cats = new List<PaletteCategory>
        {
            new() { Id = 10, Name = "Custom 1", ParentPath = "Weapons" },
            new() { Id = 20, Name = "Custom 1", ParentPath = "Armor" },
        };

        PaletteCategoryComboBinder.Populate(combo, cats);

        var first = Assert.IsType<ComboBoxItem>(combo.Items[0]);
        var second = Assert.IsType<ComboBoxItem>(combo.Items[1]);
        Assert.Equal("Weapons › Custom 1", first.Content);
        Assert.Equal("Armor › Custom 1", second.Content);
        // Tags remain the raw PaletteID — disambiguation is display-only (#2488).
        Assert.Equal((byte)10, first.Tag);
        Assert.Equal((byte)20, second.Tag);
    }
}
