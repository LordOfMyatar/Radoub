using System;
using System.Collections.Generic;
using System.Linq;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.TwoDA;
using Radoub.Formats.Uti;

namespace ItemEditor.Services;

/// <summary>
/// Drives the item property editor UI by walking the itempropdef.2da cascade chain.
/// Provides available property types, subtypes, cost values, and param values
/// for building the add/edit property workflow.
/// </summary>
public class ItemPropertyService
{
    private readonly IGameDataService _gameDataService;
    private List<PropertyTypeInfo>? _cachedPropertyTypes;

    public ItemPropertyService(IGameDataService gameDataService)
    {
        _gameDataService = gameDataService ?? throw new ArgumentNullException(nameof(gameDataService));
    }

    /// <summary>
    /// Get all available property types from itempropdef.2da, sorted by display name.
    /// </summary>
    public List<PropertyTypeInfo> GetAvailablePropertyTypes()
    {
        if (_cachedPropertyTypes != null)
            return _cachedPropertyTypes;

        _cachedPropertyTypes = new List<PropertyTypeInfo>();

        if (!_gameDataService.IsConfigured)
            return _cachedPropertyTypes;

        var propDef = _gameDataService.Get2DA("itempropdef");
        if (propDef == null)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "Could not load itempropdef.2da");
            return _cachedPropertyTypes;
        }

        for (int i = 0; i < propDef.RowCount; i++)
        {
            var label = propDef.GetValue(i, "Label");
            if (string.IsNullOrEmpty(label) || label == "****")
                continue;

            // Skip reserved/placeholder entries (BioWare Reserved, CEP Reserved, etc.)
            if (Radoub.Formats.Common.TlkHelper.IsGarbageLabel(label))
                continue;

            var gameStrRef = propDef.GetValue(i, "GameStrRef");
            var nameStrRef = propDef.GetValue(i, "Name");
            var displayName = _gameDataService.GetString(gameStrRef)
                              ?? _gameDataService.GetString(nameStrRef)
                              ?? label;

            // Also filter by resolved display name (TLK may say "Reserved" even if label is clean)
            if (Radoub.Formats.Common.TlkHelper.IsGarbageLabel(displayName))
                continue;

            var subtypeResRef = propDef.GetValue(i, "SubTypeResRef");
            var costTableResRef = propDef.GetValue(i, "CostTableResRef");
            var param1ResRef = propDef.GetValue(i, "Param1ResRef");

            _cachedPropertyTypes.Add(new PropertyTypeInfo(
                propertyIndex: i,
                displayName: displayName,
                label: label,
                subtypeResRef: IsValid(subtypeResRef) ? subtypeResRef! : null,
                costTableIndex: ParseIntOrNull(costTableResRef),
                paramTableIndex: ParseIntOrNull(param1ResRef)));
        }

        // Disambiguate duplicate display names by appending context from Label
        DisambiguateDuplicateNames(_cachedPropertyTypes);

        _cachedPropertyTypes = _cachedPropertyTypes.OrderBy(t => t.DisplayName).ToList();
        UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded {_cachedPropertyTypes.Count} property types from itempropdef.2da");
        return _cachedPropertyTypes;
    }

    /// <summary>
    /// Get subtypes for a property type (e.g., ability types for Ability Bonus).
    /// </summary>
    public List<TwoDAEntry> GetSubtypes(int propertyIndex)
    {
        var subtypeResRef = GetSubtypeResRef(propertyIndex);
        if (subtypeResRef == null)
            return new List<TwoDAEntry>();

        return LoadTwoDAEntries(subtypeResRef);
    }

    /// <summary>
    /// Get cost values for a property type by resolving through iprp_costtable.2da.
    /// </summary>
    public List<TwoDAEntry> GetCostValues(int propertyIndex)
    {
        var costTableIndex = GetCostTableIndex(propertyIndex);
        if (costTableIndex == null)
            return new List<TwoDAEntry>();

        var costTable = _gameDataService.Get2DA("iprp_costtable");
        if (costTable == null)
            return new List<TwoDAEntry>();

        var costResRef = costTable.GetValue(costTableIndex.Value, "Name");
        if (!IsValid(costResRef))
            return new List<TwoDAEntry>();

        return LoadTwoDAEntries(costResRef!);
    }

    /// <summary>
    /// Get param values for a property type by resolving through iprp_paramtable.2da.
    /// </summary>
    public List<TwoDAEntry> GetParamValues(int propertyIndex)
    {
        var paramTableIndex = GetParamTableIndex(propertyIndex);
        if (paramTableIndex == null)
            return new List<TwoDAEntry>();

        var paramTable = _gameDataService.Get2DA("iprp_paramtable");
        if (paramTable == null)
            return new List<TwoDAEntry>();

        var paramResRef = paramTable.GetValue(paramTableIndex.Value, "TableResRef");
        if (!IsValid(paramResRef))
            return new List<TwoDAEntry>();

        return LoadTwoDAEntries(paramResRef!);
    }

    /// <summary>
    /// Check if a property type has subtypes.
    /// </summary>
    public bool HasSubtypes(int propertyIndex) => GetSubtypeResRef(propertyIndex) != null;

    /// <summary>
    /// Check if a property type has a cost table.
    /// </summary>
    public bool HasCostTable(int propertyIndex) => GetCostTableIndex(propertyIndex) != null;

    /// <summary>
    /// Check if a property type has a param table.
    /// </summary>
    public bool HasParamTable(int propertyIndex) => GetParamTableIndex(propertyIndex) != null;

    /// <summary>
    /// Search property types by name or subtype name. Case-insensitive.
    /// </summary>
    public List<PropertyTypeInfo> SearchProperties(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return GetAvailablePropertyTypes();

        var types = GetAvailablePropertyTypes();
        var results = new List<PropertyTypeInfo>();

        foreach (var type in types)
        {
            if (type.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(type);
                continue;
            }

            // Search subtypes
            if (type.SubtypeResRef != null)
            {
                var subtypes = GetSubtypes(type.PropertyIndex);
                if (subtypes.Any(s => s.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)))
                {
                    results.Add(type);
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Create an ItemProperty with correct indices from user selections.
    /// </summary>
    public ItemProperty CreateItemProperty(int propertyIndex, int subtypeIndex, int costValueIndex, int? paramValueIndex)
    {
        var prop = new ItemProperty
        {
            PropertyName = (ushort)propertyIndex,
            Subtype = (ushort)subtypeIndex,
            CostValue = (ushort)costValueIndex,
            ChanceAppear = 100
        };

        // Set CostTable from itempropdef
        var costTableIndex = GetCostTableIndex(propertyIndex);
        prop.CostTable = costTableIndex.HasValue ? (byte)costTableIndex.Value : (byte)0;

        // Set Param1 from itempropdef
        var paramTableIndex = GetParamTableIndex(propertyIndex);
        if (paramTableIndex.HasValue && paramValueIndex.HasValue)
        {
            prop.Param1 = (byte)paramTableIndex.Value;
            prop.Param1Value = (byte)paramValueIndex.Value;
        }
        else
        {
            prop.Param1 = 0xFF;
            prop.Param1Value = 0;
        }

        return prop;
    }

    #region Private helpers

    private string? GetSubtypeResRef(int propertyIndex)
    {
        var propDef = _gameDataService.Get2DA("itempropdef");
        if (propDef == null || propertyIndex >= propDef.RowCount)
            return null;

        var value = propDef.GetValue(propertyIndex, "SubTypeResRef");
        return IsValid(value) ? value : null;
    }

    private int? GetCostTableIndex(int propertyIndex)
    {
        var propDef = _gameDataService.Get2DA("itempropdef");
        if (propDef == null || propertyIndex >= propDef.RowCount)
            return null;

        var value = propDef.GetValue(propertyIndex, "CostTableResRef");
        return ParseIntOrNull(value);
    }

    private int? GetParamTableIndex(int propertyIndex)
    {
        var propDef = _gameDataService.Get2DA("itempropdef");
        if (propDef == null || propertyIndex >= propDef.RowCount)
            return null;

        var value = propDef.GetValue(propertyIndex, "Param1ResRef");
        return ParseIntOrNull(value);
    }

    private List<TwoDAEntry> LoadTwoDAEntries(string twoDAName)
    {
        var twoDA = _gameDataService.Get2DA(twoDAName);
        if (twoDA == null)
            return new List<TwoDAEntry>();

        var entries = new List<TwoDAEntry>();
        for (int i = 0; i < twoDA.RowCount; i++)
        {
            var nameStrRef = twoDA.GetValue(i, "Name");
            if (!IsValid(nameStrRef))
                continue;

            var displayName = _gameDataService.GetString(nameStrRef);
            if (string.IsNullOrEmpty(displayName))
            {
                // Fallback to Label column
                var label = twoDA.GetValue(i, "Label");
                if (IsValid(label))
                    displayName = label;
                else
                    continue;
            }

            entries.Add(new TwoDAEntry(i, displayName!));
        }

        return entries;
    }

    /// <summary>
    /// When multiple property types share the same display name (e.g., "On Hit" appears
    /// for OnHit, OnHitCastSpell, and OnMonsterHit), append a suffix derived from
    /// the nwscript.nss ITEM_PROPERTY_* constant names to make each entry distinguishable.
    /// </summary>
    private static void DisambiguateDuplicateNames(List<PropertyTypeInfo> types)
    {
        var duplicateGroups = types.GroupBy(t => t.DisplayName)
            .Where(g => g.Count() > 1);

        foreach (var group in duplicateGroups)
        {
            foreach (var type in group)
            {
                // Try the known constant map first, fall back to label-based suffix
                var suffix = ItemPropertyConstants.TryGetValue(type.PropertyIndex, out var name)
                    ? name
                    : FormatLabel(type.Label);

                if (!string.IsNullOrEmpty(suffix))
                    type.DisplayName = $"{type.DisplayName} ({suffix})";
            }
        }
    }

    /// <summary>
    /// Fallback: strip IP_CONST_ prefix and format underscored label as title case.
    /// Used when a property index isn't in the known constants map.
    /// </summary>
    private static string FormatLabel(string label)
    {
        var stripped = label;
        if (stripped.StartsWith("IP_CONST_", StringComparison.OrdinalIgnoreCase))
            stripped = stripped.Substring("IP_CONST_".Length);

        var words = stripped.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Select(w =>
            w.Length <= 1 ? w.ToUpperInvariant() :
            char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant()));
    }

    /// <summary>
    /// Known ITEM_PROPERTY_* constants from nwscript.nss that share TLK display names.
    /// Maps itempropdef.2da row index → human-readable disambiguation suffix.
    /// Source: nwscript.nss (stable game constants).
    /// </summary>
    private static readonly Dictionary<int, string> ItemPropertyConstants = new()
    {
        // AC Bonus group
        { 1, "" },                           // ITEM_PROPERTY_AC_BONUS (base)
        { 2, "vs. Alignment Group" },        // ITEM_PROPERTY_AC_BONUS_VS_ALIGNMENT_GROUP
        { 3, "vs. Damage Type" },            // ITEM_PROPERTY_AC_BONUS_VS_DAMAGE_TYPE
        { 4, "vs. Racial Group" },           // ITEM_PROPERTY_AC_BONUS_VS_RACIAL_GROUP
        { 5, "vs. Specific Alignment" },     // ITEM_PROPERTY_AC_BONUS_VS_SPECIFIC_ALIGNMENT

        // Enhancement Bonus group
        { 6, "" },                           // ITEM_PROPERTY_ENHANCEMENT_BONUS (base)
        { 7, "vs. Alignment Group" },        // ITEM_PROPERTY_ENHANCEMENT_BONUS_VS_ALIGNMENT_GROUP
        { 8, "vs. Racial Group" },           // ITEM_PROPERTY_ENHANCEMENT_BONUS_VS_RACIAL_GROUP
        { 9, "vs. Specific Alignment" },     // ITEM_PROPERTY_ENHANCEMENT_BONUS_VS_SPECIFIC_ALIGNEMENT

        // Damage Bonus group
        { 16, "" },                          // ITEM_PROPERTY_DAMAGE_BONUS (base)
        { 17, "vs. Alignment Group" },       // ITEM_PROPERTY_DAMAGE_BONUS_VS_ALIGNMENT_GROUP
        { 18, "vs. Racial Group" },          // ITEM_PROPERTY_DAMAGE_BONUS_VS_RACIAL_GROUP
        { 19, "vs. Specific Alignment" },    // ITEM_PROPERTY_DAMAGE_BONUS_VS_SPECIFIC_ALIGNMENT

        // Saving Throw Bonus group
        { 40, "" },                          // ITEM_PROPERTY_SAVING_THROW_BONUS (base)
        { 41, "Specific" },                  // ITEM_PROPERTY_SAVING_THROW_BONUS_SPECIFIC

        // Decreased Saving Throws group
        { 49, "" },                          // ITEM_PROPERTY_DECREASED_SAVING_THROWS (base)
        { 50, "Specific" },                  // ITEM_PROPERTY_DECREASED_SAVING_THROWS_SPECIFIC

        // "On Hit" group
        { 48, "Properties" },               // ITEM_PROPERTY_ON_HIT_PROPERTIES — effects like Stun, Sleep, Hold
        { 72, "Monster Hit" },              // ITEM_PROPERTY_ON_MONSTER_HIT — monster-specific on-hit effects
        { 82, "Cast Spell" },               // ITEM_PROPERTY_ONHITCASTSPELL — cast spell on hit

        // Attack Bonus group
        { 56, "" },                          // ITEM_PROPERTY_ATTACK_BONUS (base)
        { 57, "vs. Alignment Group" },       // ITEM_PROPERTY_ATTACK_BONUS_VS_ALIGNMENT_GROUP
        { 58, "vs. Racial Group" },          // ITEM_PROPERTY_ATTACK_BONUS_VS_RACIAL_GROUP
        { 59, "vs. Specific Alignment" },    // ITEM_PROPERTY_ATTACK_BONUS_VS_SPECIFIC_ALIGNMENT
    };

    private static bool IsValid(string? value) => !string.IsNullOrEmpty(value) && value != "****";

    private static int? ParseIntOrNull(string? value)
    {
        if (!IsValid(value))
            return null;
        return int.TryParse(value, out int result) ? result : null;
    }

    #endregion
}

/// <summary>
/// Represents a property type from itempropdef.2da.
/// </summary>
public class PropertyTypeInfo
{
    public int PropertyIndex { get; }
    public string DisplayName { get; internal set; }
    public string Label { get; }
    public string? SubtypeResRef { get; }
    public int? CostTableIndex { get; }
    public int? ParamTableIndex { get; }

    public bool HasSubtypes => SubtypeResRef != null;
    public bool HasCostTable => CostTableIndex != null;
    public bool HasParamTable => ParamTableIndex != null;

    public PropertyTypeInfo(int propertyIndex, string displayName, string label,
        string? subtypeResRef, int? costTableIndex, int? paramTableIndex)
    {
        PropertyIndex = propertyIndex;
        DisplayName = displayName;
        Label = label;
        SubtypeResRef = subtypeResRef;
        CostTableIndex = costTableIndex;
        ParamTableIndex = paramTableIndex;
    }

    public override string ToString() => DisplayName;
}

/// <summary>
/// A single entry from a 2DA table (subtype, cost value, or param value).
/// </summary>
public class TwoDAEntry
{
    public int Index { get; }
    public string DisplayName { get; }

    public TwoDAEntry(int index, string displayName)
    {
        Index = index;
        DisplayName = displayName;
    }

    public override string ToString() => DisplayName;
}
