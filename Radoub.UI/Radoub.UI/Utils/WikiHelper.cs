using System;
using System.Collections.Generic;
using System.Diagnostics;
using Radoub.Formats.Logging;

namespace Radoub.UI.Utils;

/// <summary>
/// Resolves and opens per-tool documentation on the Radoub wiki (#2061).
///
/// Every tool's Help menu points at its own wiki page rather than the wiki root,
/// so users land on documentation for the tool they are actually using.
/// </summary>
public static class WikiHelper
{
    private const string WikiRoot = "https://github.com/LordOfMyatar/Radoub/wiki";

    /// <summary>
    /// Wiki page per tool. Page names are the wiki's own, minus the .md extension.
    /// Parley is the exception: it has no "Parley" landing page, so it uses the
    /// Getting Started page that Home.md links to.
    /// </summary>
    private static readonly Dictionary<string, string> ToolPages =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Parley"] = "Parley-Getting-Started",
            ["Manifest"] = "Manifest",
            ["Quartermaster"] = "Quartermaster",
            ["Fence"] = "Fence",
            ["Relique"] = "Relique",
            ["Reliquary"] = "Reliquary",
            ["Trebuchet"] = "Trebuchet",
            ["Marlinspike"] = "Marlinspike",
        };

    /// <summary>
    /// Gets the wiki URL for a tool, falling back to the wiki root when the tool is
    /// unknown or unnamed — a valid destination beats a broken link.
    /// </summary>
    public static string GetToolWikiUrl(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return WikiRoot;

        return ToolPages.TryGetValue(toolName.Trim(), out var page)
            ? $"{WikiRoot}/{page}"
            : WikiRoot;
    }

    /// <summary>
    /// Opens the tool's wiki documentation in the default browser.
    /// </summary>
    /// <returns>True if the browser was launched; false if it failed (already logged).</returns>
    public static bool OpenToolDocumentation(string? toolName)
    {
        var url = GetToolWikiUrl(toolName);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"Failed to open documentation URL for '{toolName}': {ex.Message}");
            return false;
        }
    }
}
