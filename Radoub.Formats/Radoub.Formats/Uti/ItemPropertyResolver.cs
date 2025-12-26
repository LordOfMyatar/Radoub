using Radoub.Formats.Common;
using Radoub.Formats.Resolver;
using Radoub.Formats.Tlk;
using Radoub.Formats.TwoDA;

namespace Radoub.Formats.Uti;

/// <summary>
/// Resolves item property names from the NWN 2DA chain.
/// Converts raw ItemProperty indices into human-readable strings.
///
/// Resolution chain:
/// - itempropdef.2da → property name + subtype/cost/param table references
/// - iprp_*.2da → subtype names
/// - iprp_costtable.2da → cost table references → specific cost 2da
/// - iprp_paramtable.2da → param table references → specific param 2da
///
/// Reference: BioWare Aurora Item Format spec (Chapter 5), neverwinter.nim
/// </summary>
public class ItemPropertyResolver : IDisposable
{
    private readonly GameResourceResolver _resolver;
    private readonly TlkFile? _tlk;
    private readonly TlkFile? _customTlk;
    private readonly Dictionary<string, TwoDAFile> _twoDACache = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    // Core 2DA files (loaded on demand)
    private TwoDAFile? _itemPropDef;
    private TwoDAFile? _costTable;
    private TwoDAFile? _paramTable;

    /// <summary>
    /// Create a resolver using a GameResourceResolver for 2DA access.
    /// </summary>
    public ItemPropertyResolver(GameResourceResolver resolver, TlkFile? tlk = null, TlkFile? customTlk = null)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _tlk = tlk;
        _customTlk = customTlk;
    }

    /// <summary>
    /// Resolve an item property to a human-readable string.
    /// Returns formatted string like "Enhancement Bonus +3" or "Bonus Feat: Alertness".
    /// </summary>
    public string Resolve(ItemProperty property)
    {
        var result = new ResolvedItemProperty();

        // Get property definition from itempropdef.2da
        var propDef = GetItemPropDef();
        if (propDef == null || property.PropertyName >= propDef.RowCount)
            return FormatUnknown(property);

        // Get property name from TLK
        var nameStrRef = propDef.GetValue(property.PropertyName, "Name");
        var gameStrRef = propDef.GetValue(property.PropertyName, "GameStrRef");
        result.PropertyName = GetTlkString(gameStrRef) ?? GetTlkString(nameStrRef) ?? $"Property {property.PropertyName}";

        // Get subtype name if applicable
        var subtypeResRef = propDef.GetValue(property.PropertyName, "SubTypeResRef");
        if (!string.IsNullOrEmpty(subtypeResRef) && subtypeResRef != "****")
        {
            result.SubtypeName = ResolveSubtype(subtypeResRef, property.Subtype);
        }

        // Get cost value name if applicable
        var costTableIdx = propDef.GetValue(property.PropertyName, "CostTableResRef");
        if (!string.IsNullOrEmpty(costTableIdx) && costTableIdx != "****" && int.TryParse(costTableIdx, out int costIdx))
        {
            result.CostValueName = ResolveCostValue(costIdx, property.CostValue);
        }

        // Get param value name if applicable
        var paramIdx = property.Param1;
        if (paramIdx != 0xFF)
        {
            result.ParamValueName = ResolveParamValue(paramIdx, property.Param1Value);
        }
        else
        {
            // Check if param is defined in itempropdef.2da
            var paramResRef = propDef.GetValue(property.PropertyName, "Param1ResRef");
            if (!string.IsNullOrEmpty(paramResRef) && paramResRef != "****" && int.TryParse(paramResRef, out int propParamIdx))
            {
                result.ParamValueName = ResolveParamValue(propParamIdx, property.Param1Value);
            }
        }

        return result.Format();
    }

    /// <summary>
    /// Resolve multiple item properties.
    /// </summary>
    public IEnumerable<string> Resolve(IEnumerable<ItemProperty> properties)
    {
        return properties.Select(Resolve);
    }

    /// <summary>
    /// Get detailed resolution info for an item property.
    /// Useful for debugging or detailed display.
    /// </summary>
    public ResolvedItemProperty ResolveDetailed(ItemProperty property)
    {
        var result = new ResolvedItemProperty
        {
            PropertyIndex = property.PropertyName,
            SubtypeIndex = property.Subtype,
            CostTableIndex = property.CostTable,
            CostValueIndex = property.CostValue,
            ParamTableIndex = property.Param1,
            ParamValueIndex = property.Param1Value
        };

        var propDef = GetItemPropDef();
        if (propDef == null || property.PropertyName >= propDef.RowCount)
            return result;

        // Property name
        var nameStrRef = propDef.GetValue(property.PropertyName, "Name");
        var gameStrRef = propDef.GetValue(property.PropertyName, "GameStrRef");
        result.PropertyName = GetTlkString(gameStrRef) ?? GetTlkString(nameStrRef) ?? $"Property {property.PropertyName}";

        // Subtype
        var subtypeResRef = propDef.GetValue(property.PropertyName, "SubTypeResRef");
        if (!string.IsNullOrEmpty(subtypeResRef) && subtypeResRef != "****")
        {
            result.SubtypeTableResRef = subtypeResRef;
            result.SubtypeName = ResolveSubtype(subtypeResRef, property.Subtype);
        }

        // Cost
        var costTableIdx = propDef.GetValue(property.PropertyName, "CostTableResRef");
        if (!string.IsNullOrEmpty(costTableIdx) && costTableIdx != "****" && int.TryParse(costTableIdx, out int costIdx))
        {
            result.CostValueName = ResolveCostValue(costIdx, property.CostValue);
        }

        // Param
        var paramIdx = property.Param1;
        if (paramIdx != 0xFF)
        {
            result.ParamValueName = ResolveParamValue(paramIdx, property.Param1Value);
        }
        else
        {
            var paramResRef = propDef.GetValue(property.PropertyName, "Param1ResRef");
            if (!string.IsNullOrEmpty(paramResRef) && paramResRef != "****" && int.TryParse(paramResRef, out int propParamIdx))
            {
                result.ParamValueName = ResolveParamValue(propParamIdx, property.Param1Value);
            }
        }

        return result;
    }

    private string? ResolveSubtype(string subtypeResRef, int subtypeIndex)
    {
        var subtypeTwoDA = GetTwoDA(subtypeResRef);
        if (subtypeTwoDA == null || subtypeIndex >= subtypeTwoDA.RowCount)
            return null;

        var nameStrRef = subtypeTwoDA.GetValue(subtypeIndex, "Name");
        return GetTlkString(nameStrRef);
    }

    private string? ResolveCostValue(int costTableIndex, int costValueIndex)
    {
        var costTable = GetCostTable();
        if (costTable == null || costTableIndex >= costTable.RowCount)
            return null;

        var costResRef = costTable.GetValue(costTableIndex, "Name");
        if (string.IsNullOrEmpty(costResRef) || costResRef == "****")
            return null;

        var costTwoDA = GetTwoDA(costResRef);
        if (costTwoDA == null || costValueIndex >= costTwoDA.RowCount)
            return null;

        // Cost tables may have Name or Label columns
        var nameStrRef = costTwoDA.GetValue(costValueIndex, "Name");
        var resolved = GetTlkString(nameStrRef);
        if (resolved != null)
            return resolved;

        // Fallback to Label if Name doesn't resolve
        var label = costTwoDA.GetValue(costValueIndex, "Label");
        return label != "****" ? label : null;
    }

    private string? ResolveParamValue(int paramTableIndex, int paramValueIndex)
    {
        var paramTable = GetParamTable();
        if (paramTable == null || paramTableIndex >= paramTable.RowCount)
            return null;

        var paramResRef = paramTable.GetValue(paramTableIndex, "TableResRef");
        if (string.IsNullOrEmpty(paramResRef) || paramResRef == "****")
            return null;

        var paramTwoDA = GetTwoDA(paramResRef);
        if (paramTwoDA == null || paramValueIndex >= paramTwoDA.RowCount)
            return null;

        var nameStrRef = paramTwoDA.GetValue(paramValueIndex, "Name");
        var resolved = GetTlkString(nameStrRef);
        if (resolved != null)
            return resolved;

        var label = paramTwoDA.GetValue(paramValueIndex, "Label");
        return label != "****" ? label : null;
    }

    private string? GetTlkString(string? strRefStr)
    {
        if (string.IsNullOrEmpty(strRefStr) || strRefStr == "****")
            return null;

        if (!uint.TryParse(strRefStr, out uint strRef))
            return null;

        // Check custom TLK first (high bit indicates custom TLK)
        if (strRef >= 0x01000000 && _customTlk != null)
        {
            return _customTlk.GetString(strRef & 0x00FFFFFF);
        }

        return _tlk?.GetString(strRef);
    }

    private TwoDAFile? GetItemPropDef()
    {
        if (_itemPropDef == null)
            _itemPropDef = GetTwoDA("itempropdef");
        return _itemPropDef;
    }

    private TwoDAFile? GetCostTable()
    {
        if (_costTable == null)
            _costTable = GetTwoDA("iprp_costtable");
        return _costTable;
    }

    private TwoDAFile? GetParamTable()
    {
        if (_paramTable == null)
            _paramTable = GetTwoDA("iprp_paramtable");
        return _paramTable;
    }

    private TwoDAFile? GetTwoDA(string resRef)
    {
        if (_twoDACache.TryGetValue(resRef, out var cached))
            return cached;

        var data = _resolver.FindResource(resRef, ResourceTypes.TwoDA);
        if (data == null)
            return null;

        try
        {
            var twoDA = TwoDAReader.Read(data);
            _twoDACache[resRef] = twoDA;
            return twoDA;
        }
        catch
        {
            return null;
        }
    }

    private static string FormatUnknown(ItemProperty property)
    {
        return $"Unknown Property ({property.PropertyName})";
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _twoDACache.Clear();
            _disposed = true;
        }
    }
}

/// <summary>
/// Detailed resolution result for an item property.
/// </summary>
public class ResolvedItemProperty
{
    public int PropertyIndex { get; set; }
    public string PropertyName { get; set; } = string.Empty;

    public int SubtypeIndex { get; set; }
    public string? SubtypeTableResRef { get; set; }
    public string? SubtypeName { get; set; }

    public int CostTableIndex { get; set; }
    public int CostValueIndex { get; set; }
    public string? CostValueName { get; set; }

    public int ParamTableIndex { get; set; }
    public int ParamValueIndex { get; set; }
    public string? ParamValueName { get; set; }

    /// <summary>
    /// Format as a human-readable string.
    /// Examples: "Enhancement Bonus +3", "Bonus Feat: Alertness", "Damage Bonus: Fire 1d6"
    /// </summary>
    public string Format()
    {
        var parts = new List<string>();

        // Start with property name (may end with ":" for properties expecting values)
        var propName = PropertyName.TrimEnd(':');
        parts.Add(propName);

        // Add subtype if present
        if (!string.IsNullOrEmpty(SubtypeName))
            parts.Add(SubtypeName);

        // Add cost value if present (often the "amount" like +1, +2, etc.)
        if (!string.IsNullOrEmpty(CostValueName))
            parts.Add(CostValueName);

        // Add param value if present (often a target like damage type, skill, etc.)
        if (!string.IsNullOrEmpty(ParamValueName))
            parts.Add(ParamValueName);

        return string.Join(" ", parts);
    }

    public override string ToString() => Format();
}
