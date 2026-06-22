using System.Collections.Generic;
using System.Linq;
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
    /// <c>Content</c> is the disambiguated display label (see <see cref="BuildDisplayLabels"/>)
    /// and <c>Tag</c> is the byte <c>Id</c> (so
    /// <see cref="ComboBoxHelper.SelectByTag{T}"/> / <see cref="ComboBoxHelper.GetSelectedTag{T}"/>
    /// work). Falls back to <see cref="DefaultFallback"/> when the list is null/empty.
    /// Returns the list actually loaded (caller can cache it).
    /// </summary>
    public static IReadOnlyList<PaletteCategory> Populate(ComboBox? combo, IReadOnlyList<PaletteCategory>? categories)
    {
        var list = (categories != null && categories.Count > 0) ? categories : DefaultFallback;

        if (combo != null)
        {
            var labels = BuildDisplayLabels(list);
            combo.Items.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                combo.Items.Add(new ComboBoxItem { Content = labels[i], Tag = list[i].Id });
            }
        }

        return list;
    }

    /// <summary>
    /// Build one display label per category so nesting is visible and duplicates are
    /// distinguishable (#2488). Rules, applied per category:
    /// <list type="bullet">
    /// <item>Top-level (no <c>ParentPath</c>) → bare <c>Name</c>, so the common flat palette reads
    /// cleanly.</item>
    /// <item>Nested (has a <c>ParentPath</c>) → always <c>"Parent › Name"</c>, even when the name is
    /// unique, so siblings under one parent read consistently (e.g. all of Armor's children show
    /// "Armor › …", not just a duplicated one).</item>
    /// <item>If the resulting label still collides (same name with no path, or identical
    /// name+path) → append <c>" (id N)"</c>.</item>
    /// </list>
    /// Display-only — placement is by PaletteID (#2477), so the item Tag stays the raw <c>Id</c>.
    /// The order of the returned labels matches <paramref name="categories"/>.
    /// </summary>
    public static IReadOnlyList<string> BuildDisplayLabels(IReadOnlyList<PaletteCategory> categories)
    {
        // Base label: bare for top-level, path-qualified for nested. Count collisions on this base
        // so we only add an id suffix where the base label is genuinely ambiguous.
        var baseLabels = new List<string>(categories.Count);
        var baseCounts = new Dictionary<string, int>();
        foreach (var cat in categories)
        {
            var label = BaseLabel(cat);
            baseLabels.Add(label);
            baseCounts[label] = baseCounts.TryGetValue(label, out var n) ? n + 1 : 1;
        }

        var labels = new List<string>(categories.Count);
        for (int i = 0; i < categories.Count; i++)
        {
            var label = baseLabels[i];
            labels.Add(baseCounts[label] <= 1 ? label : $"{label} (id {categories[i].Id})");
        }

        return labels;
    }

    // Bare name for a top-level category; "Parent › Name" for a nested one.
    private static string BaseLabel(PaletteCategory cat)
        => string.IsNullOrEmpty(cat.ParentPath) ? cat.Name : $"{cat.ParentPath} › {cat.Name}";

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
