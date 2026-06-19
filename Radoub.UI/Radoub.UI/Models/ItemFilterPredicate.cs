using Radoub.Formats.Services;
using Radoub.UI.ViewModels;

namespace Radoub.UI.Models;

/// <summary>
/// Pure, UI-free predicate for the item palette filter (Relique / Quartermaster).
///
/// Extracted from <c>ItemFilterPanel.MatchesFilter</c> (#2360) so the multi-criteria
/// match logic — source, type, slot, name/tag/resref text, property text — can be
/// unit-tested without constructing an Avalonia control. The panel delegates to this;
/// behavior is identical.
/// </summary>
public static class ItemFilterPredicate
{
    /// <summary>
    /// Returns true if <paramref name="item"/> satisfies every active criterion.
    /// </summary>
    /// <param name="item">The item to test.</param>
    /// <param name="searchLower">Lowercased name/tag/resref search text (empty = no text filter).</param>
    /// <param name="propertySearchLower">Lowercased property search text (empty = no property filter).</param>
    /// <param name="typeFilter">Base-item-type filter, or null / AllTypes for no type filter.</param>
    /// <param name="slotFilter">Equipment-slot filter, or null / AllSlots for no slot filter.</param>
    /// <param name="showStandard">Include base-game (BIF) items.</param>
    /// <param name="showOverride">Include items from the user's Override folder.</param>
    /// <param name="showHak">Include items from module-referenced HAK packs.</param>
    /// <param name="showModule">Include loose UTI files from the module directory.</param>
    /// <param name="showCreatureItems">
    /// Include creature natural weapons + internal/marker base types (see
    /// <see cref="Utils.ItemPaletteExclusions"/>). Default true (no filtering) so existing callers and
    /// tests are unaffected; the palette passes the user's toggle (default off → hide them).
    /// </param>
    public static bool Matches(
        ItemViewModel item,
        string searchLower,
        string propertySearchLower,
        ItemTypeInfo? typeFilter,
        SlotFilterInfo? slotFilter,
        bool showStandard,
        bool showOverride,
        bool showHak,
        bool showModule,
        bool showCreatureItems = true)
    {
        // Source filter (#1995: each of the four game-resource sources toggles independently).
        var sourceVisible = item.Source switch
        {
            GameResourceSource.Bif => showStandard,
            GameResourceSource.Override => showOverride,
            GameResourceSource.Hak => showHak,
            GameResourceSource.Module => showModule,
            _ => true
        };
        if (!sourceVisible) return false;

        // Creature/internal items (natural weapons, marker) are hidden unless explicitly shown.
        if (!showCreatureItems && Utils.ItemPaletteExclusions.IsExcluded(item.BaseItem)) return false;

        // Type filter
        if (typeFilter != null && !typeFilter.IsAllTypes)
        {
            if (item.BaseItem != typeFilter.BaseItemIndex)
                return false;
        }

        // Slot filter
        if (slotFilter != null && !slotFilter.IsAllSlots)
        {
            if (slotFilter.IsNonEquipable)
            {
                // Show only items that cannot be equipped
                if (item.IsEquipable) return false;
            }
            else
            {
                // Show only items that can go in the selected slot
                if ((item.EquipableSlotFlags & slotFilter.SlotFlag) == 0) return false;
            }
        }

        // Text search (name, tag, resref)
        if (!string.IsNullOrEmpty(searchLower))
        {
            var nameMatch = item.Name.ToLowerInvariant().Contains(searchLower);
            var tagMatch = item.Tag.ToLowerInvariant().Contains(searchLower);
            var resRefMatch = item.ResRef.ToLowerInvariant().Contains(searchLower);

            if (!nameMatch && !tagMatch && !resRefMatch)
                return false;
        }

        // Property search (searches resolved property strings)
        if (!string.IsNullOrEmpty(propertySearchLower))
        {
            var propsLower = item.PropertiesDisplay.ToLowerInvariant();
            if (!propsLower.Contains(propertySearchLower))
                return false;
        }

        return true;
    }
}
