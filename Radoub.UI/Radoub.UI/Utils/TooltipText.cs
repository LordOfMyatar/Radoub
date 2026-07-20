using System;
using System.Text.RegularExpressions;

namespace Radoub.UI.Utils;

/// <summary>
/// Prepares text for display in a tooltip (#1567).
///
/// Tooltips are for SHORT supplementary hints. Game-data descriptions from the TLK
/// (feats, spells, item properties) are frequently several hundred characters of
/// prose with hard line breaks — dumped into a tooltip they render as an enormous
/// unreadable block.
///
/// Use <see cref="Summarize"/> for the hint, and put the full text in a details
/// panel or info popup where it can be read properly.
/// </summary>
public static class TooltipText
{
    /// <summary>
    /// Maximum tooltip length. Roughly two lines at the shared 400px MaxWidth.
    /// </summary>
    public const int MaxLength = 160;

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Collapses whitespace and truncates on a word boundary, appending an ellipsis
    /// when text was dropped.
    /// </summary>
    public static string Summarize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        // TLK entries carry hard line breaks that wrap badly inside a tooltip.
        var normalized = Whitespace.Replace(text, " ").Trim();

        if (normalized.Length <= MaxLength)
            return normalized;

        // Reserve one char for the ellipsis, then back up to the last whole word so
        // the hint never ends mid-word.
        var cut = normalized[..(MaxLength - 1)];
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > 0)
            cut = cut[..lastSpace];

        return cut.TrimEnd() + "…";
    }
}
