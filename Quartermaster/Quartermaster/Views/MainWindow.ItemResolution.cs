using Radoub.Formats.Logging;
using Radoub.Formats.Common;
using Radoub.Formats.Services;
using Radoub.Formats.Uti;
using Radoub.UI.ViewModels;
using System;
using System.IO;

namespace Quartermaster.Views;

/// <summary>
/// MainWindow partial class for item resolution from UTI files.
/// Handles loading items from module, Override, HAK, and BIF archives.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// Creates an ItemViewModel for a backpack item with full inventory metadata.
    /// Resolution order: Module directory → Override → HAK → BIF archives.
    /// </summary>
    private ItemViewModel CreateBackpackItem(Radoub.Formats.Utc.InventoryItem invItem)
    {
        var (item, source) = ResolveUtiFile(invItem.InventoryRes);

        if (item == null)
        {
            item = new UtiFile
            {
                TemplateResRef = invItem.InventoryRes,
                Tag = invItem.InventoryRes
            };
            item.LocalizedName.SetString(0, invItem.InventoryRes);
            UnifiedLogger.LogInventory(LogLevel.DEBUG, $"Created placeholder for UTI: {invItem.InventoryRes}");

            return new ItemViewModel(
                item,
                resolvedName: invItem.InventoryRes,
                baseItemName: "(unknown)",
                propertiesDisplay: "",
                invItem.Repos_PosX, invItem.Repos_PosY,
                invItem.Dropable, invItem.Pickpocketable,
                source: source
            );
        }

        var viewModel = _itemViewModelFactory.CreateBackpackItem(
            item,
            invItem.Repos_PosX, invItem.Repos_PosY,
            invItem.Dropable, invItem.Pickpocketable,
            source);
        SetupLazyIconLoading(viewModel);
        return viewModel;
    }

    /// <summary>
    /// Creates an ItemViewModel from a ResRef for equipped items (no grid position needed).
    /// Resolution order: Module directory → Override → HAK → BIF archives.
    /// </summary>
    private ItemViewModel CreatePlaceholderItem(string resRef)
    {
        var (item, source) = ResolveUtiFile(resRef);

        if (item == null)
        {
            item = new UtiFile
            {
                TemplateResRef = resRef,
                Tag = resRef
            };
            item.LocalizedName.SetString(0, resRef);
            UnifiedLogger.LogInventory(LogLevel.DEBUG, $"Created placeholder for UTI: {resRef}");

            return new ItemViewModel(
                item,
                resolvedName: resRef,
                baseItemName: "(unknown)",
                propertiesDisplay: "",
                source: source
            );
        }

        var viewModel = _itemViewModelFactory.Create(item, source);
        SetupLazyIconLoading(viewModel);
        return viewModel;
    }

    /// <summary>
    /// Resolves a UTI file from ResRef, checking module directory first, then game data.
    /// </summary>
    private (UtiFile? item, GameResourceSource source) ResolveUtiFile(string resRef)
    {
        UtiFile? item = null;
        var source = GameResourceSource.Bif;

        // 1. Try module directory first (highest priority for module-specific items)
        if (!string.IsNullOrEmpty(_currentFilePath))
        {
            var moduleDir = Path.GetDirectoryName(_currentFilePath);
            if (moduleDir != null)
            {
                var utiPath = Path.Combine(moduleDir, resRef + ".uti");
                if (File.Exists(utiPath))
                {
                    try
                    {
                        item = UtiReader.Read(utiPath);
                        source = GameResourceSource.Module;
                        UnifiedLogger.LogInventory(LogLevel.DEBUG, $"Loaded UTI from module: {resRef}");
                    }
                    catch (Exception ex)
                    {
                        UnifiedLogger.LogInventory(LogLevel.WARN, $"Failed to load UTI {resRef} from module: {ex.Message}");
                    }
                }
            }
        }

        // 2. Try GameDataService (Override → HAK → BIF) if not found in module
        if (item == null && _gameDataService.IsConfigured)
        {
            try
            {
                var utiData = _gameDataService.FindResource(resRef, ResourceTypes.Uti);
                if (utiData != null)
                {
                    item = UtiReader.Read(utiData);
                    source = GameResourceSource.Bif;
                    UnifiedLogger.LogInventory(LogLevel.DEBUG, $"Loaded UTI from game data: {resRef}");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogInventory(LogLevel.WARN, $"Failed to load UTI {resRef} from game data: {ex.Message}");
            }
        }

        return (item, source);
    }
}
