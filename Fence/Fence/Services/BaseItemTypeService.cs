using System;
using System.Collections.Generic;
using System.Linq;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;

namespace MerchantEditor.Services;

/// <summary>
/// Service for loading base item types from baseitems.2da.
/// Used for WillOnlyBuy/WillNotBuy restrictions.
/// </summary>
public class BaseItemTypeService
{
    private readonly IGameDataService? _gameDataService;
    private List<BaseItemTypeInfo>? _cachedTypes;

    public BaseItemTypeService(IGameDataService? gameDataService)
    {
        _gameDataService = gameDataService;
    }

    /// <summary>
    /// Get all valid base item types from baseitems.2da.
    /// </summary>
    public List<BaseItemTypeInfo> GetBaseItemTypes()
    {
        if (_cachedTypes != null)
            return _cachedTypes;

        _cachedTypes = new List<BaseItemTypeInfo>();

        if (_gameDataService == null || !_gameDataService.IsConfigured)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "GameDataService not configured, using hardcoded base item types");
            return GetHardcodedTypes();
        }

        var baseItems = _gameDataService.Get2DA("baseitems");
        if (baseItems == null)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "Could not load baseitems.2da, using hardcoded base item types");
            return GetHardcodedTypes();
        }

        for (int i = 0; i < baseItems.Rows.Count; i++)
        {
            var label = baseItems.GetValue(i, "label");
            if (string.IsNullOrEmpty(label) || label == "****")
                continue;

            // Get display name from TLK
            var nameStrRef = baseItems.GetValue(i, "Name");
            string displayName;
            if (nameStrRef != null && nameStrRef != "****")
            {
                displayName = _gameDataService.GetString(nameStrRef) ?? FormatLabel(label);
            }
            else
            {
                displayName = FormatLabel(label);
            }

            _cachedTypes.Add(new BaseItemTypeInfo(i, displayName, label));
        }

        // Sort by display name
        _cachedTypes = _cachedTypes.OrderBy(t => t.DisplayName).ToList();

        UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded {_cachedTypes.Count} base item types from baseitems.2da");
        return _cachedTypes;
    }

    /// <summary>
    /// Clear cached types (call when game paths change).
    /// </summary>
    public void ClearCache()
    {
        _cachedTypes = null;
    }

    private static string FormatLabel(string label)
    {
        // Convert "BASE_ITEM_SHORTSWORD" to "Shortsword"
        if (label.StartsWith("BASE_ITEM_", StringComparison.OrdinalIgnoreCase))
            label = label.Substring(10);

        // Convert underscores to spaces and title case
        return string.Join(" ", label.Split('_')
            .Select(w => char.ToUpper(w[0]) + w.Substring(1).ToLower()));
    }

    /// <summary>
    /// Hardcoded fallback when 2DA is unavailable.
    /// Based on common NWN base item types.
    /// </summary>
    private List<BaseItemTypeInfo> GetHardcodedTypes()
    {
        _cachedTypes = new List<BaseItemTypeInfo>
        {
            // Weapons
            new(0, "Shortsword", "BASE_ITEM_SHORTSWORD"),
            new(1, "Longsword", "BASE_ITEM_LONGSWORD"),
            new(2, "Battleaxe", "BASE_ITEM_BATTLEAXE"),
            new(3, "Bastardsword", "BASE_ITEM_BASTARDSWORD"),
            new(4, "Light Flail", "BASE_ITEM_LIGHTFLAIL"),
            new(5, "Warhammer", "BASE_ITEM_WARHAMMER"),
            new(6, "Heavy Crossbow", "BASE_ITEM_HEAVYCROSSBOW"),
            new(7, "Light Crossbow", "BASE_ITEM_LIGHTCROSSBOW"),
            new(8, "Longbow", "BASE_ITEM_LONGBOW"),
            new(9, "Mace", "BASE_ITEM_LIGHTMACE"),
            new(10, "Halberd", "BASE_ITEM_HALBERD"),
            new(11, "Shortbow", "BASE_ITEM_SHORTBOW"),
            new(12, "Twobladed Sword", "BASE_ITEM_TWOBLADEDSWORD"),
            new(13, "Greatsword", "BASE_ITEM_GREATSWORD"),
            new(14, "Small Shield", "BASE_ITEM_SMALLSHIELD"),
            new(15, "Torch", "BASE_ITEM_TORCH"),
            new(16, "Armor", "BASE_ITEM_ARMOR"),
            new(17, "Helmet", "BASE_ITEM_HELMET"),
            new(18, "Amulet", "BASE_ITEM_AMULET"),
            new(19, "Belt", "BASE_ITEM_BELT"),
            new(20, "Boots", "BASE_ITEM_BOOTS"),
            new(21, "Gloves", "BASE_ITEM_GLOVES"),
            new(22, "Large Shield", "BASE_ITEM_LARGESHIELD"),
            new(23, "Tower Shield", "BASE_ITEM_TOWERSHIELD"),
            new(24, "Ring", "BASE_ITEM_RING"),
            new(25, "Arrow", "BASE_ITEM_ARROW"),
            new(26, "Bolt", "BASE_ITEM_BOLT"),
            new(27, "Bullet", "BASE_ITEM_BULLET"),
            new(28, "Club", "BASE_ITEM_CLUB"),
            new(29, "Dagger", "BASE_ITEM_DAGGER"),
            new(31, "Dire Mace", "BASE_ITEM_DIREMACE"),
            new(32, "Double Axe", "BASE_ITEM_DOUBLEAXE"),
            new(33, "Heavy Flail", "BASE_ITEM_HEAVYFLAIL"),
            new(35, "Light Hammer", "BASE_ITEM_LIGHTHAMMER"),
            new(36, "Handaxe", "BASE_ITEM_HANDAXE"),
            new(37, "Healers Kit", "BASE_ITEM_HEALERSKIT"),
            new(38, "Kama", "BASE_ITEM_KAMA"),
            new(39, "Katana", "BASE_ITEM_KATANA"),
            new(40, "Kukri", "BASE_ITEM_KUKRI"),
            new(41, "Magic Rod", "BASE_ITEM_MAGICROD"),
            new(42, "Magic Staff", "BASE_ITEM_MAGICSTAFF"),
            new(43, "Magic Wand", "BASE_ITEM_MAGICWAND"),
            new(44, "Morningstar", "BASE_ITEM_MORNINGSTAR"),
            new(46, "Potions", "BASE_ITEM_POTIONS"),
            new(47, "Quarterstaff", "BASE_ITEM_QUARTERSTAFF"),
            new(48, "Rapier", "BASE_ITEM_RAPIER"),
            new(49, "Scimitar", "BASE_ITEM_SCIMITAR"),
            new(50, "Scythe", "BASE_ITEM_SCYTHE"),
            new(51, "Large Box", "BASE_ITEM_LARGEBOX"),
            new(53, "Short Spear", "BASE_ITEM_SHORTSPEAR"),
            new(54, "Shuriken", "BASE_ITEM_SHURIKEN"),
            new(55, "Sickle", "BASE_ITEM_SICKLE"),
            new(56, "Sling", "BASE_ITEM_SLING"),
            new(57, "Thieves Tools", "BASE_ITEM_THIEVESTOOLS"),
            new(58, "Throwing Axe", "BASE_ITEM_THROWINGAXE"),
            new(59, "Trap Kit", "BASE_ITEM_TRAPKIT"),
            new(60, "Key", "BASE_ITEM_KEY"),
            new(61, "Greataxe", "BASE_ITEM_GREATAXE"),
            new(63, "Cloak", "BASE_ITEM_CLOAK"),
            new(66, "Grenade", "BASE_ITEM_GRENADE"),
            new(67, "Trident", "BASE_ITEM_TRIDENT"),
            new(73, "Book", "BASE_ITEM_BOOK"),
            new(74, "Spellbook", "BASE_ITEM_SPELLSCROLL"),
            new(75, "Gold", "BASE_ITEM_GOLD"),
            new(76, "Gem", "BASE_ITEM_GEM"),
            new(77, "Bracer", "BASE_ITEM_BRACER"),
            new(78, "Miscellaneous (S)", "BASE_ITEM_MISCLARGE"),
            new(79, "Miscellaneous (M)", "BASE_ITEM_MISCMEDIUM"),
            new(80, "Miscellaneous (L)", "BASE_ITEM_MISCSMALL"),
            new(81, "Miscellaneous (T)", "BASE_ITEM_MISCTHIN"),
            new(95, "Whip", "BASE_ITEM_WHIP"),
            new(108, "Dart", "BASE_ITEM_DART"),
            new(109, "Throwing Star", "BASE_ITEM_DIREMACE")
        };

        return _cachedTypes.OrderBy(t => t.DisplayName).ToList();
    }
}

/// <summary>
/// Information about a base item type from baseitems.2da.
/// </summary>
public class BaseItemTypeInfo
{
    /// <summary>
    /// Row index in baseitems.2da (the actual base item type ID).
    /// </summary>
    public int BaseItemIndex { get; }

    /// <summary>
    /// Display name (resolved from TLK or formatted label).
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Label from baseitems.2da.
    /// </summary>
    public string Label { get; }

    public BaseItemTypeInfo(int baseItemIndex, string displayName, string label)
    {
        BaseItemIndex = baseItemIndex;
        DisplayName = displayName;
        Label = label;
    }

    public override string ToString() => DisplayName;
}
