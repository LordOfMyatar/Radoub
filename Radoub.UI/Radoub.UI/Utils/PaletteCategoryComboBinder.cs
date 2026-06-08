using System.Collections.Generic;
using Avalonia.Controls;
using Radoub.Formats.Services;

namespace Radoub.UI.Utils;

/// <summary>
/// Shared glue for binding a blueprint's palette category (<c>PaletteID</c>) to a ComboBox.
/// Extracted from the per-tool copies in Relique, Fence, and Quartermaster (#2416); migrations
/// tracked by #2421 (QM), #2422 (Fence), #2423 (Relique).
///
/// Categories come from <see cref="IGameDataService.GetPaletteCategories"/> (the tool's
/// <c>*pal.itp</c> skeleton — never hardcoded). This helper only owns the UI binding: populate
/// the combo from the category list, select by PaletteID, and read the selected id back.
/// </summary>
public static class PaletteCategoryComboBinder
{
    /// <summary>
    /// Hardcoded fallback used only when the game-data lookup yields no categories (e.g. the
    /// <c>*pal.itp</c> skeleton is missing). Matches the legacy Relique fallback list.
    /// </summary>
    public static IReadOnlyList<PaletteCategory> DefaultFallback { get; } = new List<PaletteCategory>
    {
        new() { Id = 0, Name = "Miscellaneous" },
        new() { Id = 1, Name = "Armor" },
        new() { Id = 2, Name = "Weapons" },
        new() { Id = 3, Name = "Potions" },
        new() { Id = 4, Name = "Other" },
    };

    /// <summary>
    /// Clear and repopulate the combo from <paramref name="categories"/>. Each item's
    /// <c>Content</c> is the category name and <c>Tag</c> is the byte <c>Id</c> (so
    /// <see cref="ComboBoxHelper.SelectByTag{T}"/> / <see cref="ComboBoxHelper.GetSelectedTag{T}"/>
    /// work). Falls back to <see cref="DefaultFallback"/> when the list is null/empty.
    /// Returns the list actually loaded (caller can cache it).
    /// </summary>
    public static IReadOnlyList<PaletteCategory> Populate(ComboBox? combo, IReadOnlyList<PaletteCategory>? categories)
    {
        var list = (categories != null && categories.Count > 0) ? categories : DefaultFallback;

        if (combo != null)
        {
            combo.Items.Clear();
            foreach (var cat in list)
            {
                combo.Items.Add(new ComboBoxItem { Content = cat.Name, Tag = cat.Id });
            }
        }

        return list;
    }

    /// <summary>
    /// Select the item whose Tag matches <paramref name="paletteId"/>. If not present, selects the
    /// first item (does NOT inject a synthetic row — palette categories are a closed set).
    /// </summary>
    public static void SelectById(ComboBox? combo, byte paletteId)
    {
        if (combo == null) return;

        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item && item.Tag is byte id && id == paletteId)
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    /// <summary>
    /// The PaletteID of the currently selected item, or null if nothing selected / wrong Tag type.
    /// </summary>
    public static byte? GetSelectedId(ComboBox? combo)
        => ComboBoxHelper.GetSelectedTag<byte>(combo);
}
