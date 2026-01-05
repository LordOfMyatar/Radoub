namespace Radoub.UI.Models;

/// <summary>
/// Persisted filter state for ItemFilterPanel.
/// </summary>
public class FilterState
{
    /// <summary>
    /// Show base game (Standard) items.
    /// </summary>
    public bool ShowStandard { get; set; } = true;

    /// <summary>
    /// Show module/HAK (Custom) items.
    /// </summary>
    public bool ShowCustom { get; set; } = true;

    /// <summary>
    /// Text search string.
    /// </summary>
    public string? SearchText { get; set; }

    /// <summary>
    /// Selected base item type index, or null for "All Types".
    /// </summary>
    public int? SelectedBaseItemIndex { get; set; }

    /// <summary>
    /// Property search string (searches item properties).
    /// </summary>
    public string? PropertySearchText { get; set; }
}
