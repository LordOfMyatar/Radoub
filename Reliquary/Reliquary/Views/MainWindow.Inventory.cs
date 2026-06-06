using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Uti;
using Radoub.UI.Services;
using Radoub.UI.ViewModels;
using PlaceableEditor.Commands;
using PlaceableEditor.Views.Panels;

namespace PlaceableEditor.Views;

/// <summary>
/// Inventory wiring for Reliquary's MainWindow (design §5.4). Shows the InventoryPanel only when
/// the placeable Has Inventory, loads the backpack from the model's <c>ItemList</c> (UTI resolved
/// module → Override → HAK → BIF), populates the UTI palette from the shared item cache, and
/// routes Add/Remove through the undo manager as <see cref="AddInventoryItemCommand"/> /
/// <see cref="RemoveInventoryItemCommand"/>. The model's <c>ItemList</c> and the panel's UI
/// collection stay index-aligned (the commands own that invariant).
/// </summary>
public partial class MainWindow
{
    private readonly ISharedPaletteCacheService _itemCache = new SharedPaletteCacheService();
    private ItemViewModelFactory? _itemFactory;
    private bool _inventoryWired;
    private bool _paletteLoaded;

    /// <summary>Connect the inventory panel's add/remove + resolver hooks once.</summary>
    private void WireInventory()
    {
        if (_inventoryWired) return;
        _inventoryWired = true;

        var inv = this.FindControl<InventoryPanel>("InventoryPanel");
        if (inv == null) return;

        inv.AddItemRequested += OnInventoryAddRequested;
        inv.RemoveItemRequested += OnInventoryRemoveRequested;
        inv.ItemResolver = ResolveForDetails;
    }

    /// <summary>
    /// Refresh inventory after a placeable loads: toggle visibility on Has Inventory, fill the
    /// backpack from the model, and load the palette on first reveal. Called from LoadPlaceable.
    /// </summary>
    private void RefreshInventory()
    {
        var inv = this.FindControl<InventoryPanel>("InventoryPanel");
        if (inv == null || _placeable == null) return;

        // Has Inventory controls the panel's visibility (design §5.1 / §5.4).
        inv.IsVisible = _placeable.HasInventory;
        // Keep visibility live as the user toggles the flag in the identity panel.
        _placeable.PropertyChanged -= OnPlaceableInventoryFlagChanged;
        _placeable.PropertyChanged += OnPlaceableInventoryFlagChanged;

        if (!_placeable.HasInventory) return;

        inv.SetBackpackItems(BuildBackpackItems());
        EnsurePaletteLoaded(inv);
    }

    private void OnPlaceableInventoryFlagChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ViewModels.PlaceableViewModel.HasInventory)) return;
        var inv = this.FindControl<InventoryPanel>("InventoryPanel");
        if (inv == null || _placeable == null) return;

        inv.IsVisible = _placeable.HasInventory;
        if (_placeable.HasInventory)
        {
            inv.SetBackpackItems(BuildBackpackItems());
            EnsurePaletteLoaded(inv);
        }
    }

    /// <summary>Resolve each model ItemList entry to a backpack ItemViewModel (placeholder on miss).</summary>
    private List<ItemViewModel> BuildBackpackItems()
    {
        var items = new List<ItemViewModel>();
        if (_placeable == null) return items;

        foreach (var entry in _placeable.Utp.ItemList)
            items.Add(ResolveBackpackItem(entry.InventoryRes));

        return items;
    }

    private ItemViewModel ResolveBackpackItem(string resRef)
    {
        var (uti, source) = ResolveUtiFile(resRef);
        if (uti != null && _itemFactory != null)
            return _itemFactory.Create(uti, source);

        // Unresolved UTI — show a placeholder so the row is still visible/removable.
        return new ItemViewModel
        {
            Name = resRef,
            ResRef = resRef,
            BaseItemName = "(unresolved)",
            Source = source
        };
    }

    /// <summary>Resolve a palette row to full data for the read-only details pane.</summary>
    private ItemViewModel? ResolveForDetails(ItemViewModel cacheItem)
    {
        if (cacheItem.Item != null) return cacheItem; // already fully loaded
        var (uti, source) = ResolveUtiFile(cacheItem.ResRef);
        return uti != null && _itemFactory != null ? _itemFactory.Create(uti, source) : cacheItem;
    }

    /// <summary>UTI resolution cascade: module directory → Override → HAK → BIF.</summary>
    private (UtiFile? item, GameResourceSource source) ResolveUtiFile(string resRef)
    {
        if (string.IsNullOrEmpty(resRef)) return (null, GameResourceSource.Bif);

        // 1. Module directory (same folder as the open UTP).
        if (!string.IsNullOrEmpty(_currentFilePath))
        {
            var moduleDir = Path.GetDirectoryName(_currentFilePath);
            if (!string.IsNullOrEmpty(moduleDir))
            {
                var utiPath = Path.Combine(moduleDir, resRef + ".uti");
                if (File.Exists(utiPath))
                {
                    try { return (UtiReader.Read(utiPath), GameResourceSource.Module); }
                    catch (Exception ex) when (ex is IOException or InvalidDataException)
                    {
                        UnifiedLogger.LogApplication(LogLevel.WARN,
                            $"Reliquary: failed to read module UTI {resRef}: {ex.Message}");
                    }
                }
            }
        }

        // 2. Override → HAK → BIF via game data.
        if (_gameData is { IsConfigured: true })
        {
            try
            {
                var bytes = _gameData.FindResource(resRef, ResourceTypes.Uti);
                if (bytes != null) return (UtiReader.Read(bytes), GameResourceSource.Bif);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Reliquary: failed to read UTI {resRef} from game data: {ex.Message}");
            }
        }

        return (null, GameResourceSource.Bif);
    }

    /// <summary>
    /// Populate the UTI palette from the shared item cache (the same cache QM/Relique build at
    /// ~/Radoub/Cache/ItemPalette). No rebuild here — if the cache is cold the palette is empty
    /// and the user can still add module items by ResRef later. Loaded once per session.
    /// </summary>
    private void EnsurePaletteLoaded(InventoryPanel inv)
    {
        if (_paletteLoaded) return;
        _paletteLoaded = true;

        List<SharedPaletteCacheItem>? cache;
        try { cache = _itemCache.GetAggregatedCache(); }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Reliquary: item palette cache read failed: {ex.Message}");
            return;
        }
        if (cache == null || cache.Count == 0)
        {
            UpdateStatus("Item palette cache empty — build it in Quartermaster/Relique to populate.");
            return;
        }

        var vms = cache.Select(c => new ItemViewModel
        {
            ResRef = c.ResRef,
            Name = c.DisplayName,
            BaseItemName = c.BaseItemTypeName,
            BaseItem = c.BaseItemType,
            Value = c.BaseValue,
            Tag = string.IsNullOrEmpty(c.Tag) ? c.ResRef : c.Tag,
            PropertiesDisplay = c.PropertiesDisplay,
            Source = c.IsStandard ? GameResourceSource.Bif : GameResourceSource.Override
        }).ToList();

        inv.SetPaletteItems(vms);
        UpdateStatus($"Item palette: {vms.Count:N0} items.");
    }

    // --- Add / Remove (panel events → undoable commands) ---

    private void OnInventoryAddRequested(object? sender, ItemViewModel paletteItem)
    {
        if (_placeable == null || sender is not InventoryPanel inv) return;

        // Add the resolved item so the backpack row shows full data, falling back to the cache row.
        var resolved = ResolveForDetails(paletteItem) ?? paletteItem;
        _undo.Execute(new AddInventoryItemCommand(_placeable.Utp.ItemList, inv.BackpackItems, resolved));
        inv.OnBackpackChanged();
    }

    private void OnInventoryRemoveRequested(object? sender, ItemViewModel backpackItem)
    {
        if (_placeable == null || sender is not InventoryPanel inv) return;

        _undo.Execute(new RemoveInventoryItemCommand(_placeable.Utp.ItemList, inv.BackpackItems, backpackItem));
        inv.OnBackpackChanged();
    }
}
