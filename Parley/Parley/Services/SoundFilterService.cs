using System.Collections.Generic;
using System.Linq;
using DialogEditor.Models.Sound;

namespace DialogEditor.Services;

/// <summary>
/// Filters sound lists by mono status and search text.
/// Extracted from SoundBrowserWindow for testability.
/// </summary>
public static class SoundFilterService
{
    /// <summary>
    /// Apply mono and text search filters to a sound collection, returning sorted results.
    /// </summary>
    /// <param name="allSounds">The full unfiltered sound list.</param>
    /// <param name="monoOnly">If true, exclude stereo sounds (keep mono and channel-unknown).</param>
    /// <param name="searchText">Case-insensitive substring filter on FileName. Null/whitespace = no filter.</param>
    /// <returns>Filtered and alphabetically sorted list.</returns>
    public static List<SoundFileInfo> ApplyFilters(
        IEnumerable<SoundFileInfo> allSounds,
        bool monoOnly,
        string? searchText)
    {
        var result = allSounds.AsEnumerable();

        if (monoOnly)
            result = result.Where(s => s.IsMono || s.ChannelUnknown);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var lower = searchText.ToLowerInvariant();
            result = result.Where(s => s.FileName.ToLowerInvariant().Contains(lower));
        }

        return result.OrderBy(s => s.FileName).ToList();
    }
}
