using Avalonia.Controls;
using System;

namespace Quartermaster.Views.Panels;

/// <summary>
/// Helper methods for ComboBox operations with typed Tag values.
/// Eliminates repetitive selection code across panels.
/// </summary>
public static class ComboBoxHelper
{
    /// <summary>
    /// Select a ComboBox item by matching its Tag value.
    /// If not found, adds a new item with the value.
    /// </summary>
    /// <typeparam name="T">Type of the Tag value (byte, ushort, int, uint, etc.)</typeparam>
    /// <param name="combo">The ComboBox to select in</param>
    /// <param name="value">The value to match against item Tags</param>
    /// <param name="formatNotFound">Optional format string for new items (default: just the value)</param>
    public static void SelectByTag<T>(ComboBox? combo, T value, string? formatNotFound = null) where T : struct, IEquatable<T>
    {
        if (combo == null) return;

        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item && item.Tag is T id && id.Equals(value))
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        // Not found - add it
        var content = formatNotFound != null
            ? string.Format(formatNotFound, value)
            : value.ToString();
        combo.Items.Add(new ComboBoxItem { Content = content, Tag = value });
        combo.SelectedIndex = combo.Items.Count - 1;
    }

    /// <summary>
    /// Get the selected item's Tag value, cast to specified type.
    /// </summary>
    /// <typeparam name="T">Expected type of the Tag</typeparam>
    /// <returns>Tag value or null if no selection or wrong type</returns>
    public static T? GetSelectedTag<T>(ComboBox? combo) where T : struct
    {
        if (combo?.SelectedItem is ComboBoxItem item && item.Tag is T value)
            return value;
        return null;
    }

    /// <summary>
    /// Get the selected item's Tag value as the specified type, with default fallback.
    /// </summary>
    public static T GetSelectedTagOrDefault<T>(ComboBox? combo, T defaultValue) where T : struct
    {
        return GetSelectedTag<T>(combo) ?? defaultValue;
    }
}
