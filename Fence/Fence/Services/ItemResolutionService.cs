using System;
using System.Collections.Generic;
using System.IO;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Uti;

namespace MerchantEditor.Services;

/// <summary>
/// Service for resolving item data from UTI files.
/// Resolution order: Module directory → Override → HAK → BIF archives.
/// Provides caching to avoid repeated file reads.
/// </summary>
public class ItemResolutionService
{
    private readonly IGameDataService? _gameDataService;
    private readonly Dictionary<string, ResolvedItemData> _cache = new(StringComparer.OrdinalIgnoreCase);
    private string? _moduleDirectory;

    public ItemResolutionService(IGameDataService? gameDataService)
    {
        _gameDataService = gameDataService;

        // Log configuration status on creation
        if (_gameDataService == null)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "ItemResolutionService: GameDataService is null - BIF lookup disabled");
        }
        else if (!_gameDataService.IsConfigured)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "ItemResolutionService: GameDataService not configured - BIF lookup disabled");
        }
        else
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, "ItemResolutionService: GameDataService configured - BIF lookup enabled");
        }
    }

    /// <summary>
    /// Sets the current file path to enable module-local item resolution.
    /// Items in the same directory as the UTM file take precedence.
    /// </summary>
    public void SetCurrentFilePath(string? filePath)
    {
        _moduleDirectory = string.IsNullOrEmpty(filePath) ? null : Path.GetDirectoryName(filePath);
        ClearCache(); // Clear cache when file context changes
        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"ItemResolutionService: Module directory set to: {_moduleDirectory ?? "(none)"}");
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
        UtiFile? uti = null;

        // 1. Try module directory first (highest priority for module-specific items)
        if (!string.IsNullOrEmpty(_moduleDirectory))
        {
            var utiPath = Path.Combine(_moduleDirectory, resRef + ".uti");
            if (File.Exists(utiPath))
            {
                try
                {
                    uti = UtiReader.Read(utiPath);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Loaded UTI from module: {resRef}");
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to load UTI {resRef} from module: {ex.Message}");
                }
            }
        }

        // 2. Try GameDataService (Override → HAK → BIF) if not found in module
        if (uti == null && _gameDataService != null && _gameDataService.IsConfigured)
        {
            try
            {
                var utiData = _gameDataService.FindResource(resRef, ResourceTypes.Uti);
                if (utiData != null)
                {
                    uti = UtiReader.Read(utiData);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Loaded UTI from game data: {resRef}");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to load UTI {resRef} from game data: {ex.Message}");
            }
        }

        // 3. Return fallback if UTI not found anywhere
        if (uti == null)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"UTI not found in module or game data: {resRef}");
            return CreateFallbackData(resRef);
        }

        try
        {
            // Get display name with full resolution chain
            var displayName = ResolveDisplayName(uti, resRef);

            // Get base item type name
            var baseItemTypeName = GetBaseItemTypeName(uti.BaseItem);

            // Get base cost from UTI
            var baseCost = (int)uti.Cost;

            return new ResolvedItemData
            {
                ResRef = resRef,
                Tag = uti.Tag,
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

    private string ResolveDisplayName(UtiFile uti, string resRef)
    {
        // 1. Try localized name string first
        var defaultString = uti.LocalizedName.GetDefault();
        if (!string.IsNullOrEmpty(defaultString) && !IsInvalidTlkString(defaultString))
            return defaultString;

        // 2. Fall back to TLK reference if LocalizedName has a StrRef
        if (uti.LocalizedName.StrRef != 0xFFFFFFFF && _gameDataService != null)
        {
            var tlkString = _gameDataService.GetString(uti.LocalizedName.StrRef);
            if (!string.IsNullOrEmpty(tlkString) && !IsInvalidTlkString(tlkString))
                return tlkString;
        }

        // 3. Fall back to ResRef from UTI
        if (!string.IsNullOrEmpty(uti.TemplateResRef))
            return uti.TemplateResRef;

        // 4. Final fallback to the resRef we were looking for
        return resRef;
    }

    /// <summary>
    /// Check if a TLK string is a placeholder/garbage value that should be skipped.
    /// </summary>
    private static bool IsInvalidTlkString(string value)
    {
        var trimmed = value.Trim();

        // Common placeholder values in NWN TLK files
        return trimmed.Equals("BadStrRef", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("BadStreff", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("DELETED", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("DELETE_ME", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("Padding", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("PAdding", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("Bad Str", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("Xp2spec", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains("deleted", StringComparison.OrdinalIgnoreCase);
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
            if (!string.IsNullOrEmpty(name) && !IsInvalidTlkString(name))
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
            Tag = resRef,
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
    public required string Tag { get; init; }
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
