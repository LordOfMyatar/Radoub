using System.Text.RegularExpressions;

namespace Radoub.Formats.Search;

/// <summary>
/// Defines what to search for and how to match.
/// </summary>
public class SearchCriteria
{
    /// <summary>Search pattern (plain text or regex)</summary>
    public required string Pattern { get; init; }

    /// <summary>Treat Pattern as a regular expression</summary>
    public bool IsRegex { get; init; }

    /// <summary>Case-sensitive matching</summary>
    public bool CaseSensitive { get; init; }

    /// <summary>Match whole words only</summary>
    public bool WholeWord { get; init; }

    /// <summary>
    /// Specific field names to search (null = all registered fields).
    /// Names must match FieldDefinition.Name values.
    /// </summary>
    public IReadOnlyList<string>? FieldFilter { get; init; }

    /// <summary>Filter by field type (null = all types)</summary>
    public IReadOnlyList<SearchFieldType>? FieldTypeFilter { get; init; }

    /// <summary>Filter by field category (null = all categories)</summary>
    public IReadOnlyList<SearchFieldCategory>? CategoryFilter { get; init; }

    /// <summary>
    /// Filter by file type — applied at discovery time to skip parsing irrelevant files.
    /// Used by ModuleSearchService, ignored by individual providers.
    /// Null = search all registered types.
    /// </summary>
    public IReadOnlyList<ushort>? FileTypeFilter { get; init; }

    /// <summary>
    /// Validates the pattern. Returns null if valid, error message if invalid.
    /// </summary>
    public string? Validate()
    {
        if (string.IsNullOrEmpty(Pattern))
            return "Search pattern is empty";

        if (IsRegex)
        {
            try { _ = new Regex(Pattern); }
            catch (RegexParseException ex) { return $"Invalid regex: {ex.Message}"; }
        }
        return null;
    }

    /// <summary>
    /// Compiles the pattern into a Regex for matching.
    /// Call Validate() first — this throws on invalid patterns.
    /// Call once and reuse across multiple searches.
    /// </summary>
    public Regex ToRegex()
    {
        var options = RegexOptions.Compiled;
        if (!CaseSensitive)
            options |= RegexOptions.IgnoreCase;

        var pattern = Pattern;

        if (!IsRegex)
            pattern = Regex.Escape(pattern);

        if (WholeWord)
            pattern = $@"\b{pattern}\b";

        return new Regex(pattern, options);
    }

    /// <summary>
    /// Returns true if the given field definition passes all filters.
    /// </summary>
    public bool MatchesField(FieldDefinition field)
    {
        if (FieldFilter != null && !FieldFilter.Contains(field.Name))
            return false;
        if (FieldTypeFilter != null && !FieldTypeFilter.Contains(field.FieldType))
            return false;
        if (CategoryFilter != null && !CategoryFilter.Contains(field.Category))
            return false;
        return true;
    }
}
