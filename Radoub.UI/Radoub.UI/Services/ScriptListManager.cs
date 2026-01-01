using System;
using System.Collections.Generic;
using System.Linq;

namespace Radoub.UI.Services;

/// <summary>
/// Result of filtering and merging script lists for display.
/// </summary>
public class ScriptListResult
{
    public List<ScriptEntry> Scripts { get; set; } = new();
    public int ModuleCount { get; set; }
    public int HakCount { get; set; }
    public int BuiltInCount { get; set; }

    public int TotalCount => Scripts.Count;
}

/// <summary>
/// Manages script list filtering, merging, and sorting for ScriptBrowserWindow.
/// Handles the priority order: Module > HAK > Built-in.
/// </summary>
public static class ScriptListManager
{
    /// <summary>
    /// Combines script lists from multiple sources respecting override priority.
    /// Module scripts override HAK scripts, which override built-in scripts.
    /// </summary>
    /// <param name="moduleScripts">Scripts from module directory (highest priority)</param>
    /// <param name="hakScripts">Scripts from HAK files</param>
    /// <param name="builtInScripts">Built-in game scripts (lowest priority)</param>
    /// <param name="includeHak">Whether to include HAK scripts</param>
    /// <param name="includeBuiltIn">Whether to include built-in scripts</param>
    /// <param name="searchText">Optional search filter (case-insensitive)</param>
    /// <returns>Filtered and sorted list with counts</returns>
    public static ScriptListResult CombineAndFilter(
        IReadOnlyList<ScriptEntry> moduleScripts,
        IReadOnlyList<ScriptEntry> hakScripts,
        IReadOnlyList<ScriptEntry> builtInScripts,
        bool includeHak,
        bool includeBuiltIn,
        string? searchText = null)
    {
        var allScripts = new List<ScriptEntry>(moduleScripts);

        // Add HAK scripts that aren't in module list
        if (includeHak)
        {
            foreach (var hakScript in hakScripts)
            {
                if (!HasScript(allScripts, hakScript.Name))
                {
                    allScripts.Add(hakScript);
                }
            }
        }

        // Add built-in scripts that aren't in module or HAK list
        if (includeBuiltIn)
        {
            foreach (var builtIn in builtInScripts)
            {
                if (!HasScript(allScripts, builtIn.Name))
                {
                    allScripts.Add(builtIn);
                }
            }
        }

        // Apply search filter
        var filtered = allScripts;
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var search = searchText.ToLowerInvariant();
            filtered = allScripts
                .Where(s => s.Name.ToLowerInvariant().Contains(search))
                .ToList();
        }

        // Sort: module scripts first, then HAK, then built-in, all alphabetical
        var sorted = filtered
            .OrderBy(s => s.IsBuiltIn ? 2 : s.IsFromHak ? 1 : 0)
            .ThenBy(s => s.Name)
            .ToList();

        return new ScriptListResult
        {
            Scripts = sorted,
            ModuleCount = sorted.Count(s => !s.IsBuiltIn && !s.IsFromHak),
            HakCount = sorted.Count(s => s.IsFromHak),
            BuiltInCount = sorted.Count(s => s.IsBuiltIn)
        };
    }

    /// <summary>
    /// Formats the script count for display in the UI.
    /// </summary>
    public static string FormatCountText(ScriptListResult result)
    {
        var countText = $"{result.ModuleCount} module";
        if (result.HakCount > 0)
        {
            countText += $" + {result.HakCount} 📦 HAK";
        }
        if (result.BuiltInCount > 0)
        {
            countText += $" + {result.BuiltInCount} 🎮 built-in";
        }
        return countText;
    }

    private static bool HasScript(IReadOnlyList<ScriptEntry> scripts, string name)
    {
        return scripts.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
