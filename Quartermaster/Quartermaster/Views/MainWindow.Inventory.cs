using Quartermaster.Services;
using Quartermaster.Views.Panels;
using Radoub.Formats.Logging;
using Radoub.Formats.Common;
using Radoub.Formats.Services;
using Radoub.Formats.Uti;
using Radoub.UI.Controls;
using Radoub.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Quartermaster.Views;

/// <summary>
/// MainWindow partial class for inventory population and operations.
/// Split into partial classes for maintainability.
/// </summary>
public partial class MainWindow
{
    // Track if inventory was modified (only sync if true)
    private bool _inventoryModified = false;

    #region Inventory UI

    private void PopulateInventoryUI()
    {
        if (_currentCreature == null) return;

        ClearInventoryUI();

        // Populate equipment slots from EquipItemList
        UnifiedLogger.LogInventory(LogLevel.INFO, $"Processing {_currentCreature.EquipItemList.Count} equipped items, have {_equipmentSlots.Count} slots");
        foreach (var equippedItem in _currentCreature.EquipItemList)
        {
            UnifiedLogger.LogInventory(LogLevel.INFO, $"Looking for slot with flag {equippedItem.Slot} (0x{equippedItem.Slot:X}) for {equippedItem.EquipRes}");
            var slot = EquipmentSlotFactory.GetSlotByFlag(_equipmentSlots, equippedItem.Slot);
            if (slot != null && !string.IsNullOrEmpty(equippedItem.EquipRes))
            {
                var itemVm = CreatePlaceholderItem(equippedItem.EquipRes);
                slot.EquippedItem = itemVm;
                UnifiedLogger.LogInventory(LogLevel.INFO, $"Equipped {equippedItem.EquipRes} to {slot.Name}");
            }
            else if (slot == null)
            {
                UnifiedLogger.LogInventory(LogLevel.WARN, $"No slot found for flag {equippedItem.Slot} (0x{equippedItem.Slot:X})");
            }
        }

        // Populate backpack from ItemList
        foreach (var invItem in _currentCreature.ItemList)
        {
            if (!string.IsNullOrEmpty(invItem.InventoryRes))
            {
                var itemVm = CreateBackpackItem(invItem);
                InventoryPanelContent.BackpackItems.Add(itemVm);
                UnifiedLogger.LogInventory(LogLevel.DEBUG, $"Added to backpack: {invItem.InventoryRes}");
            }
        }

        // Populate item palette from module directory
        PopulateItemPalette();

        _inventoryModified = false;

        UnifiedLogger.LogInventory(LogLevel.INFO, $"Populated inventory: {_currentCreature.EquipItemList.Count} equipped, {InventoryPanelContent.BackpackItems.Count} in backpack");
    }

    private void ClearInventoryUI()
    {
        InventoryPanelContent.ClearAll();
        HasSelection = false;
    }

    #endregion

    #region Inventory Operations

    /// <summary>
    /// Handles item dropped on backpack list.
    /// </summary>
    private void OnBackpackItemDropped(object? sender, Radoub.UI.Controls.ItemDropEventArgs e)
    {
        if (e.DataObject.Contains("EquippedItem"))
        {
            var data = e.DataObject.Get("EquippedItem");
            if (data is ItemViewModel equippedItem)
            {
                var slot = _equipmentSlots.FirstOrDefault(s => s.EquippedItem == equippedItem);
                if (slot != null)
                {
                    UnequipToBackpack(slot);
                    UnifiedLogger.LogInventory(LogLevel.INFO,
                        $"Dropped {equippedItem.Name} from {slot.Name} to backpack");
                }
            }
        }
    }

    /// <summary>
    /// Handles add to backpack request from palette.
    /// </summary>
    private void OnAddToBackpackRequested(object? sender, ItemViewModel[] items)
    {
        foreach (var paletteItem in items)
        {
            if (paletteItem.Item == null)
            {
                UnifiedLogger.LogInventory(LogLevel.WARN, $"Cannot add item to backpack: Item data is null for {paletteItem.ResRef}");
                continue;
            }

            var nextPos = GetNextBackpackPosition();

            var backpackItem = _itemViewModelFactory.CreateBackpackItem(
                paletteItem.Item,
                nextPos.x, nextPos.y,
                isDropable: true,
                isPickpocketable: false,
                paletteItem.Source);
            SetupLazyIconLoading(backpackItem);

            InventoryPanelContent.AddToBackpack(backpackItem);
        }

        _inventoryModified = true;
        MarkDirty();
    }

    private (ushort x, ushort y) GetNextBackpackPosition()
    {
        const int GridWidth = 6;
        var count = InventoryPanelContent.BackpackItems.Count;
        return ((ushort)(count % GridWidth), (ushort)(count / GridWidth));
    }

    /// <summary>
    /// Handles equip items request from palette.
    /// </summary>
    private void OnEquipItemsRequested(object? sender, ItemViewModel[] items)
    {
        var validator = new Radoub.UI.Services.EquipmentSlotValidator(_gameDataService);

        foreach (var item in items)
        {
            var validSlotsBitmask = validator.GetEquipableSlots(item.BaseItem);

            if (validSlotsBitmask == null || validSlotsBitmask == 0)
            {
                UnifiedLogger.LogInventory(LogLevel.WARN,
                    $"Cannot equip {item.Name}: no valid equipment slots for base item {item.BaseItem}");
                continue;
            }

            EquipmentSlotViewModel? targetSlot = null;
            foreach (var slot in _equipmentSlots)
            {
                if ((validSlotsBitmask.Value & slot.SlotFlag) != 0 && !slot.HasItem)
                {
                    targetSlot = slot;
                    break;
                }
            }

            if (targetSlot == null)
            {
                foreach (var slot in _equipmentSlots)
                {
                    if ((validSlotsBitmask.Value & slot.SlotFlag) != 0)
                    {
                        targetSlot = slot;
                        break;
                    }
                }
            }

            if (targetSlot != null)
            {
                targetSlot.EquippedItem = item;
                UnifiedLogger.LogInventory(LogLevel.INFO, $"Equipped {item.Name} to {targetSlot.Name}");
            }
        }

        _inventoryModified = true;
        MarkDirty();
    }

    /// <summary>
    /// Unequips an item from a slot and adds it to backpack.
    /// </summary>
    public void UnequipToBackpack(EquipmentSlotViewModel slot)
    {
        if (!slot.HasItem || slot.EquippedItem == null) return;

        var item = slot.EquippedItem;
        if (item.Item == null)
        {
            UnifiedLogger.LogInventory(LogLevel.WARN, $"Cannot unequip: Item data is null for {item.ResRef}");
            return;
        }

        var nextPos = GetNextBackpackPosition();
        var backpackItem = _itemViewModelFactory.CreateBackpackItem(
            item.Item,
            nextPos.x, nextPos.y,
            isDropable: true,
            isPickpocketable: false,
            item.Source);
        SetupLazyIconLoading(backpackItem);

        slot.EquippedItem = null;

        InventoryPanelContent.AddToBackpack(backpackItem);
        _inventoryModified = true;
        MarkDirty();

        UnifiedLogger.LogInventory(LogLevel.INFO, $"Unequipped {item.Name} from {slot.Name} to backpack");
    }

    #endregion

    #region Inventory Sync

    /// <summary>
    /// Syncs all inventory UI state back to the creature data model.
    /// </summary>
    public void SyncInventoryToCreature()
    {
        if (_currentCreature == null) return;

        if (!_inventoryModified)
        {
            UnifiedLogger.LogInventory(LogLevel.DEBUG, "Inventory not modified, skipping sync");
            return;
        }

        SyncBackpackToCreature();
        SyncEquipmentToCreature();

        UnifiedLogger.LogInventory(LogLevel.INFO,
            $"Synced inventory: {_currentCreature.ItemList.Count} backpack, {_currentCreature.EquipItemList.Count} equipped");
    }

    private void SyncBackpackToCreature()
    {
        if (_currentCreature == null) return;

        _currentCreature.ItemList.Clear();

        foreach (var itemVm in InventoryPanelContent.BackpackItems)
        {
            var invItem = new Radoub.Formats.Utc.InventoryItem
            {
                InventoryRes = itemVm.ResRef,
                Repos_PosX = itemVm.GridPositionX,
                Repos_PosY = itemVm.GridPositionY,
                Dropable = itemVm.IsDropable,
                Pickpocketable = itemVm.IsPickpocketable
            };
            _currentCreature.ItemList.Add(invItem);
        }
    }

    private void SyncEquipmentToCreature()
    {
        if (_currentCreature == null) return;

        _currentCreature.EquipItemList.Clear();

        foreach (var slot in _equipmentSlots)
        {
            if (slot.HasItem && slot.EquippedItem != null)
            {
                var equipItem = new Radoub.Formats.Utc.EquippedItem
                {
                    Slot = slot.SlotFlag,
                    EquipRes = slot.EquippedItem.ResRef
                };
                _currentCreature.EquipItemList.Add(equipItem);
            }
        }
    }

    #endregion
}
