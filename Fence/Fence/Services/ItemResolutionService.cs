using System;
using System.Collections.Generic;
using System.IO;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Uti;
using Radoub.UI.Services;
using Radoub.UI.ViewModels;

namespace MerchantEditor.Services;

/// <summary>
/// Service for resolving item data from UTI files.
/// Resolution order: Module directory → Override → HAK → BIF archives.
/// Provides caching to avoid repeated file reads.
/// </summary>
public class ItemResolutionService
{
    private readonly IGameDataService? _gameDataService;
    private readonly ITlkService? _tlkService;
    private readonly ItemViewModelFactory? _itemViewModelFactory;
    private readonly Dictionary<string, ResolvedItemData> _cache = new(StringComparer.OrdinalIgnoreCase);
    private string? _moduleDirectory;

    public ItemResolutionService(IGameDataService? gameDataService, ITlkService? tlkService = null, ItemViewModelFactory? itemViewModelFactory = null)
    {
        _gameDataService = gameDataService;
        _tlkService = tlkService;
        _itemViewModelFactory = itemViewModelFactory;

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
        string sourceLocation = string.Empty;

        // 1. Try module directory first (highest priority for module-specific items)
        if (!string.IsNullOrEmpty(_moduleDirectory))
        {
            var utiPath = Path.Combine(_moduleDirectory, resRef + ".uti");
            if (File.Exists(utiPath))
            {
                try
                {
                    uti = UtiReader.Read(utiPath);
                    sourceLocation = Path.GetFileName(utiPath);
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
                var result = _gameDataService.FindResourceWithSource(resRef, ResourceTypes.Uti);
                if (result != null)
                {
                    uti = UtiReader.Read(result.Data);
                    sourceLocation = !string.IsNullOrEmpty(result.SourcePath)
                        ? Path.GetFileName(result.SourcePath)
                        : result.Source.ToString();
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

            // Resolve item properties display string
            var propertiesDisplay = _itemViewModelFactory?.GetPropertiesDisplay(uti.Properties) ?? string.Empty;

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
                Cursed = uti.Cursed,
                PropertiesDisplay = propertiesDisplay,
                SourceLocation = sourceLocation
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
        // Use TlkService for language-aware resolution (#1361)
        if (_tlkService != null)
        {
            var resolved = _tlkService.ResolveLocString(uti.LocalizedName);
            if (!string.IsNullOrEmpty(resolved))
                return resolved;
        }
        else
        {
            // Fallback: no TlkService available - use basic resolution
            var defaultString = uti.LocalizedName.GetDefault();
            if (TlkHelper.IsValidTlkString(defaultString))
                return defaultString!;

            if (uti.LocalizedName.StrRef != 0xFFFFFFFF && _gameDataService != null)
            {
                var tlkString = _gameDataService.GetString(uti.LocalizedName.StrRef);
                if (TlkHelper.IsValidTlkString(tlkString))
                    return tlkString!;
            }
        }

        // Fall back to ResRef from UTI
        if (!string.IsNullOrEmpty(uti.TemplateResRef))
            return uti.TemplateResRef;

        // Final fallback to the resRef we were looking for
        return resRef;
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
            if (TlkHelper.IsValidTlkString(name))
                return name!;
        }

        // Fall back to label
        var label = _gameDataService.Get2DAValue("baseitems", baseItemIndex, "label");
        if (!string.IsNullOrEmpty(label) && label != "****")
        {
            return TlkHelper.FormatBaseItemLabel(label);
        }

        return $"Type {baseItemIndex}";
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
    public string PropertiesDisplay { get; init; } = string.Empty;
    public string SourceLocation { get; init; } = string.Empty;

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
