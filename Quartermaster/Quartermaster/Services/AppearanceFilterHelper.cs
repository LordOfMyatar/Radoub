using System;
using System.Collections.Generic;

namespace Quartermaster.Services;

/// <summary>
/// Filters appearance lists by text search and resource source.
/// Searches across Name, Label, and Race fields (case-insensitive).
/// </summary>
public static class AppearanceFilterHelper
{
    /// <summary>
    /// Filter appearances by text search and resource source checkboxes.
    /// </summary>
    /// <param name="appearances">Full list of appearances to filter</param>
    /// <param name="searchText">Text to search for in Name, Label, and Race (case-insensitive). Null/empty = no text filter.</param>
    /// <param name="showBif">Include appearances from BIF (base game) archives</param>
    /// <param name="showHak">Include appearances from HAK packs</param>
    /// <param name="showOverride">Include appearances from the Override folder</param>
    /// <param name="excludePatterns">Semicolon-separated patterns to exclude (matched against Name and Label, case-insensitive). Null/empty = no exclusions.</param>
    /// <returns>Filtered list of appearances matching both text and source criteria</returns>
    public static List<AppearanceInfo> Filter(
        List<AppearanceInfo> appearances,
        string? searchText,
        bool showBif,
        bool showHak,
        bool showOverride,
        string? excludePatterns = null)
    {
        if (appearances == null || appearances.Count == 0)
            return new List<AppearanceInfo>();

        var noSourceFilter = !showBif && !showHak && !showOverride;
        var noTextFilter = string.IsNullOrWhiteSpace(searchText);
        var parsedExcludes = ParseExcludePatterns(excludePatterns);

        if (noSourceFilter && noTextFilter && parsedExcludes.Length == 0)
            return new List<AppearanceInfo>(appearances);

        var result = new List<AppearanceInfo>();

        foreach (var app in appearances)
        {
            if (!noSourceFilter && !MatchesSourceFilter(app.Source, showBif, showHak, showOverride))
                continue;

            if (!noTextFilter && !MatchesTextSearch(app, searchText!))
                continue;

            if (parsedExcludes.Length > 0 && MatchesExcludePattern(app, parsedExcludes))
                continue;

            result.Add(app);
        }

        return result;
    }

    private static bool MatchesSourceFilter(AppearanceSource source, bool showBif, bool showHak, bool showOverride)
    {
        return source switch
        {
            AppearanceSource.Bif => showBif,
            AppearanceSource.Hak => showHak,
            AppearanceSource.Override => showOverride,
            AppearanceSource.Unknown => true,
            _ => true
        };
    }

    private static bool MatchesTextSearch(AppearanceInfo app, string searchText)
    {
        return app.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || app.Label.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || app.Race.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parse semicolon-separated exclude patterns into trimmed, non-empty strings.
    /// </summary>
    internal static string[] ParseExcludePatterns(string? excludePatterns)
    {
        if (string.IsNullOrWhiteSpace(excludePatterns))
            return Array.Empty<string>();

        var parts = excludePatterns.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0)
                result.Add(trimmed);
        }
        return result.ToArray();
    }

    private static bool MatchesExcludePattern(AppearanceInfo app, string[] excludePatterns)
    {
        foreach (var pattern in excludePatterns)
        {
            if (app.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase)
                || app.Label.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
