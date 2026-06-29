using System;
using System.Collections.Generic;
using System.Linq;

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

        var allSourcesUnchecked = !showBif && !showHak && !showOverride;
        var noTextFilter = string.IsNullOrWhiteSpace(searchText);
        var parsedExcludes = ParseExcludePatterns(excludePatterns);

        // All sources unchecked = show nothing (user explicitly deselected all)
        if (allSourcesUnchecked)
            return new List<AppearanceInfo>();

        var result = new List<AppearanceInfo>();

        foreach (var app in appearances)
        {
            if (!MatchesSourceFilter(app.Source, showBif, showHak, showOverride))
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
        // Row-number search (#2027): an all-digit query also matches the exact AppearanceId,
        // so typing "175" surfaces row 175 even when "175" appears nowhere in its text. The
        // substring match still runs, so digit queries also catch names containing the digits.
        // All-digits check (not just int.TryParse, which would accept "+5"/" 5"/"-5" as a row).
        var trimmed = searchText.Trim();
        if (trimmed.Length > 0 && trimmed.All(char.IsDigit)
            && int.TryParse(trimmed, out int rowNumber)
            && app.AppearanceId == rowNumber)
        {
            return true;
        }

        return app.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || app.Label.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || app.Race.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parse the maker/author of a CEP appearance from its label's parentheses (#2028).
    /// CEP labels append the author in a trailing group, e.g. "Bear: Black (Shemsu-Heru)".
    /// Some labels also carry a model-resref group (e.g. "...(Darklight) (c_abishaibl)"); the
    /// group equal to <paramref name="modelRef"/> is skipped so the real author is returned.
    /// The synthetic "(Dynamic)" UI prefix is treated as not-a-maker.
    /// Returns the empty string when no maker group is present.
    /// </summary>
    /// <param name="label">Raw appearance.2da LABEL (not the UI-decorated display name).</param>
    /// <param name="modelRef">Known model resref to exclude from maker candidates.</param>
    public static string ParseMaker(string label, string modelRef)
    {
        if (string.IsNullOrEmpty(label))
            return "";

        var matches = System.Text.RegularExpressions.Regex.Matches(label, @"\(([^()]*)\)");
        if (matches.Count == 0)
            return "";

        var model = modelRef?.Trim() ?? "";

        // Walk from the last parenthetical backward; skip groups that are the model resref or
        // the synthetic "Dynamic" prefix. Return the first real author group found.
        for (int i = matches.Count - 1; i >= 0; i--)
        {
            var group = matches[i].Groups[1].Value.Trim();
            if (group.Length == 0)
                continue;
            if (model.Length > 0 && group.Equals(model, StringComparison.OrdinalIgnoreCase))
                continue;
            if (group.Equals("Dynamic", StringComparison.OrdinalIgnoreCase))
                continue;
            return group;
        }

        return "";
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
