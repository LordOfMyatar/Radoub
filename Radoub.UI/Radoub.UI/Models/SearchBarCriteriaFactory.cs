using Radoub.Formats.Search;

namespace Radoub.UI.Models;

/// <summary>
/// Pure builder for the GFF find/replace <see cref="SearchCriteria"/> from SearchBar
/// inputs (#2360). Extracted from <c>SearchBar.BuildCriteria</c> so the criteria
/// assembly — pattern, regex/case flags, and the category-tag → enum parse — can be
/// unit-tested without an Avalonia control. The control delegates to this.
/// </summary>
public static class SearchBarCriteriaFactory
{
    /// <summary>
    /// Builds search criteria from the raw SearchBar control values.
    /// </summary>
    /// <param name="pattern">The search pattern text.</param>
    /// <param name="isRegex">Whether the pattern is a regular expression.</param>
    /// <param name="caseSensitive">Whether matching is case-sensitive.</param>
    /// <param name="categoryTag">
    /// The selected field-filter ComboBoxItem tag. When it parses to a
    /// <see cref="SearchFieldCategory"/>, the criteria are restricted to that single
    /// category; otherwise (null, empty, or an "all"/unparseable tag) no category
    /// filter is applied.
    /// </param>
    public static SearchCriteria Build(string pattern, bool isRegex, bool caseSensitive, string? categoryTag)
    {
        SearchFieldCategory[]? categoryFilter = null;

        if (!string.IsNullOrEmpty(categoryTag) &&
            Enum.TryParse<SearchFieldCategory>(categoryTag, out var category))
        {
            categoryFilter = new[] { category };
        }

        return new SearchCriteria
        {
            Pattern = pattern,
            IsRegex = isRegex,
            CaseSensitive = caseSensitive,
            CategoryFilter = categoryFilter
        };
    }
}
