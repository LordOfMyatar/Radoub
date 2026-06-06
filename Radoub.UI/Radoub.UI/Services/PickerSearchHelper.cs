using System;
using System.Linq;

namespace Radoub.UI.Services;

/// <summary>
/// Shared, UI-free match helper for the tool picker search boxes (#2360).
///
/// Eight picker windows (Creature/Spell/Class/Portrait/Soundset in Quartermaster,
/// Quest/Sound in Parley, Store in Fence) each hand-rolled the same
/// "name-contains OR id-contains" filter in OnSearchTextChanged with no test seam.
/// Centralizing the comparison here gives one tested implementation they can all
/// delegate to.
/// </summary>
public static class PickerSearchHelper
{
    /// <summary>
    /// Returns true if <paramref name="searchText"/> is empty (match everything) or
    /// is a case-insensitive substring of any of the supplied candidate fields.
    /// Null candidate fields are skipped.
    /// </summary>
    /// <param name="searchText">The (already trimmed or raw) search text. Empty/null matches all.</param>
    /// <param name="fields">Candidate text fields to test (e.g. name, id, tag).</param>
    public static bool Matches(string? searchText, params string?[] fields)
    {
        if (string.IsNullOrEmpty(searchText))
            return true;

        if (fields == null)
            return false;

        return fields.Any(f =>
            !string.IsNullOrEmpty(f) &&
            f.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }
}
