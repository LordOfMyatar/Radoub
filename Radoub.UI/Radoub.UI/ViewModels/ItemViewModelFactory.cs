using Radoub.Formats.Common;
using Radoub.Formats.Services;
using Radoub.Formats.Uti;

namespace Radoub.UI.ViewModels;

/// <summary>
/// Factory for creating ItemViewModel instances with resolved display names.
/// Uses IGameDataService for 2DA and TLK lookups.
/// </summary>
public class ItemViewModelFactory
{
    private readonly IGameDataService _gameData;

    /// <summary>
    /// Creates a new factory using the specified game data service.
    /// </summary>
    public ItemViewModelFactory(IGameDataService gameData)
    {
        _gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
    }

    /// <summary>
    /// Create an ItemViewModel from a UtiFile.
    /// Resolves display name, base item type, and properties.
    /// </summary>
    public ItemViewModel Create(UtiFile item)
    {
        var displayName = ResolveDisplayName(item);
        var baseItemName = ResolveBaseItemName(item.BaseItem);
        var propertiesDisplay = ResolvePropertiesDisplay(item.Properties);

        return new ItemViewModel(item, displayName, baseItemName, propertiesDisplay);
    }

    /// <summary>
    /// Create ItemViewModels for a collection of items.
    /// </summary>
    public IEnumerable<ItemViewModel> Create(IEnumerable<UtiFile> items)
    {
        return items.Select(Create);
    }

    private string ResolveDisplayName(UtiFile item)
    {
        // Try localized name first
        var defaultString = item.LocalizedName.GetDefault();
        if (!string.IsNullOrEmpty(defaultString))
            return defaultString;

        // Fall back to TLK reference if LocalizedName has a StrRef
        if (item.LocalizedName.StrRef != 0xFFFFFFFF)
        {
            var tlkString = _gameData.GetString(item.LocalizedName.StrRef);
            if (!string.IsNullOrEmpty(tlkString))
                return tlkString;
        }

        // Fall back to ResRef
        if (!string.IsNullOrEmpty(item.TemplateResRef))
            return item.TemplateResRef;

        return "(unnamed)";
    }

    private string ResolveBaseItemName(int baseItem)
    {
        // Look up in baseitems.2da
        var nameStrRef = _gameData.Get2DAValue("baseitems", baseItem, "Name");
        if (nameStrRef != null)
        {
            var resolved = _gameData.GetString(nameStrRef);
            if (!string.IsNullOrEmpty(resolved))
                return resolved;
        }

        // Fallback to label
        var label = _gameData.Get2DAValue("baseitems", baseItem, "label");
        if (!string.IsNullOrEmpty(label))
            return label;

        return $"Type {baseItem}";
    }

    private string ResolvePropertiesDisplay(List<ItemProperty> properties)
    {
        if (properties.Count == 0)
            return string.Empty;

        // Resolve each property and join with semicolons
        var resolved = new List<string>();
        foreach (var prop in properties)
        {
            var propName = ResolvePropertyName(prop);
            if (!string.IsNullOrEmpty(propName))
                resolved.Add(propName);
        }

        return string.Join("; ", resolved);
    }

    private string ResolvePropertyName(ItemProperty prop)
    {
        // Get property definition from itempropdef.2da
        var gameStrRef = _gameData.Get2DAValue("itempropdef", prop.PropertyName, "GameStrRef");
        var baseName = _gameData.GetString(gameStrRef);
        if (string.IsNullOrEmpty(baseName))
        {
            var nameStrRef = _gameData.Get2DAValue("itempropdef", prop.PropertyName, "Name");
            baseName = _gameData.GetString(nameStrRef) ?? $"Property {prop.PropertyName}";
        }

        var parts = new List<string> { baseName.TrimEnd(':') };

        // Resolve subtype if present
        var subtypeResRef = _gameData.Get2DAValue("itempropdef", prop.PropertyName, "SubTypeResRef");
        if (!string.IsNullOrEmpty(subtypeResRef) && subtypeResRef != "****")
        {
            var subtypeStrRef = _gameData.Get2DAValue(subtypeResRef, prop.Subtype, "Name");
            var subtypeName = _gameData.GetString(subtypeStrRef);
            if (!string.IsNullOrEmpty(subtypeName))
                parts.Add(subtypeName);
        }

        // Resolve cost value if present
        var costTableIdx = _gameData.Get2DAValue("itempropdef", prop.PropertyName, "CostTableResRef");
        if (!string.IsNullOrEmpty(costTableIdx) && costTableIdx != "****" && int.TryParse(costTableIdx, out int costIdx))
        {
            var costResRef = _gameData.Get2DAValue("iprp_costtable", costIdx, "Name");
            if (!string.IsNullOrEmpty(costResRef) && costResRef != "****")
            {
                var costStrRef = _gameData.Get2DAValue(costResRef, prop.CostValue, "Name");
                var costName = _gameData.GetString(costStrRef);
                if (!string.IsNullOrEmpty(costName))
                    parts.Add(costName);
            }
        }

        return string.Join(" ", parts);
    }
}
