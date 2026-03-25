using System.Collections.Generic;
using System.Linq;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;

namespace ItemEditor.Services;

/// <summary>
/// Loads base item types from baseitems.2da for the BaseItem dropdown.
/// </summary>
public class BaseItemTypeService
{
    private readonly IGameDataService? _gameDataService;
    private List<BaseItemTypeInfo>? _cachedTypes;

    public BaseItemTypeService(IGameDataService? gameDataService)
    {
        _gameDataService = gameDataService;
    }

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

            if (TlkHelper.IsGarbageLabel(label))
                continue;

            var nameStrRef = baseItems.GetValue(i, "Name");
            string displayName;
            if (nameStrRef != null && nameStrRef != "****")
            {
                var tlkName = _gameDataService.GetString(nameStrRef);
                displayName = TlkHelper.IsValidTlkString(tlkName) ? tlkName! : TlkHelper.FormatBaseItemLabel(label);
            }
            else
            {
                displayName = TlkHelper.FormatBaseItemLabel(label);
            }

            // Filter by resolved display name (TLK may resolve to placeholder like "User")
            if (TlkHelper.IsGarbageLabel(displayName))
                continue;

            // Read ModelType column for conditional field display
            var modelTypeStr = baseItems.GetValue(i, "ModelType");
            int modelType = 0;
            if (modelTypeStr != null && modelTypeStr != "****" && int.TryParse(modelTypeStr, out var mt))
                modelType = mt;

            // Read Description column → TLK string
            string descriptionText = string.Empty;
            var descStrRef = baseItems.GetValue(i, "Description");
            if (descStrRef != null && descStrRef != "****")
            {
                var tlkDesc = _gameDataService.GetString(descStrRef);
                if (TlkHelper.IsValidTlkString(tlkDesc))
                    descriptionText = tlkDesc!;
            }

            // Read Stacking column: 1=single, 2=stackable, 3=charges
            var stackingStr = baseItems.GetValue(i, "Stacking");
            int stacking = 1; // Default: single (not stackable)
            if (stackingStr != null && stackingStr != "****" && int.TryParse(stackingStr, out var st))
                stacking = st;

            _cachedTypes.Add(new BaseItemTypeInfo(i, displayName, label, modelType, descriptionText, stacking));
        }

        _cachedTypes = _cachedTypes.OrderBy(t => t.DisplayName).ToList();
        UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded {_cachedTypes.Count} base item types from baseitems.2da");
        return _cachedTypes;
    }

    private List<BaseItemTypeInfo> GetHardcodedTypes()
    {
        // ModelType: 0=Simple, 1=Layered, 2=Composite, 3=Armor
        _cachedTypes = new List<BaseItemTypeInfo>
        {
            new(0, "Shortsword", "BASE_ITEM_SHORTSWORD", 0),
            new(1, "Longsword", "BASE_ITEM_LONGSWORD", 0),
            new(2, "Battleaxe", "BASE_ITEM_BATTLEAXE", 0),
            new(3, "Bastardsword", "BASE_ITEM_BASTARDSWORD", 0),
            new(4, "Light Flail", "BASE_ITEM_LIGHTFLAIL", 0),
            new(5, "Warhammer", "BASE_ITEM_WARHAMMER", 0),
            new(6, "Heavy Crossbow", "BASE_ITEM_HEAVYCROSSBOW", 0),
            new(7, "Light Crossbow", "BASE_ITEM_LIGHTCROSSBOW", 0),
            new(8, "Longbow", "BASE_ITEM_LONGBOW", 0),
            new(9, "Mace", "BASE_ITEM_LIGHTMACE", 0),
            new(10, "Halberd", "BASE_ITEM_HALBERD", 0),
            new(11, "Shortbow", "BASE_ITEM_SHORTBOW", 0),
            new(12, "Twobladed Sword", "BASE_ITEM_TWOBLADEDSWORD", 2),
            new(13, "Greatsword", "BASE_ITEM_GREATSWORD", 0),
            new(14, "Small Shield", "BASE_ITEM_SMALLSHIELD", 0),
            new(15, "Torch", "BASE_ITEM_TORCH", 0),
            new(16, "Armor", "BASE_ITEM_ARMOR", 3),
            new(17, "Helmet", "BASE_ITEM_HELMET", 0),
            new(18, "Amulet", "BASE_ITEM_AMULET", 0),
            new(19, "Belt", "BASE_ITEM_BELT", 0),
            new(20, "Boots", "BASE_ITEM_BOOTS", 0),
            new(21, "Gloves", "BASE_ITEM_GLOVES", 0),
            new(22, "Large Shield", "BASE_ITEM_LARGESHIELD", 0),
            new(23, "Tower Shield", "BASE_ITEM_TOWERSHIELD", 0),
            new(24, "Ring", "BASE_ITEM_RING", 0),
            new(25, "Arrow", "BASE_ITEM_ARROW", 0),
            new(26, "Bolt", "BASE_ITEM_BOLT", 0),
            new(27, "Bullet", "BASE_ITEM_BULLET", 0),
            new(28, "Club", "BASE_ITEM_CLUB", 0),
            new(29, "Dagger", "BASE_ITEM_DAGGER", 0),
            new(31, "Dire Mace", "BASE_ITEM_DIREMACE", 2),
            new(32, "Double Axe", "BASE_ITEM_DOUBLEAXE", 2),
            new(33, "Heavy Flail", "BASE_ITEM_HEAVYFLAIL", 0),
            new(35, "Light Hammer", "BASE_ITEM_LIGHTHAMMER", 0),
            new(36, "Handaxe", "BASE_ITEM_HANDAXE", 0),
            new(37, "Healers Kit", "BASE_ITEM_HEALERSKIT", 0),
            new(38, "Kama", "BASE_ITEM_KAMA", 0),
            new(39, "Katana", "BASE_ITEM_KATANA", 0),
            new(40, "Kukri", "BASE_ITEM_KUKRI", 0),
            new(41, "Magic Rod", "BASE_ITEM_MAGICROD", 0),
            new(42, "Magic Staff", "BASE_ITEM_MAGICSTAFF", 0),
            new(43, "Magic Wand", "BASE_ITEM_MAGICWAND", 0),
            new(44, "Morningstar", "BASE_ITEM_MORNINGSTAR", 0),
            new(46, "Potions", "BASE_ITEM_POTIONS", 0),
            new(47, "Quarterstaff", "BASE_ITEM_QUARTERSTAFF", 0),
            new(48, "Rapier", "BASE_ITEM_RAPIER", 0),
            new(49, "Scimitar", "BASE_ITEM_SCIMITAR", 0),
            new(50, "Scythe", "BASE_ITEM_SCYTHE", 0),
            new(53, "Short Spear", "BASE_ITEM_SHORTSPEAR", 0),
            new(54, "Shuriken", "BASE_ITEM_SHURIKEN", 0),
            new(55, "Sickle", "BASE_ITEM_SICKLE", 0),
            new(56, "Sling", "BASE_ITEM_SLING", 0),
            new(58, "Throwing Axe", "BASE_ITEM_THROWINGAXE", 0),
            new(61, "Greataxe", "BASE_ITEM_GREATAXE", 0),
            new(63, "Cloak", "BASE_ITEM_CLOAK", 1),
            new(67, "Trident", "BASE_ITEM_TRIDENT", 0),
            new(73, "Book", "BASE_ITEM_BOOK", 0),
            new(77, "Bracer", "BASE_ITEM_BRACER", 0),
            new(95, "Whip", "BASE_ITEM_WHIP", 0),
            new(108, "Dart", "BASE_ITEM_DART", 0),
        };

        return _cachedTypes.OrderBy(t => t.DisplayName).ToList();
    }
}

public class BaseItemTypeInfo
{
    public int BaseItemIndex { get; }
    public string DisplayName { get; }
    public string Label { get; }

    /// <summary>
    /// ModelType from baseitems.2da:
    /// 0 = Simple (1 model part), 1 = Layered (colors), 2 = Composite (3 parts), 3 = Armor (parts + colors)
    /// </summary>
    public int ModelType { get; }

    /// <summary>
    /// Item type description from baseitems.2da Description column → TLK.
    /// Provides context about the item category (read-only in UI).
    /// </summary>
    public string DescriptionText { get; }

    /// <summary>
    /// Stacking behavior from baseitems.2da:
    /// 1 = single (not stackable), 2 = stackable, 3 = charges
    /// </summary>
    public int Stacking { get; }

    public bool HasColorFields => ModelType is 1 or 3;
    public bool HasArmorParts => ModelType == 3;
    public bool HasModelParts => ModelType is 0 or 1 or 2;
    public bool HasMultipleModelParts => ModelType == 2;
    public bool IsStackable => Stacking == 2;
    public bool HasCharges => Stacking == 3;

    public BaseItemTypeInfo(int baseItemIndex, string displayName, string label, int modelType = 0, string descriptionText = "", int stacking = 1)
    {
        BaseItemIndex = baseItemIndex;
        DisplayName = displayName;
        Label = label;
        ModelType = modelType;
        DescriptionText = descriptionText;
        Stacking = stacking;
    }

    public override string ToString() => DisplayName;
}
