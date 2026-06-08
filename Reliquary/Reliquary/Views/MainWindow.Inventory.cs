using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Radoub.Formats.Uti;
using Radoub.UI.Services;
using Radoub.UI.Services.Search;
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
        inv.EditItemRequested += OnInventoryEditRequested;
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

        if (_gameData != null) inv.SetGameDataService(_gameData);
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
        {
            // BIF UTIs often carry an empty TemplateResRef (the ResRef lives in the BIF index, not
            // the file). Stamp it so the VM's ResRef — and the model InventoryRes — is correct.
            if (string.IsNullOrEmpty(uti.TemplateResRef)) uti.TemplateResRef = resRef;
            var vm = _itemFactory.Create(uti, source);
            vm.IconBitmap = _itemIconService?.GetItemIcon(uti); // #2411 detail icons
            return vm;
        }

        // Unresolved UTI — show a placeholder so the row is still visible/removable.
        return new ItemViewModel
        {
            Name = resRef,
            ResRef = resRef,
            BaseItemName = "(unresolved)",
            Source = source
        };
    }

    /// <summary>Resolve a palette row to full data for the read-only details pane / backpack add.</summary>
    private ItemViewModel? ResolveForDetails(ItemViewModel cacheItem)
    {
        if (cacheItem.Item != null) return cacheItem; // already fully loaded
        var (uti, source) = ResolveUtiFile(cacheItem.ResRef);
        if (uti == null || _itemFactory == null) return cacheItem;
        if (string.IsNullOrEmpty(uti.TemplateResRef)) uti.TemplateResRef = cacheItem.ResRef;
        var vm = _itemFactory.Create(uti, source);
        vm.IconBitmap = _itemIconService?.GetItemIcon(uti); // #2411 detail icons
        return vm;
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
    private async void EnsurePaletteLoaded(InventoryPanel inv)
    {
        if (_paletteLoaded) return;
        _paletteLoaded = true;

        UpdateStatus("Loading item palette...");
        try
        {
            // Build any missing source caches (BIF/Override/HAK) in the background, then read
            // the aggregated UTI cache (shared with QM/Relique at ~/Radoub/Cache/ItemPalette).
            await BuildItemCacheAsync();

            var cache = _itemCache.GetAggregatedCache() ?? new List<SharedPaletteCacheItem>();
            var vms = cache
                // Hide creature natural weapons + the invalid marker — they are not authorable
                // blueprints (Fence/QM parity, #2411). Shared list keeps the tools in sync.
                .Where(c => !Radoub.UI.Utils.ItemPaletteExclusions.IsExcluded(c.BaseItemType))
                .Select(c => new ItemViewModel
                {
                    ResRef = c.ResRef,
                    Name = c.DisplayName,
                    BaseItemName = c.BaseItemTypeName,
                    BaseItem = c.BaseItemType,
                    Value = c.BaseValue,
                    Tag = string.IsNullOrEmpty(c.Tag) ? c.ResRef : c.Tag,
                    PropertiesDisplay = c.PropertiesDisplay,
                    // IsStandard is a bool, so a custom item is HAK or Override; SourceLocation carries
                    // the real origin (hak filename vs "Override"). Use it to label HAK items as Hak
                    // instead of lumping them under Override (#2411 follow-up).
                    Source = SourceFromCache(c),
                    SourceLocation = c.SourceLocation, // surface the hak filename (Fence parity)
                    // Item detail images (#2411): reuse Reliquary's ItemIconService (already used for
                    // placeable portraits) for inventory palette/details icons, matching Fence.
                    IconBitmap = _itemIconService?.GetItemIcon(c.BaseItemType)
                }).ToList();

            // Add loose module-directory UTIs (not in BIF/Override/HAK) so module items show too.
            var moduleResRefs = new HashSet<string>(vms.Select(v => v.ResRef), StringComparer.OrdinalIgnoreCase);
            foreach (var moduleVm in LoadModuleItemViewModels())
                if (!Radoub.UI.Utils.ItemPaletteExclusions.IsExcluded(moduleVm.BaseItem) && moduleResRefs.Add(moduleVm.ResRef))
                    vms.Add(moduleVm);

            inv.SetPaletteItems(vms);
            UpdateStatus(vms.Count > 0
                ? $"Item palette: {vms.Count:N0} items."
                : "Item palette empty — configure game paths in Settings to populate.");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Reliquary: item palette load failed: {ex.Message}");
            UpdateStatus("Item palette load failed — see log.");
        }
    }

    /// <summary>
    /// Build missing UTI source caches (BIF, Override, module HAKs) so the aggregated cache is
    /// populated on first inventory use. Source caches are shared and skipped when already valid,
    /// so this is cheap on subsequent runs / after QM/Relique already built them.
    /// </summary>
    private async Task BuildItemCacheAsync()
    {
        if (_gameData is not { IsConfigured: true } || _itemFactory == null) return;

        await Task.Run(() =>
        {
            foreach (var (gameSource, cacheSource) in new[]
            {
                (GameResourceSource.Bif, "bif"),
                (GameResourceSource.Override, "override")
            })
            {
                if (_itemCache.HasValidSourceCache(cacheSource)) continue;
                if (!_itemCache.AcquireBuildLock(cacheSource)) continue; // another tool is building
                try { BuildSourceCache(gameSource, cacheSource); }
                finally { _itemCache.ReleaseBuildLock(cacheSource); }
            }
        });

        // Module HAKs (shared scanner manages its own threading + per-HAK cache validity).
        var moduleDir = GetModuleWorkingDirectory();
        if (!string.IsNullOrEmpty(moduleDir))
        {
            var hakSearchPaths = RadoubSettings.Instance.GetAllHakSearchPaths();
            await new HakPaletteScannerService()
                .ScanAndCacheModuleHaksAsync(moduleDir, hakSearchPaths, _itemCache, default);
        }

        _itemCache.InvalidateAggregatedCache(); // force a rebuild from the refreshed sources
    }

    /// <summary>Build one source cache (BIF or Override) from the game data resource list.</summary>
    private void BuildSourceCache(GameResourceSource gameSource, string cacheSource)
    {
        if (_gameData == null || _itemFactory == null) return;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<SharedPaletteCacheItem>();

        foreach (var res in _gameData.ListResources(ResourceTypes.Uti).Where(r => r.Source == gameSource))
        {
            if (!seen.Add(res.ResRef)) continue;
            try
            {
                var bytes = _gameData.FindResource(res.ResRef, ResourceTypes.Uti);
                if (bytes == null) continue;
                var uti = UtiReader.Read(bytes);
                // Keep the shared cache clean of non-authorable items (Fence parity, #2411).
                if (Radoub.UI.Utils.ItemPaletteExclusions.IsExcluded(uti.BaseItem)) continue;
                items.Add(new SharedPaletteCacheItem
                {
                    ResRef = res.ResRef,
                    Tag = uti.Tag ?? string.Empty,
                    DisplayName = _itemFactory.GetItemDisplayName(uti),
                    BaseItemTypeName = _itemFactory.GetBaseItemTypeName(uti.BaseItem),
                    PropertiesDisplay = _itemFactory.GetPropertiesDisplay(uti.Properties),
                    BaseItemType = uti.BaseItem,
                    BaseValue = uti.Cost,
                    IsStandard = gameSource == GameResourceSource.Bif,
                    SourceLocation = string.IsNullOrEmpty(res.SourcePath)
                        ? res.Source.ToString() : Path.GetFileName(res.SourcePath)
                });
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Reliquary: skip UTI {res.ResRef}: {ex.Message}");
            }
        }

        if (items.Count == 0) return;
        var validationPath = cacheSource == "bif"
            ? RadoubSettings.Instance.BaseGameInstallPath
            : RadoubSettings.Instance.NeverwinterNightsPath;
        _itemCache.SaveSourceCacheAsync(cacheSource, items, validationPath).GetAwaiter().GetResult();
        UnifiedLogger.LogApplication(LogLevel.INFO, $"Reliquary: built {cacheSource} item cache ({items.Count} items).");
    }

    /// <summary>
    /// Map a cached palette item to a source enum. BIF items are standard; custom items are HAK when
    /// SourceLocation names a .hak file, otherwise Override. (SharedPaletteCacheItem.IsStandard is a
    /// bool, so SourceLocation is the only signal that distinguishes HAK from Override — #2411 follow-up.)
    /// </summary>
    private static GameResourceSource SourceFromCache(SharedPaletteCacheItem c)
    {
        if (c.IsStandard) return GameResourceSource.Bif;
        return !string.IsNullOrEmpty(c.SourceLocation)
               && c.SourceLocation.EndsWith(".hak", StringComparison.OrdinalIgnoreCase)
            ? GameResourceSource.Hak
            : GameResourceSource.Override;
    }

    /// <summary>Load loose .uti files from the open placeable's directory as palette items.</summary>
    private List<ItemViewModel> LoadModuleItemViewModels()
    {
        var vms = new List<ItemViewModel>();
        if (string.IsNullOrEmpty(_currentFilePath) || _itemFactory == null) return vms;

        var dir = Path.GetDirectoryName(_currentFilePath);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return vms;

        foreach (var file in Directory.GetFiles(dir, "*.uti", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var vm = _itemFactory.Create(UtiReader.Read(file), GameResourceSource.Module);
                vm.IconBitmap = _itemIconService?.GetItemIcon(vm.BaseItem); // #2411 detail icons
                vms.Add(vm);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Reliquary: skip module UTI {Path.GetFileName(file)}: {ex.Message}");
            }
        }
        return vms;
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

    /// <summary>
    /// Context "Edit" on a palette/backpack item → resolve its .uti to a file and open it in Relique
    /// via the shared ToolDispatchService (same pattern as Conversation → Parley). BIF/HAK items have
    /// no editable file path, so only loose module/Override UTIs can be edited.
    /// </summary>
    private void OnInventoryEditRequested(object? sender, ItemViewModel item)
    {
        if (string.IsNullOrWhiteSpace(item.ResRef))
        {
            UpdateStatus("No item selected to edit.");
            return;
        }

        var fileDir = string.IsNullOrEmpty(_currentFilePath) ? null : Path.GetDirectoryName(_currentFilePath);
        var moduleDir = GetModuleWorkingDirectory();
        var utiPath = ExternalEditorService.ResolveResourcePath(item.ResRef, ".uti", fileDir, moduleDir);
        if (utiPath is null)
        {
            UpdateStatus($"Can't edit {item.ResRef}.uti in Relique — it lives in a HAK/BIF, not a module file.");
            return;
        }

        if (!new ToolDispatchService().LaunchTool(ResourceTypes.Uti, utiPath))
            UpdateStatus($"Could not launch Relique for {item.ResRef}.uti — Relique may not be installed alongside Reliquary.");
        else
            UpdateStatus($"Opening {item.ResRef}.uti in Relique…");
    }
}
