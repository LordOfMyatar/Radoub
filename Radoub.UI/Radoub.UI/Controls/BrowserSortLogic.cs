namespace Radoub.UI.Controls;

/// <summary>
/// Pure-logic sort + filter for file browser panels. Extracted as a static
/// test seam so sort/search behavior can be unit-tested without UI binding.
/// </summary>
internal static class BrowserSortLogic
{
    /// <summary>
    /// Apply search filter against the field corresponding to <paramref name="mode"/>,
    /// then sort module-first followed by the sort field.
    /// </summary>
    /// <remarks>
    /// Search semantics:
    /// <list type="bullet">
    /// <item>ResRef → matches <see cref="FileBrowserEntry.Name"/></item>
    /// <item>Name → matches <see cref="FileBrowserEntry.DisplayLabel"/> (empty if null)</item>
    /// <item>Tag → matches <see cref="FileBrowserEntry.Tag"/> (empty if null)</item>
    /// </list>
    /// Sort semantics: module entries (IsFromHak=false) always come before
    /// archive entries; within each tier entries are ordered by the active
    /// sort field. Null DisplayLabel/Tag values sort after non-null ones in
    /// Name/Tag modes so unindexed entries cluster at the bottom of their tier.
    /// </remarks>
    public static List<FileBrowserEntry> FilterAndSort(
        IEnumerable<FileBrowserEntry> entries,
        string? searchText,
        BrowserSortMode mode)
        => FilterAndSort(entries, searchText, mode, BrowserSortDirection.Ascending);

    /// <summary>
    /// Sort direction overload. Descending reverses the active sort field
    /// within each tier; the module-first tier and the null-last placement
    /// for unindexed Name/Tag entries are preserved (so DESC doesn't bubble
    /// unindexed rows to the top of their tier).
    /// </summary>
    public static List<FileBrowserEntry> FilterAndSort(
        IEnumerable<FileBrowserEntry> entries,
        string? searchText,
        BrowserSortMode mode,
        BrowserSortDirection direction)
    {
        var search = string.IsNullOrWhiteSpace(searchText)
            ? null
            : searchText.ToLowerInvariant();

        IEnumerable<FileBrowserEntry> filtered = entries;

        if (search != null)
        {
            filtered = mode switch
            {
                BrowserSortMode.Name => filtered.Where(e =>
                    (e.DisplayLabel ?? string.Empty).ToLowerInvariant().Contains(search)),
                BrowserSortMode.Tag => filtered.Where(e =>
                    (e.Tag ?? string.Empty).ToLowerInvariant().Contains(search)),
                _ => filtered.Where(e =>
                    e.Name.ToLowerInvariant().Contains(search))
            };
        }

        var desc = direction == BrowserSortDirection.Descending;

        return mode switch
        {
            BrowserSortMode.Name => SortByLabel(filtered, e => e.DisplayLabel, desc),
            BrowserSortMode.Tag => SortByLabel(filtered, e => e.Tag, desc),
            _ => SortByResRef(filtered, desc)
        };
    }

    /// <summary>
    /// Sort by ResRef with module-first tier. Descending reverses ResRef order
    /// within each tier; module tier stays before HAK tier either way.
    /// </summary>
    private static List<FileBrowserEntry> SortByResRef(IEnumerable<FileBrowserEntry> filtered, bool desc)
    {
        var ordered = filtered.OrderBy(e => e.IsFromHak ? 1 : 0);
        return (desc
            ? ordered.ThenByDescending(e => e.Name, StringComparer.OrdinalIgnoreCase)
            : ordered.ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Sort by a nullable label field (DisplayLabel/Tag) with module-first tier
    /// and null-last placement preserved regardless of direction.
    /// </summary>
    private static List<FileBrowserEntry> SortByLabel(
        IEnumerable<FileBrowserEntry> filtered,
        Func<FileBrowserEntry, string?> labelSelector,
        bool desc)
    {
        var withTier = filtered
            .OrderBy(e => e.IsFromHak ? 1 : 0)
            .ThenBy(e => string.IsNullOrEmpty(labelSelector(e)) ? 1 : 0);

        var withLabel = desc
            ? withTier.ThenByDescending(e => labelSelector(e) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            : withTier.ThenBy(e => labelSelector(e) ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        // Stable tiebreaker by ResRef matches direction so DESC is fully reversed.
        return (desc
            ? withLabel.ThenByDescending(e => e.Name, StringComparer.OrdinalIgnoreCase)
            : withLabel.ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }
}
