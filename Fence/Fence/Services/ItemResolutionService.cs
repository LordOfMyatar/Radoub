using System;
using System.Collections.Generic;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Uti;

namespace MerchantEditor.Services;

/// <summary>
/// Service for resolving item data from UTI files.
/// Provides caching to avoid repeated file reads.
/// </summary>
public class ItemResolutionService
{
    private readonly IGameDataService? _gameDataService;
    private readonly Dictionary<string, ResolvedItemData> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ItemResolutionService(IGameDataService? gameDataService)
    {
        _gameDataService = gameDataService;
    }

    /// <summary>
    /// Resolve item data from a ResRef.
    /// </summary>
    /// <param name="resRef">Item blueprint ResRef</param>
    /// <returns>Resolved item data, or null if not found</returns>
    public ResolvedItemData? ResolveItem(string resRef)
    {
        if (string.IsNullOrEmpty(resRef))
            return null;

        // Check cache first
        if (_cache.TryGetValue(resRef, out var cached))
            return cached;

        var data = LoadItemData(resRef);
        if (data != null)
        {
            _cache[resRef] = data;
        }

        return data;
    }

    /// <summary>
    /// Resolve multiple items efficiently.
    /// </summary>
    public Dictionary<string, ResolvedItemData> ResolveItems(IEnumerable<string> resRefs)
    {
        var results = new Dictionary<string, ResolvedItemData>(StringComparer.OrdinalIgnoreCase);

        foreach (var resRef in resRefs)
        {
            var data = ResolveItem(resRef);
            if (data != null)
            {
                results[resRef] = data;
            }
        }

        return results;
    }

    /// <summary>
    /// Clear the item cache.
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
    }

    private ResolvedItemData? LoadItemData(string resRef)
    {
        if (_gameDataService == null || !_gameDataService.IsConfigured)
        {
            return CreateFallbackData(resRef);
        }

        try
        {
            // Try to find the UTI resource
            var utiData = _gameDataService.FindResource(resRef, ResourceTypes.Uti);
            if (utiData == null)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"UTI not found: {resRef}");
                return CreateFallbackData(resRef);
            }

            // Parse the UTI
            var uti = UtiReader.Read(utiData);

            // Get display name
            var displayName = uti.LocalizedName.GetDefault();
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = resRef;
            }

            // Get base item type name
            var baseItemTypeName = GetBaseItemTypeName(uti.BaseItem);

            // Get base cost from UTI
            var baseCost = (int)uti.Cost;

            return new ResolvedItemData
            {
                ResRef = resRef,
                DisplayName = displayName,
                BaseItemType = uti.BaseItem,
                BaseItemTypeName = baseItemTypeName,
                BaseCost = baseCost,
                StackSize = uti.StackSize,
                Plot = uti.Plot,
                Cursed = uti.Cursed
            };
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to parse UTI {resRef}: {ex.Message}");
            return CreateFallbackData(resRef);
        }
    }

    private string GetBaseItemTypeName(int baseItemIndex)
    {
        if (_gameDataService == null)
            return $"Type {baseItemIndex}";

        // Try to get name from baseitems.2da
        var nameStrRef = _gameDataService.Get2DAValue("baseitems", baseItemIndex, "Name");
        if (!string.IsNullOrEmpty(nameStrRef) && nameStrRef != "****")
        {
            var name = _gameDataService.GetString(nameStrRef);
            if (!string.IsNullOrEmpty(name))
                return name;
        }

        // Fall back to label
        var label = _gameDataService.Get2DAValue("baseitems", baseItemIndex, "label");
        if (!string.IsNullOrEmpty(label) && label != "****")
        {
            return FormatLabel(label);
        }

        return $"Type {baseItemIndex}";
    }

    private static string FormatLabel(string label)
    {
        // Convert "BASE_ITEM_SHORTSWORD" to "Shortsword"
        if (label.StartsWith("BASE_ITEM_", StringComparison.OrdinalIgnoreCase))
            label = label.Substring(10);

        // Convert underscores to spaces and title case
        var words = label.Split('_');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
            }
        }
        return string.Join(" ", words);
    }

    private static ResolvedItemData CreateFallbackData(string resRef)
    {
        return new ResolvedItemData
        {
            ResRef = resRef,
            DisplayName = resRef,
            BaseItemType = -1,
            BaseItemTypeName = "Unknown",
            BaseCost = 0,
            StackSize = 1,
            Plot = false,
            Cursed = false
        };
    }
}

/// <summary>
/// Resolved item data from a UTI file.
/// </summary>
public class ResolvedItemData
{
    public required string ResRef { get; init; }
    public required string DisplayName { get; init; }
    public required int BaseItemType { get; init; }
    public required string BaseItemTypeName { get; init; }
    public required int BaseCost { get; init; }
    public required ushort StackSize { get; init; }
    public required bool Plot { get; init; }
    public required bool Cursed { get; init; }

    /// <summary>
    /// Calculate sell price (what player pays to buy from store).
    /// </summary>
    /// <param name="markUp">Store markup percentage (100 = base price)</param>
    public int CalculateSellPrice(int markUp)
    {
        return (int)Math.Ceiling(BaseCost * markUp / 100.0);
    }

    /// <summary>
    /// Calculate buy price (what store pays to buy from player).
    /// </summary>
    /// <param name="markDown">Store markdown percentage (100 = full price)</param>
    public int CalculateBuyPrice(int markDown)
    {
        return (int)Math.Floor(BaseCost * markDown / 100.0);
    }
}
