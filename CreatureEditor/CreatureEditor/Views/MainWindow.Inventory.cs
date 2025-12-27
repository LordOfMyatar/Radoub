using CreatureEditor.Services;
using Radoub.Formats.Common;
using Radoub.Formats.Services;
using Radoub.Formats.Uti;
using Radoub.UI.Controls;
using Radoub.UI.ViewModels;
using System;
using System.IO;

namespace CreatureEditor.Views;

/// <summary>
/// MainWindow partial class for inventory population and item resolution.
/// Extracted from MainWindow.axaml.cs for maintainability (#582).
/// </summary>
public partial class MainWindow
{
    #region Inventory UI

    private void PopulateInventoryUI()
    {
        if (_currentCreature == null) return;

        // Clear existing data
        ClearInventoryUI();

        // Populate equipment slots from EquipItemList
        foreach (var equippedItem in _currentCreature.EquipItemList)
        {
            var slot = EquipmentSlotFactory.GetSlotByFlag(_equipmentSlots, equippedItem.Slot);
            if (slot != null && !string.IsNullOrEmpty(equippedItem.EquipRes))
            {
                // Create placeholder item from ResRef (full resolution requires game data)
                var itemVm = CreatePlaceholderItem(equippedItem.EquipRes);
                slot.EquippedItem = itemVm;
                UnifiedLogger.LogInventory(LogLevel.DEBUG, $"Equipped {equippedItem.EquipRes} to {slot.Name}");
            }
        }

        // Populate backpack from ItemList
        foreach (var invItem in _currentCreature.ItemList)
        {
            if (!string.IsNullOrEmpty(invItem.InventoryRes))
            {
                var itemVm = CreatePlaceholderItem(invItem.InventoryRes, invItem.Dropable, invItem.Pickpocketable);
                _backpackItems.Add(itemVm);
                UnifiedLogger.LogInventory(LogLevel.DEBUG, $"Added to backpack: {invItem.InventoryRes}");
            }
        }

        // Populate item palette from module directory
        PopulateItemPalette();

        UnifiedLogger.LogInventory(LogLevel.INFO, $"Populated inventory: {_currentCreature.EquipItemList.Count} equipped, {_backpackItems.Count} in backpack");
    }

    /// <summary>
    /// Creates an ItemViewModel from a ResRef, attempting to load the actual UTI file.
    /// Resolution order: Module directory → Override → HAK → BIF archives.
    /// Falls back to placeholder data if UTI file not found anywhere.
    /// </summary>
    private ItemViewModel CreatePlaceholderItem(string resRef, bool dropable = true, bool pickpocketable = false)
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
                    // GameDataService searches Override first, then HAK, then BIF
                    // For now, mark as Bif source (could enhance to track actual source)
                    source = GameResourceSource.Bif;
                    UnifiedLogger.LogInventory(LogLevel.DEBUG, $"Loaded UTI from game data: {resRef}");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogInventory(LogLevel.WARN, $"Failed to load UTI {resRef} from game data: {ex.Message}");
            }
        }

        // 3. Fall back to placeholder if UTI not found anywhere
        if (item == null)
        {
            item = new UtiFile
            {
                TemplateResRef = resRef,
                Tag = resRef
            };
            item.LocalizedName.SetString(0, resRef);
            UnifiedLogger.LogInventory(LogLevel.DEBUG, $"Created placeholder for UTI: {resRef}");

            // Return basic placeholder without factory resolution
            return new ItemViewModel(
                item,
                resolvedName: resRef,
                baseItemName: "(unknown)",
                propertiesDisplay: "",
                source: source
            );
        }

        // Use factory for proper name resolution via 2DA/TLK
        return _itemViewModelFactory.Create(item, source);
    }

    private void ClearInventoryUI()
    {
        // Clear equipment slots
        EquipmentPanel.ClearAllSlots();

        // Clear backpack
        _backpackItems.Clear();

        // Clear palette
        _paletteItems.Clear();

        // Update selection state
        HasSelection = false;
        HasBackpackSelection = false;
        HasPaletteSelection = false;
    }

    #endregion

    #region Item Palette

    /// <summary>
    /// Populates the item palette by scanning the module directory for UTI files.
    /// Items are loaded and added to _paletteItems for display in the palette list.
    /// </summary>
    private void PopulateItemPalette()
    {
        _paletteItems.Clear();

        if (string.IsNullOrEmpty(_currentFilePath))
        {
            UnifiedLogger.LogInventory(LogLevel.DEBUG, "No file path - palette empty");
            return;
        }

        var moduleDir = Path.GetDirectoryName(_currentFilePath);
        if (string.IsNullOrEmpty(moduleDir) || !Directory.Exists(moduleDir))
        {
            UnifiedLogger.LogInventory(LogLevel.DEBUG, "Module directory not found - palette empty");
            return;
        }

        // Scan for UTI files in module directory
        var utiFiles = Directory.GetFiles(moduleDir, "*.uti", SearchOption.TopDirectoryOnly);
        UnifiedLogger.LogInventory(LogLevel.INFO, $"Found {utiFiles.Length} UTI files in module directory");

        foreach (var utiPath in utiFiles)
        {
            try
            {
                var item = UtiReader.Read(utiPath);
                var viewModel = _itemViewModelFactory.Create(item, GameResourceSource.Module);
                _paletteItems.Add(viewModel);
            }
            catch (Exception ex)
            {
                var fileName = Path.GetFileName(utiPath);
                UnifiedLogger.LogInventory(LogLevel.WARN, $"Failed to load UTI {fileName}: {ex.Message}");
            }
        }

        UnifiedLogger.LogInventory(LogLevel.INFO, $"Loaded {_paletteItems.Count} items into palette");
    }

    #endregion
}
