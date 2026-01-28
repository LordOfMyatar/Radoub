using System;
using System.Linq;

namespace Radoub.Formats.Common;

/// <summary>
/// Shared utilities for TLK string validation and Aurora Engine label formatting.
/// Used across tools that read game data (baseitems.2da, TLK strings, etc.).
/// </summary>
public static class TlkHelper
{
    /// <summary>
    /// Check if a TLK string is a valid, displayable value.
    /// Returns false for placeholder/garbage values like "BadStrRef", "DELETED", etc.
    /// </summary>
    /// <param name="value">The TLK string to validate</param>
    /// <returns>True if the string is valid for display, false if it's a placeholder</returns>
    public static bool IsValidTlkString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();

        // Common placeholder values in NWN TLK files
        return !trimmed.Equals("BadStrRef", StringComparison.OrdinalIgnoreCase) &&
               !trimmed.Equals("BadStreff", StringComparison.OrdinalIgnoreCase) && // Common typo variant
               !trimmed.Equals("Bad Strref", StringComparison.OrdinalIgnoreCase) &&
               !trimmed.Equals("DELETED", StringComparison.OrdinalIgnoreCase) &&
               !trimmed.Equals("DELETE_ME", StringComparison.OrdinalIgnoreCase) &&
               !trimmed.Equals("Padding", StringComparison.OrdinalIgnoreCase) &&
               !trimmed.Equals("PAdding", StringComparison.OrdinalIgnoreCase) &&
               !trimmed.StartsWith("Bad Str", StringComparison.OrdinalIgnoreCase) &&
               !trimmed.StartsWith("Xp2spec", StringComparison.OrdinalIgnoreCase) &&
               !trimmed.Contains("deleted", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Check if a 2DA label is a garbage/placeholder entry that should be skipped.
    /// </summary>
    /// <param name="label">The 2DA label to check</param>
    /// <returns>True if the label represents deleted/padding content</returns>
    public static bool IsGarbageLabel(string label)
    {
        if (string.IsNullOrEmpty(label))
            return true;

        return label.Contains("deleted", StringComparison.OrdinalIgnoreCase) ||
               label.Contains("padding", StringComparison.OrdinalIgnoreCase) ||
               label.StartsWith("xp2spec", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Format a base item label for display.
    /// Converts "BASE_ITEM_SHORTSWORD" to "Shortsword".
    /// </summary>
    /// <param name="label">The 2DA label (e.g., BASE_ITEM_SHORTSWORD)</param>
    /// <returns>Human-readable name (e.g., "Shortsword")</returns>
    public static string FormatBaseItemLabel(string label)
    {
        if (string.IsNullOrEmpty(label))
            return string.Empty;

        // Remove common prefixes
        if (label.StartsWith("BASE_ITEM_", StringComparison.OrdinalIgnoreCase))
            label = label.Substring(10);

        // Convert underscores to spaces and title case each word
        return string.Join(" ", label.Split('_')
            .Where(w => w.Length > 0)
            .Select(w => char.ToUpper(w[0]) + w.Substring(1).ToLower()));
    }
}
