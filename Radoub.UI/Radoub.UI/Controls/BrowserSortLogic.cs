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

        return mode switch
        {
            BrowserSortMode.Name => filtered
                .OrderBy(e => e.IsFromHak ? 1 : 0)
                .ThenBy(e => string.IsNullOrEmpty(e.DisplayLabel) ? 1 : 0)
                .ThenBy(e => e.DisplayLabel ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            BrowserSortMode.Tag => filtered
                .OrderBy(e => e.IsFromHak ? 1 : 0)
                .ThenBy(e => string.IsNullOrEmpty(e.Tag) ? 1 : 0)
                .ThenBy(e => e.Tag ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            _ => filtered
                .OrderBy(e => e.IsFromHak ? 1 : 0)
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }
}
