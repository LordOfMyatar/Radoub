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
    /// Build one display label per category, disambiguating duplicate names (#2488). CEP and
    /// custom content commonly repeat names like "Custom 1" across branches; a flat combo of
    /// identical labels is unusable. Rules, applied per category:
    /// <list type="bullet">
    /// <item>Name unique in the list → bare <c>Name</c> (unchanged from the simple case).</item>
    /// <item>Name duplicated, has a <c>ParentPath</c> → <c>"Parent › Name"</c>.</item>
    /// <item>Name still ambiguous (no path, or same name+path) → append <c>" (id N)"</c>.</item>
    /// </list>
    /// Disambiguation is display-only — placement is by PaletteID (#2477), so the item Tag stays
    /// the raw <c>Id</c>. The order of the returned labels matches <paramref name="categories"/>.
    /// </summary>
    public static IReadOnlyList<string> BuildDisplayLabels(IReadOnlyList<PaletteCategory> categories)
    {
        var nameCounts = categories
            .GroupBy(c => c.Name)
            .ToDictionary(g => g.Key, g => g.Count());

        // After path-qualification, a "Parent › Name" string can still collide (same name AND
        // same parent path). Detect those so we can add the id suffix only where it's needed.
        var qualifiedCounts = new Dictionary<string, int>();
        foreach (var cat in categories)
        {
            var key = PathQualified(cat);
            qualifiedCounts[key] = qualifiedCounts.TryGetValue(key, out var n) ? n + 1 : 1;
        }

        var labels = new List<string>(categories.Count);
        foreach (var cat in categories)
        {
            if (nameCounts[cat.Name] <= 1)
            {
                labels.Add(cat.Name); // unique — leave it bare
                continue;
            }

            var qualified = PathQualified(cat);
            // Path disambiguates only if the path-qualified form is itself unique.
            labels.Add(qualifiedCounts[qualified] <= 1 ? qualified : $"{qualified} (id {cat.Id})");
        }

        return labels;
    }

    private static string PathQualified(PaletteCategory cat)
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
