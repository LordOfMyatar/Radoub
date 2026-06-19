using System.Text.Json.Serialization;

namespace Radoub.UI.Models;

/// <summary>
/// Persisted filter state for ItemFilterPanel.
/// </summary>
public class FilterState
{
    /// <summary>
    /// Show base game (Standard / BIF) items.
    /// </summary>
    public bool ShowStandard { get; set; } = true;

    /// <summary>
    /// Show items from the user's Override folder. Default off (#1995): overrides shadow base
    /// content and are not usually what a builder wants in the palette.
    /// </summary>
    public bool ShowOverride { get; set; } = false;

    /// <summary>
    /// Show items from module-referenced HAK packs. Default on.
    /// </summary>
    public bool ShowHak { get; set; } = true;

    /// <summary>
    /// Show loose UTI files in the module directory. Default on.
    /// </summary>
    public bool ShowModule { get; set; } = true;

    /// <summary>
    /// Legacy binary "show custom" flag (Override/HAK/Module lumped together). Read only from old
    /// persisted files via the original "ShowCustom" JSON key; null in new files. Consumed by
    /// <see cref="MigrateLegacy"/> to seed the three per-source toggles, then cleared (#1995).
    /// </summary>
    [JsonPropertyName("ShowCustom")]
    public bool? LegacyShowCustom { get; set; }

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

    /// <summary>
    /// Selected slot filter flag, or null for "All Slots".
    /// </summary>
    public int? SelectedSlotFlag { get; set; }

    /// <summary>
    /// One-time migration from the old binary Standard/Custom model (#1995). If a legacy
    /// "ShowCustom" value was loaded, apply it to all three per-source toggles so a user who had
    /// custom content visible keeps seeing it, then clear the legacy field. No-op for new files.
    /// </summary>
    public void MigrateLegacy()
    {
        if (LegacyShowCustom is { } legacy)
        {
            ShowOverride = legacy;
            ShowHak = legacy;
            ShowModule = legacy;
            LegacyShowCustom = null;
        }
    }
}
