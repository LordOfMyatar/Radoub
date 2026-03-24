using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Services;

/// <summary>
/// Pure logic helpers for skill display in LUW/NCW wizards.
/// Extracted for testability (#1499, #1500).
/// </summary>
public static class SkillDisplayHelper
{
    /// <summary>
    /// Interface for items that can be filtered by name (#1799).
    /// </summary>
    public interface INamedItem
    {
        string Name { get; }
    }

    /// <summary>
    /// Minimal interface for skill filter operations — avoids coupling to UI display items.
    /// </summary>
    public class SkillFilterItem : INamedItem
    {
        public string Name { get; set; } = "";
        public bool IsClassSkill { get; set; }
        public bool IsUnavailable { get; set; }
    }

    /// <summary>
    /// Filters any named items by name using case-insensitive substring matching.
    /// Returns all items if filter is null, empty, or whitespace.
    /// </summary>
    public static List<T> FilterByName<T>(List<T> items, string? filter) where T : INamedItem
    {
        if (string.IsNullOrWhiteSpace(filter))
            return new List<T>(items);

        return items
            .Where(s => s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Returns the class skill indicator text for display.
    /// Unavailable takes precedence over class/cross-class status.
    /// </summary>
    public static string GetClassSkillIndicator(bool isClassSkill, bool isUnavailable)
    {
        if (isUnavailable) return "(unavailable)";
        return isClassSkill ? "(class skill, 1 pt)" : "(cross-class, 2 pts)";
    }

    /// <summary>
    /// Determines whether a skill should use the class skill highlight color (green).
    /// Only available class skills get the highlight — unavailable skills do not.
    /// </summary>
    public static bool ShouldUseClassSkillColor(bool isClassSkill, bool isUnavailable)
    {
        return isClassSkill && !isUnavailable;
    }
}
