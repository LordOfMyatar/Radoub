using System.Collections.Generic;
using System.Linq;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;

namespace Radoub.Formats.Services;

/// <summary>
/// Shared service for loading base item types from baseitems.2da.
/// Used by Fence (store panel organization) and Relique (conditional field visibility).
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

            if (TlkHelper.IsGarbageLabel(label))
                continue;

            // Get display name from TLK
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

            if (TlkHelper.IsGarbageLabel(displayName))
                continue;

            // Read StorePanel column (2DA values: 0=armor, 1=weapons, 2=potions, 3=scrolls, 4=misc)
            var storePanelStr = baseItems.GetValue(i, "StorePanel");
            int storePanel = 4; // Default to miscellaneous
            if (storePanelStr != null && storePanelStr != "****" && int.TryParse(storePanelStr, out var sp))
                storePanel = sp;

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

            // Read Stacking column: max stack size (1=single, >1=stackable)
            var stackingStr = baseItems.GetValue(i, "Stacking");
            int stacking = 1;
            if (stackingStr != null && stackingStr != "****" && int.TryParse(stackingStr, out var st))
                stacking = st;

            // Read ChargesStarting column: >0 means item uses charges (wands, rods, staves)
            var chargesStartingStr = baseItems.GetValue(i, "ChargesStarting");
            int chargesStarting = 0;
            if (chargesStartingStr != null && chargesStartingStr != "****" && int.TryParse(chargesStartingStr, out var cs))
                chargesStarting = cs;

            // Read InvSlotWidth/InvSlotHeight for inventory size rendering
            var invSlotWidthStr = baseItems.GetValue(i, "InvSlotWidth");
            int invSlotWidth = 1;
            if (invSlotWidthStr != null && invSlotWidthStr != "****" && int.TryParse(invSlotWidthStr, out var isw))
                invSlotWidth = isw;

            var invSlotHeightStr = baseItems.GetValue(i, "InvSlotHeight");
            int invSlotHeight = 1;
            if (invSlotHeightStr != null && invSlotHeightStr != "****" && int.TryParse(invSlotHeightStr, out var ish))
                invSlotHeight = ish;

            // WeaponWield: BioWare uses **** as a sentinel meaning "default melee weapon
            // held in one or two hands depending on creature size" — capture as -1 so the
            // IsHeldWeapon flag can include this category.
            var weaponWieldStr = baseItems.GetValue(i, "WeaponWield");
            int weaponWield;
            if (weaponWieldStr == "****")
                weaponWield = -1;
            else if (!string.IsNullOrEmpty(weaponWieldStr) && int.TryParse(weaponWieldStr, out var ww))
                weaponWield = ww;
            else
                weaponWield = 1; // missing column or unparseable → treat as nonweapon

            _cachedTypes.Add(new BaseItemTypeInfo(i, displayName, label, storePanel, modelType, descriptionText, stacking, chargesStarting, invSlotWidth, invSlotHeight, weaponWield));
        }

        _cachedTypes = _cachedTypes.OrderBy(t => t.DisplayName).ToList();

        UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded {_cachedTypes.Count} base item types from baseitems.2da");
        return _cachedTypes;
    }

    /// <summary>
    /// Get the UTM store panel ID for a base item type index.
    /// Returns StorePanels.Miscellaneous if the type is unknown.
    /// </summary>
    public int GetStorePanelForBaseItem(int baseItemIndex)
    {
        var types = GetBaseItemTypes();
        var typeInfo = types.FirstOrDefault(t => t.BaseItemIndex == baseItemIndex);
        if (typeInfo == null)
            return Radoub.Formats.Utm.StorePanels.Miscellaneous;

        return GetUtmStorePanel(typeInfo.StorePanel);
    }

    /// <summary>
    /// Convert baseitems.2da StorePanel value to UTM StorePanels constant.
    /// 2DA: 0=armor, 1=weapons, 2=potions, 3=scrolls, 4=misc
    /// UTM: 0=Armor, 1=Misc, 2=Potions, 3=RingsAmulets, 4=Weapons
    /// </summary>
    public static int GetUtmStorePanel(int twoDaStorePanel)
    {
        return twoDaStorePanel switch
        {
            0 => Radoub.Formats.Utm.StorePanels.Armor,
            1 => Radoub.Formats.Utm.StorePanels.Weapons,
            2 => Radoub.Formats.Utm.StorePanels.Potions,
            3 => Radoub.Formats.Utm.StorePanels.Potions, // Scrolls share Potions panel
            _ => Radoub.Formats.Utm.StorePanels.Miscellaneous,
        };
    }

    /// <summary>
    /// Clear cached types (call when game paths change).
    /// </summary>
    public void ClearCache()
    {
        _cachedTypes = null;
    }

    /// <summary>
    /// Hardcoded fallback when 2DA is unavailable.
    /// Union of Fence (57 entries with StorePanel) and Relique (ModelType, Stacking, ChargesStarting).
    /// </summary>
    private List<BaseItemTypeInfo> GetHardcodedTypes()
    {
        // StorePanel 2DA values: 0=armor, 1=weapons, 2=potions, 3=scrolls, 4=misc
        // ModelType: 0=Simple, 1=Layered, 2=Composite, 3=Armor
        //                                                         storeP  model  desc  stack  charges
        _cachedTypes = new List<BaseItemTypeInfo>
        {
            // Weapons (StorePanel=1)
            new(0, "Shortsword", "BASE_ITEM_SHORTSWORD", 1),
            new(1, "Longsword", "BASE_ITEM_LONGSWORD", 1),
            new(2, "Battleaxe", "BASE_ITEM_BATTLEAXE", 1),
            new(3, "Bastardsword", "BASE_ITEM_BASTARDSWORD", 1),
            new(4, "Light Flail", "BASE_ITEM_LIGHTFLAIL", 1),
            new(5, "Warhammer", "BASE_ITEM_WARHAMMER", 1),
            new(6, "Heavy Crossbow", "BASE_ITEM_HEAVYCROSSBOW", 1),
            new(7, "Light Crossbow", "BASE_ITEM_LIGHTCROSSBOW", 1),
            new(8, "Longbow", "BASE_ITEM_LONGBOW", 1),
            new(9, "Mace", "BASE_ITEM_LIGHTMACE", 1),
            new(10, "Halberd", "BASE_ITEM_HALBERD", 1),
            new(11, "Shortbow", "BASE_ITEM_SHORTBOW", 1),
            new(12, "Twobladed Sword", "BASE_ITEM_TWOBLADEDSWORD", 1, modelType: 2),
            new(13, "Greatsword", "BASE_ITEM_GREATSWORD", 1),
            // Armor/Shields (StorePanel=0)
            new(14, "Small Shield", "BASE_ITEM_SMALLSHIELD", 0),
            new(15, "Torch", "BASE_ITEM_TORCH", 4),
            new(16, "Armor", "BASE_ITEM_ARMOR", 0, modelType: 3),
            new(17, "Helmet", "BASE_ITEM_HELMET", 0),
            new(18, "Amulet", "BASE_ITEM_AMULET", 4),
            new(19, "Belt", "BASE_ITEM_BELT", 0),
            new(20, "Boots", "BASE_ITEM_BOOTS", 0),
            new(21, "Gloves", "BASE_ITEM_GLOVES", 0),
            new(22, "Large Shield", "BASE_ITEM_LARGESHIELD", 0),
            new(23, "Tower Shield", "BASE_ITEM_TOWERSHIELD", 0),
            new(24, "Ring", "BASE_ITEM_RING", 4),
            // Ammunition (stackable)
            new(25, "Arrow", "BASE_ITEM_ARROW", 1, stacking: 99),
            new(26, "Bolt", "BASE_ITEM_BOLT", 1, stacking: 99),
            new(27, "Bullet", "BASE_ITEM_BULLET", 1, stacking: 99),
            // More weapons
            new(28, "Club", "BASE_ITEM_CLUB", 1),
            new(29, "Dagger", "BASE_ITEM_DAGGER", 1),
            new(31, "Dire Mace", "BASE_ITEM_DIREMACE", 1, modelType: 2),
            new(32, "Double Axe", "BASE_ITEM_DOUBLEAXE", 1, modelType: 2),
            new(33, "Heavy Flail", "BASE_ITEM_HEAVYFLAIL", 1),
            new(35, "Light Hammer", "BASE_ITEM_LIGHTHAMMER", 1),
            new(36, "Handaxe", "BASE_ITEM_HANDAXE", 1),
            new(37, "Healers Kit", "BASE_ITEM_HEALERSKIT", 4, stacking: 10),
            new(38, "Kama", "BASE_ITEM_KAMA", 1),
            new(39, "Katana", "BASE_ITEM_KATANA", 1),
            new(40, "Kukri", "BASE_ITEM_KUKRI", 1),
            // Magic items (with charges)
            new(41, "Magic Rod", "BASE_ITEM_MAGICROD", 4, chargesStarting: 50),
            new(42, "Magic Staff", "BASE_ITEM_MAGICSTAFF", 1, chargesStarting: 50),
            new(43, "Magic Wand", "BASE_ITEM_MAGICWAND", 4, chargesStarting: 50),
            new(44, "Morningstar", "BASE_ITEM_MORNINGSTAR", 1),
            new(46, "Potions", "BASE_ITEM_POTIONS", 2, stacking: 10),
            new(47, "Quarterstaff", "BASE_ITEM_QUARTERSTAFF", 1),
            new(48, "Rapier", "BASE_ITEM_RAPIER", 1),
            new(49, "Scimitar", "BASE_ITEM_SCIMITAR", 1),
            new(50, "Scythe", "BASE_ITEM_SCYTHE", 1),
            new(51, "Large Box", "BASE_ITEM_LARGEBOX", 4),
            new(53, "Short Spear", "BASE_ITEM_SHORTSPEAR", 1),
            new(54, "Shuriken", "BASE_ITEM_SHURIKEN", 1, stacking: 50),
            new(55, "Sickle", "BASE_ITEM_SICKLE", 1),
            new(56, "Sling", "BASE_ITEM_SLING", 1),
            new(57, "Thieves Tools", "BASE_ITEM_THIEVESTOOLS", 4),
            new(58, "Throwing Axe", "BASE_ITEM_THROWINGAXE", 1, stacking: 50),
            new(59, "Trap Kit", "BASE_ITEM_TRAPKIT", 4),
            new(60, "Key", "BASE_ITEM_KEY", 4),
            new(61, "Greataxe", "BASE_ITEM_GREATAXE", 1),
            new(63, "Cloak", "BASE_ITEM_CLOAK", 0),
            new(66, "Grenade", "BASE_ITEM_GRENADE", 4),
            new(67, "Trident", "BASE_ITEM_TRIDENT", 1),
            new(73, "Book", "BASE_ITEM_BOOK", 4),
            new(74, "Spellbook", "BASE_ITEM_SPELLSCROLL", 3),
            new(75, "Gold", "BASE_ITEM_GOLD", 4),
            new(76, "Gem", "BASE_ITEM_GEM", 4),
            new(77, "Bracer", "BASE_ITEM_BRACER", 0),
            new(78, "Miscellaneous (S)", "BASE_ITEM_MISCLARGE", 4),
            new(79, "Miscellaneous (M)", "BASE_ITEM_MISCMEDIUM", 4),
            new(80, "Miscellaneous (L)", "BASE_ITEM_MISCSMALL", 4),
            new(81, "Miscellaneous (T)", "BASE_ITEM_MISCTHIN", 4),
            new(95, "Whip", "BASE_ITEM_WHIP", 1),
            new(108, "Dart", "BASE_ITEM_DART", 1, stacking: 50),
            new(109, "Throwing Star", "BASE_ITEM_THROWINGSTAR", 1),
        };

        return _cachedTypes.OrderBy(t => t.DisplayName).ToList();
    }
}

/// <summary>
/// Information about a base item type from baseitems.2da.
/// Rich model with properties used by both Fence (StorePanel) and Relique (ModelType, Stacking, Charges).
/// </summary>
public class BaseItemTypeInfo
{
    /// <summary>Row index in baseitems.2da (the actual base item type ID).</summary>
    public int BaseItemIndex { get; }

    /// <summary>Display name (resolved from TLK or formatted label).</summary>
    public string DisplayName { get; }

    /// <summary>Label from baseitems.2da.</summary>
    public string Label { get; }

    /// <summary>
    /// Store panel from baseitems.2da StorePanel column.
    /// 2DA values: 0=armor, 1=weapons, 2=potions, 3=scrolls, 4=miscellaneous.
    /// </summary>
    public int StorePanel { get; }

    /// <summary>
    /// ModelType from baseitems.2da:
    /// 0 = Simple (1 model part), 1 = Layered (colors), 2 = Composite (3 parts), 3 = Armor (parts + colors)
    /// </summary>
    public int ModelType { get; }

    /// <summary>Item type description from baseitems.2da Description column → TLK.</summary>
    public string DescriptionText { get; }

    /// <summary>Maximum stack size from baseitems.2da Stacking column. 1 = single, >1 = stackable.</summary>
    public int Stacking { get; }

    /// <summary>Initial charges from baseitems.2da ChargesStarting column. 0 = none, >0 = charge-based.</summary>
    public int ChargesStarting { get; }

    /// <summary>Inventory slot width from baseitems.2da InvSlotWidth column. Default 1.</summary>
    public int InvSlotWidth { get; }

    /// <summary>Inventory slot height from baseitems.2da InvSlotHeight column. Default 1.</summary>
    public int InvSlotHeight { get; }

    /// <summary>
    /// WeaponWield style from baseitems.2da WeaponWield column.
    /// Values: -1 (****) = melee weapon held in one or two hands; 1 = nonweapon;
    /// 4 = pole; 5 = bow; 6 = crossbow; 7 = shield; 8 = two-bladed; 9 = creature weapon;
    /// 10 = sling; 11 = thrown.
    /// </summary>
    public int WeaponWield { get; }

    // Computed convenience properties (used by Relique)
    public bool HasColorFields => ModelType is 1 or 3;
    public bool HasArmorParts => ModelType == 3;
    public bool HasModelParts => ModelType is 0 or 1 or 2;
    public bool HasMultipleModelParts => ModelType == 2;
    public bool IsStackable => Stacking > 1;
    public bool HasCharges => ChargesStarting > 0;

    /// <summary>
    /// True for items wielded as held weapons (sword, bow, crossbow, two-bladed, sling,
    /// thrown, pole). Used by Relique's 3D preview to apply a vertical orientation
    /// rotation; in-game these items get their orientation from the bone they attach to.
    /// Excludes shields (held but not "linear") and creature weapons.
    /// </summary>
    public bool IsHeldWeapon => WeaponWield is -1 or 4 or 5 or 6 or 8 or 10 or 11;

    public BaseItemTypeInfo(
        int baseItemIndex, string displayName, string label,
        int storePanel = 4, int modelType = 0, string descriptionText = "",
        int stacking = 1, int chargesStarting = 0,
        int invSlotWidth = 1, int invSlotHeight = 1,
        int weaponWield = 1)
    {
        BaseItemIndex = baseItemIndex;
        DisplayName = displayName;
        Label = label;
        StorePanel = storePanel;
        ModelType = modelType;
        DescriptionText = descriptionText;
        Stacking = stacking;
        ChargesStarting = chargesStarting;
        InvSlotWidth = invSlotWidth;
        InvSlotHeight = invSlotHeight;
        WeaponWield = weaponWield;
    }

    public override string ToString() => DisplayName;
}
