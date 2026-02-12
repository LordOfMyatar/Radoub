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
    /// Handles item dropped on an equipment slot.
    /// Supports drops from backpack (BackpackItem format) and palette (PaletteItem/ItemViewModels format).
    /// </summary>
    private void OnEquipmentSlotItemDropped(object? sender, Radoub.UI.Controls.EquipmentSlotDropEventArgs e)
    {
        var slot = e.TargetSlot;
        ItemViewModel? droppedItem = null;

        // Try BackpackItem format first
        if (e.DataObject.Contains("BackpackItem"))
        {
            var data = e.DataObject.Get("BackpackItem");
            if (data is System.Collections.Generic.IReadOnlyList<ItemViewModel> items && items.Count > 0)
                droppedItem = items[0];
            else if (data is ItemViewModel single)
                droppedItem = single;
        }
        // Try PaletteItem format
        else if (e.DataObject.Contains("PaletteItem"))
        {
            var data = e.DataObject.Get("PaletteItem");
            if (data is System.Collections.Generic.IReadOnlyList<ItemViewModel> items && items.Count > 0)
                droppedItem = items[0];
            else if (data is ItemViewModel single)
                droppedItem = single;
        }
        // Try generic ItemViewModels format
        else if (e.DataObject.Contains("ItemViewModels"))
        {
            var data = e.DataObject.Get("ItemViewModels");
            if (data is System.Collections.Generic.IReadOnlyList<ItemViewModel> items && items.Count > 0)
                droppedItem = items[0];
            else if (data is ItemViewModel single)
                droppedItem = single;
        }

        if (droppedItem == null)
        {
            UnifiedLogger.LogInventory(LogLevel.DEBUG, $"Drop on {slot.Name}: no recognized data format");
            return;
        }

        // Load UtiFile on demand
        var utiFile = droppedItem.Item ?? LoadItemFromResRef(droppedItem.ResRef, droppedItem.Source);
        if (utiFile == null)
        {
            UnifiedLogger.LogInventory(LogLevel.WARN, $"Cannot equip dropped item: Failed to load {droppedItem.ResRef}");
            return;
        }

        var equippedItem = ItemFactory.Create(utiFile, droppedItem.Source);
        SetupLazyIconLoading(equippedItem);
        slot.EquippedItem = equippedItem;

        // If dragged from backpack, remove from backpack
        if (e.DataObject.Contains("BackpackItem"))
        {
            InventoryPanelContent.RemoveFromBackpack(droppedItem);
        }

        _inventoryModified = true;
        MarkDirty();
        UnifiedLogger.LogInventory(LogLevel.INFO, $"Dropped {equippedItem.Name} onto {slot.Name}");
    }

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
            // Load UtiFile on demand if not already loaded (cache-loaded palette items)
            var utiFile = paletteItem.Item ?? LoadItemFromResRef(paletteItem.ResRef, paletteItem.Source);
            if (utiFile == null)
            {
                UnifiedLogger.LogInventory(LogLevel.WARN, $"Cannot add item to backpack: Failed to load {paletteItem.ResRef}");
                continue;
            }

            var nextPos = GetNextBackpackPosition();

            var backpackItem = ItemFactory.CreateBackpackItem(
                utiFile,
                nextPos.x, nextPos.y,
                isDropable: false,  // Default to not droppable for game balance
                isPickpocketable: false,
                paletteItem.Source);
            SetupLazyIconLoading(backpackItem);

            InventoryPanelContent.AddToBackpack(backpackItem);
        }

        _inventoryModified = true;
        MarkDirty();
    }

    /// <summary>
    /// Load a UtiFile from game data by ResRef.
    /// </summary>
    private UtiFile? LoadItemFromResRef(string resRef, GameResourceSource source)
    {
        if (string.IsNullOrEmpty(resRef))
            return null;

        try
        {
            var utiData = GameData.FindResource(resRef, ResourceTypes.Uti);
            if (utiData != null)
            {
                return UtiReader.Read(utiData);
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogInventory(LogLevel.WARN, $"Failed to load item {resRef}: {ex.Message}");
        }

        return null;
    }

    private (ushort x, ushort y) GetNextBackpackPosition()
    {
        const int GridWidth = 6;
        var count = InventoryPanelContent.BackpackItems.Count;
        return ((ushort)(count % GridWidth), (ushort)(count / GridWidth));
    }

    /// <summary>
    /// Handles equip items request from palette.
    /// Only considers standard equipment slots (not creature natural slots like Claws/Skin).
    /// </summary>
    private void OnEquipItemsRequested(object? sender, ItemViewModel[] items)
    {
        UnifiedLogger.LogInventory(LogLevel.INFO, $"OnEquipItemsRequested: {items.Length} items selected");

        var validator = new Radoub.UI.Services.EquipmentSlotValidator(GameData);

        // Only consider standard slots (not natural/creature slots) when equipping from palette
        // Natural slots (Claw1-3, Skin) are creature-only and should be edited directly
        var standardSlots = _equipmentSlots.Where(s => s.IsStandard).ToList();

        foreach (var paletteItem in items)
        {
            UnifiedLogger.LogInventory(LogLevel.DEBUG,
                $"Processing item: {paletteItem.Name} (BaseItem={paletteItem.BaseItem}, ResRef={paletteItem.ResRef})");

            var validSlotsBitmask = validator.GetEquipableSlots(paletteItem.BaseItem);

            if (validSlotsBitmask == null || validSlotsBitmask == 0)
            {
                UnifiedLogger.LogInventory(LogLevel.WARN,
                    $"Cannot equip {paletteItem.Name}: no valid equipment slots for base item {paletteItem.BaseItem} (EquipableSlots={validSlotsBitmask?.ToString() ?? "null"})");
                continue;
            }

            // Mask out creature-only slots (Claw1=0x4000, Claw2=0x8000, Claw3=0x10000, Skin=0x20000)
            const int StandardSlotsMask = 0x3FFF; // Bits 0-13 only
            var standardSlotsBitmask = validSlotsBitmask.Value & StandardSlotsMask;

            if (standardSlotsBitmask == 0)
            {
                UnifiedLogger.LogInventory(LogLevel.WARN,
                    $"Cannot equip {paletteItem.Name}: item has no standard equipment slots (creature-only item?)");
                continue;
            }

            UnifiedLogger.LogInventory(LogLevel.DEBUG,
                $"Valid standard slots bitmask for {paletteItem.Name}: 0x{standardSlotsBitmask:X}");

            // Find first empty matching standard slot
            EquipmentSlotViewModel? targetSlot = null;
            foreach (var slot in standardSlots)
            {
                if ((standardSlotsBitmask & slot.SlotFlag) != 0 && !slot.HasItem)
                {
                    targetSlot = slot;
                    break;
                }
            }

            // If no empty slot, find first matching slot (will replace)
            if (targetSlot == null)
            {
                foreach (var slot in standardSlots)
                {
                    if ((standardSlotsBitmask & slot.SlotFlag) != 0)
                    {
                        targetSlot = slot;
                        break;
                    }
                }
            }

            if (targetSlot == null)
            {
                UnifiedLogger.LogInventory(LogLevel.WARN,
                    $"Cannot equip {paletteItem.Name}: no matching standard slot found for bitmask 0x{standardSlotsBitmask:X}");
                continue;
            }

            // Load UtiFile on demand if not already loaded (cache-loaded palette items)
            var utiFile = paletteItem.Item ?? LoadItemFromResRef(paletteItem.ResRef, paletteItem.Source);
            if (utiFile == null)
            {
                UnifiedLogger.LogInventory(LogLevel.WARN, $"Cannot equip {paletteItem.Name}: Failed to load item data");
                continue;
            }

            var equippedItem = ItemFactory.Create(utiFile, paletteItem.Source);
            SetupLazyIconLoading(equippedItem);
            targetSlot.EquippedItem = equippedItem;
            UnifiedLogger.LogInventory(LogLevel.INFO, $"Equipped {equippedItem.Name} to {targetSlot.Name} (slot flag 0x{targetSlot.SlotFlag:X})");
        }

        _inventoryModified = true;
        MarkDirty();
    }

    /// <summary>
    /// Handles equip request for a single backpack item via context menu.
    /// Equips the item and removes it from backpack (move, not copy).
    /// </summary>
    private void OnEquipFromBackpackRequested(object? sender, ItemViewModel item)
    {
        // Snapshot which slots have items before equipping
        var previousSlotStates = _equipmentSlots.ToDictionary(s => s, s => s.EquippedItem);

        // Find which slot will be targeted so we can swap if occupied
        var validator = new Radoub.UI.Services.EquipmentSlotValidator(GameData);
        var validSlotsBitmask = validator.GetEquipableSlots(item.BaseItem);
        if (validSlotsBitmask != null && validSlotsBitmask != 0)
        {
            const int StandardSlotsMask = 0x3FFF;
            var standardBits = validSlotsBitmask.Value & StandardSlotsMask;
            var standardSlots = _equipmentSlots.Where(s => s.IsStandard).ToList();

            // Find the slot that OnEquipItemsRequested will target (same logic: empty first, then first match)
            var targetSlot = standardSlots.FirstOrDefault(s => (standardBits & s.SlotFlag) != 0 && !s.HasItem)
                          ?? standardSlots.FirstOrDefault(s => (standardBits & s.SlotFlag) != 0);

            // If the target slot has an existing item, unequip it to backpack first (swap)
            if (targetSlot?.HasItem == true)
            {
                UnifiedLogger.LogInventory(LogLevel.INFO,
                    $"Swapping: moving {targetSlot.EquippedItem!.Name} from {targetSlot.Name} to backpack");
                UnequipToBackpack(targetSlot);
            }
        }

        OnEquipItemsRequested(sender, new[] { item });

        // If an equipment slot changed, the item was equipped - remove from backpack
        var equipped = _equipmentSlots.Any(s => s.EquippedItem != previousSlotStates[s]);
        if (equipped)
        {
            InventoryPanelContent.RemoveFromBackpack(item);
        }
    }

    /// <summary>
    /// Handles delete request for a backpack item via context menu.
    /// </summary>
    private void OnDeleteFromBackpackRequested(object? sender, ItemViewModel item)
    {
        InventoryPanelContent.RemoveFromBackpack(item);
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

        // Load UtiFile on demand if not already loaded
        var utiFile = item.Item ?? LoadItemFromResRef(item.ResRef, item.Source);
        if (utiFile == null)
        {
            UnifiedLogger.LogInventory(LogLevel.WARN, $"Cannot unequip: Failed to load item data for {item.ResRef}");
            return;
        }

        var nextPos = GetNextBackpackPosition();
        var backpackItem = ItemFactory.CreateBackpackItem(
            utiFile,
            nextPos.x, nextPos.y,
            isDropable: false,  // Default to not droppable for game balance
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
