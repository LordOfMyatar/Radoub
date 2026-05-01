using Avalonia.Interactivity;
using Radoub.Formats.Logging;
using Radoub.Formats.Common;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Radoub.Formats.Uti;
using Radoub.UI.Services;
using Radoub.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Views;

/// <summary>
/// MainWindow partial class for item palette population.
/// Uses shared cross-tool palette cache - each source (BIF, Override, HAK) has independent cache.
/// Module folder items are scanned fresh (already unpacked files).
/// </summary>
public partial class MainWindow
{
    // Shared cross-tool cache service with per-source granularity
    private readonly ISharedPaletteCacheService _sharedCacheService = new SharedPaletteCacheService();

    // HAK scanner for loading items from module-referenced HAK files
    private readonly HakPaletteScannerService _hakScanner = new();

    // Cancellation token for background cache building
    private CancellationTokenSource? _paletteCacheCts;

    // Track loaded state
    private bool _cachePreWarmed;
    private bool _paletteLoaded;

    // Active HAK paths from current module's IFO (for filtered aggregation)
    private List<string>? _activeHakPaths;

    // Track module directory for clearing stale module items
    private string? _lastModuleDirectory;

    /// <summary>
    /// Initialize palette loading - pre-warm cache in background but don't populate UI.
    /// Called on app startup.
    /// </summary>
    public void StartGameItemsLoad(CancellationToken windowToken)
    {
        if (!GameData.IsConfigured)
        {
            UnifiedLogger.LogInventory(LogLevel.WARN, "Item palette unavailable - GameDataService not configured");
            return;
        }

        // Link to window cancellation token
        _paletteCacheCts?.Cancel();
        _paletteCacheCts = CancellationTokenSource.CreateLinkedTokenSource(windowToken);

        // Pre-warm cache in background (non-blocking)
        _ = PreWarmCacheAsync(_paletteCacheCts.Token);
    }

    /// <summary>
    /// Pre-warm the cache in background so it's ready when user navigates to Inventory.
    /// Uses shared cache - only rebuilds sources that need updating.
    /// </summary>
    private async Task PreWarmCacheAsync(CancellationToken token)
    {
        try
        {
            token.ThrowIfCancellationRequested();

            // Resolve active HAK paths for module-aware filtering
            _activeHakPaths = ResolveActiveHakPaths();

            // Try to load aggregated cache from existing source caches (filtered to active HAKs)
            var existing = await Task.Run(() => _sharedCacheService.GetAggregatedCache(_activeHakPaths), token);

            if (existing != null && existing.Count > 0)
            {
                _cachePreWarmed = true;
                UnifiedLogger.LogInventory(LogLevel.INFO, $"Cache pre-warmed from existing sources: {existing.Count} items");

                // Check if any sources need rebuilding
                _ = RebuildStaleCachesAsync(token);
                return;
            }

            token.ThrowIfCancellationRequested();

            // No valid caches - build all from scratch
            UnifiedLogger.LogInventory(LogLevel.INFO, "Building palette caches in background...");
            await BuildAllCachesAsync(token);
            _cachePreWarmed = true;
        }
        catch (OperationCanceledException)
        {
            UnifiedLogger.LogInventory(LogLevel.DEBUG, "Cache pre-warm cancelled");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogInventory(LogLevel.WARN, $"Cache pre-warm failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Rebuild only the source caches that are stale or missing.
    /// Runs in background without blocking UI.
    /// </summary>
    private async Task RebuildStaleCachesAsync(CancellationToken token)
    {
        var sourcesToRebuild = new List<string>();

        // Check which sources need rebuilding
        if (!_sharedCacheService.HasValidSourceCache("bif"))
            sourcesToRebuild.Add("bif");

        if (!_sharedCacheService.HasValidSourceCache("override"))
            sourcesToRebuild.Add("override");

        // HAK caches are checked separately via the scanner
        bool needsHakScan = HasModuleWithHaks();

        if (sourcesToRebuild.Count == 0 && !needsHakScan)
        {
            UnifiedLogger.LogInventory(LogLevel.DEBUG, "All source caches are valid");
            return;
        }

        UnifiedLogger.LogInventory(LogLevel.INFO, $"Rebuilding {sourcesToRebuild.Count} stale cache(s): {string.Join(", ", sourcesToRebuild)}");

        try
        {
            if (sourcesToRebuild.Count > 0)
            {
                await Task.Run(async () =>
                {
                    foreach (var source in sourcesToRebuild)
                    {
                        token.ThrowIfCancellationRequested();
                        var gameSource = source == "bif" ? GameResourceSource.Bif : GameResourceSource.Override;
                        await BuildSourceCacheAsync(gameSource, source, token);
                    }
                }, token);
            }

            // Scan module HAKs (checks validity internally, skips cached)
            if (needsHakScan)
            {
                await ScanModuleHaksAsync(token);
            }

            // Invalidate aggregated cache so next read rebuilds from refreshed sources
            if (!token.IsCancellationRequested)
            {
                _sharedCacheService.InvalidateAggregatedCache();
            }
        }
        catch (OperationCanceledException)
        {
            UnifiedLogger.LogInventory(LogLevel.DEBUG, "Cache rebuild cancelled");
        }
    }

    /// <summary>
    /// Build all source caches from scratch.
    /// </summary>
    private async Task BuildAllCachesAsync(CancellationToken token)
    {
        try
        {
            await Task.Run(async () =>
            {
                // Build each source — check if another process is building first (#1633)
                foreach (var (gameSource, cacheSource) in new[] {
                    (GameResourceSource.Bif, "bif"),
                    (GameResourceSource.Override, "override") })
                {
                    token.ThrowIfCancellationRequested();
                    await BuildSourceCacheAsync(gameSource, cacheSource, token);
                }
            }, token);

            // Scan module HAKs (outside Task.Run — scanner manages its own threading)
            await ScanModuleHaksAsync(token);

            // Log aggregated result (filtered to active HAKs)
            var aggregated = _sharedCacheService.GetAggregatedCache(_activeHakPaths);
            UnifiedLogger.LogInventory(LogLevel.INFO, $"All caches built: {aggregated?.Count ?? 0} total items");
        }
        catch (OperationCanceledException)
        {
            UnifiedLogger.LogInventory(LogLevel.DEBUG, "Cache build cancelled");
        }
    }

    /// <summary>
    /// Build cache for a specific source type.
    /// </summary>
    private async Task BuildSourceCacheAsync(GameResourceSource gameSource, string cacheSource, CancellationToken token)
    {
        // Skip if valid cache already exists
        if (_sharedCacheService.HasValidSourceCache(cacheSource))
        {
            UnifiedLogger.LogInventory(LogLevel.DEBUG, $"{cacheSource} cache already valid, skipping build");
            return;
        }

        // Acquire build lock — skip if another process is building (#1633)
        if (!_sharedCacheService.AcquireBuildLock(cacheSource))
        {
            UnifiedLogger.LogInventory(LogLevel.DEBUG,
                $"{cacheSource} build lock held by another process, waiting...");
            await _sharedCacheService.WaitForBuildLock(cacheSource, timeout: 60000, cancellationToken: token);
            return;
        }

        try
        {
        var cacheItems = new List<SharedPaletteCacheItem>();
        var existingResRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var gameResources = GameData.ListResources(ResourceTypes.Uti)
            .Where(r => r.Source == gameSource)
            .ToList();

        UnifiedLogger.LogInventory(LogLevel.INFO, $"Building {cacheSource} cache from {gameResources.Count} UTI resources...");

        foreach (var resourceInfo in gameResources)
        {
            if (token.IsCancellationRequested)
                break;

            if (existingResRefs.Contains(resourceInfo.ResRef))
                continue;

            try
            {
                var utiData = GameData.FindResource(resourceInfo.ResRef, ResourceTypes.Uti);
                if (utiData != null)
                {
                    var item = UtiReader.Read(utiData);
                    var displayName = ItemFactory.GetItemDisplayName(item);
                    var baseItemTypeName = ItemFactory.GetBaseItemTypeName(item.BaseItem);
                    var propertiesDisplay = ItemFactory.GetPropertiesDisplay(item.Properties);

                    cacheItems.Add(new SharedPaletteCacheItem
                    {
                        ResRef = resourceInfo.ResRef,
                        Tag = item.Tag ?? string.Empty,
                        DisplayName = displayName,
                        BaseItemTypeName = baseItemTypeName,
                        PropertiesDisplay = propertiesDisplay,
                        BaseItemType = item.BaseItem,
                        BaseValue = item.Cost,
                        IsStandard = gameSource == GameResourceSource.Bif,
                        SourceLocation = !string.IsNullOrEmpty(resourceInfo.SourcePath)
                            ? Path.GetFileName(resourceInfo.SourcePath)
                            : resourceInfo.Source.ToString()
                    });
                    existingResRefs.Add(resourceInfo.ResRef);
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogInventory(LogLevel.DEBUG, $"Failed to cache item {resourceInfo.ResRef}: {ex.Message}");
            }
        }

        if (!token.IsCancellationRequested && cacheItems.Count > 0)
        {
            // Determine validation path based on source type
            var settings = RadoubSettings.Instance;
            var validationPath = cacheSource == "bif"
                ? settings.BaseGameInstallPath
                : settings.NeverwinterNightsPath;

            await _sharedCacheService.SaveSourceCacheAsync(cacheSource, cacheItems, validationPath);
            UnifiedLogger.LogInventory(LogLevel.INFO, $"{cacheSource} cache complete: {cacheItems.Count} items");
        }
        }
        finally
        {
            _sharedCacheService.ReleaseBuildLock(cacheSource);
        }
    }

    /// <summary>
    /// Load palette items into the UI. Called when user navigates to Inventory panel.
    /// Uses cached data if available, otherwise waits for cache to be ready.
    /// </summary>
    public async Task LoadPaletteItemsAsync(CancellationToken token)
    {
        if (_paletteLoaded)
            return;

        UpdateStatus("Loading item palette...");

        try
        {
            // Wait for cache if not pre-warmed yet
            if (!_cachePreWarmed)
            {
                await PreWarmCacheAsync(token);
            }

            token.ThrowIfCancellationRequested();

            // Snapshot the aggregated cache once for this load operation
            var paletteData = _sharedCacheService.GetAggregatedCache(_activeHakPaths);

            if (paletteData == null || paletteData.Count == 0)
            {
                UpdateStatus("Ready (no items available)");
                _paletteLoaded = true;
                return;
            }

            // Load all items at once (no batching - faster)
            var standardItems = paletteData.Where(i => i.IsStandard).ToList();
            var customItems = paletteData.Where(i => !i.IsStandard).ToList();

            UnifiedLogger.LogInventory(LogLevel.INFO, $"Loading {standardItems.Count} standard + {customItems.Count} custom items");

            // Create all view models on background thread
            var viewModels = await Task.Run(() =>
            {
                var vms = new List<ItemViewModel>(paletteData.Count);
                foreach (var cached in paletteData)
                {
                    token.ThrowIfCancellationRequested();
                    var vm = new ItemViewModel
                    {
                        ResRef = cached.ResRef,
                        Name = cached.DisplayName,
                        BaseItemName = cached.BaseItemTypeName,
                        BaseItem = cached.BaseItemType,
                        Value = cached.BaseValue,
                        Tag = !string.IsNullOrEmpty(cached.Tag) ? cached.Tag : cached.ResRef,
                        PropertiesDisplay = cached.PropertiesDisplay,
                        Source = cached.IsStandard ? GameResourceSource.Bif : GameResourceSource.Override
                    };
                    ItemFactory.PopulateEquipableSlots(vm, cached.BaseItemType);
                    vms.Add(vm);
                }
                return vms;
            }, token);

            // Collect module directory items (loose UTIs) before SetPaletteItems clears them
            var moduleDir = string.IsNullOrEmpty(_currentFilePath)
                ? null
                : Path.GetDirectoryName(_currentFilePath);
            var moduleVms = LoadModuleItemViewModels(moduleDir);
            if (moduleVms.Count > 0)
            {
                // Add module items to the batch so they're included in a single SetPaletteItems call
                var existingResRefs = new HashSet<string>(
                    viewModels.Select(v => v.ResRef), StringComparer.OrdinalIgnoreCase);
                foreach (var mvm in moduleVms)
                {
                    if (!existingResRefs.Contains(mvm.ResRef))
                        viewModels.Add(mvm);
                }
                _lastModuleDirectory = moduleDir;
            }

            // Set all items at once (efficient - avoids per-item filter updates)
            InventoryPanelContent.SetPaletteItems(viewModels);

            _paletteLoaded = true;
            var totalItems = InventoryPanelContent.PaletteItems.Count;
            UpdateStatus($"Ready - {totalItems:N0} items loaded");
            UnifiedLogger.LogInventory(LogLevel.INFO, $"Palette loaded: {viewModels.Count} items (total: {totalItems})");
        }
        catch (OperationCanceledException)
        {
            UnifiedLogger.LogInventory(LogLevel.DEBUG, "Palette load cancelled");
            UpdateStatus("Ready");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogInventory(LogLevel.ERROR, $"Failed to load palette: {ex.Message}");
            UpdateStatus("Ready (palette load failed)");
            _paletteLoaded = true;
        }
    }

    /// <summary>
    /// Load module directory UTI files into ItemViewModels without adding to palette.
    /// Used during initial palette build to include module items in the batch.
    /// </summary>
    private List<ItemViewModel> LoadModuleItemViewModels(string? moduleDir)
    {
        var results = new List<ItemViewModel>();
        if (string.IsNullOrEmpty(moduleDir) || !Directory.Exists(moduleDir))
            return results;

        var utiFiles = Directory.GetFiles(moduleDir, "*.uti", SearchOption.TopDirectoryOnly);
        foreach (var utiPath in utiFiles)
        {
            try
            {
                var item = UtiReader.Read(utiPath);
                var viewModel = ItemFactory.Create(item, GameResourceSource.Module);
                SetupLazyIconLoading(viewModel);
                results.Add(viewModel);
            }
            catch (Exception ex)
            {
                var fileName = Path.GetFileName(utiPath);
                UnifiedLogger.LogInventory(LogLevel.WARN, $"Failed to load UTI {fileName}: {ex.Message}");
            }
        }

        UnifiedLogger.LogInventory(LogLevel.INFO, $"Loaded {results.Count} module UTIs from {UnifiedLogger.SanitizePath(moduleDir)}");
        return results;
    }

    /// <summary>
    /// Populates the item palette from module directory (loose UTI files).
    /// Called when a creature file is loaded. Module items are NOT cached.
    /// </summary>
    private void PopulateItemPalette()
    {
        var newModuleDir = string.IsNullOrEmpty(_currentFilePath)
            ? null
            : Path.GetDirectoryName(_currentFilePath);

        // Same directory and module items already present — skip rescan
        if (_lastModuleDirectory == newModuleDir &&
            InventoryPanelContent.PaletteItems.Any(p => p.Source == GameResourceSource.Module))
        {
            UnifiedLogger.LogInventory(LogLevel.DEBUG,
                "PopulateItemPalette: module items already loaded, skipping rescan");
            return;
        }

        UnifiedLogger.LogInventory(LogLevel.INFO,
            $"PopulateItemPalette: scanning directory '{(newModuleDir != null ? UnifiedLogger.SanitizePath(newModuleDir) : "(null)")}' for module UTIs");

        // Clear old module items if directory changed
        if (_lastModuleDirectory != newModuleDir)
        {
            ClearModuleItems();
            _lastModuleDirectory = newModuleDir;
        }

        // Load module items and add in batch (disconnect filter to avoid per-item updates)
        var moduleVms = LoadModuleItemViewModels(newModuleDir);
        if (moduleVms.Count > 0)
        {
            var existingResRefs = new HashSet<string>(
                InventoryPanelContent.PaletteItems.Select(p => p.ResRef),
                StringComparer.OrdinalIgnoreCase);

            var toAdd = moduleVms.Where(vm => !existingResRefs.Contains(vm.ResRef)).ToList();
            if (toAdd.Count > 0)
            {
                InventoryPanelContent.AddPaletteItems(toAdd);
                UnifiedLogger.LogInventory(LogLevel.INFO, $"Added {toAdd.Count} module items to palette");
            }
        }
    }

    /// <summary>
    /// Clear module items from the palette (when switching to different module folder).
    /// </summary>
    private void ClearModuleItems()
    {
        var moduleItems = InventoryPanelContent.PaletteItems
            .Where(i => i.Source == GameResourceSource.Module)
            .ToList();

        foreach (var item in moduleItems)
        {
            InventoryPanelContent.PaletteItems.Remove(item);
        }

        if (moduleItems.Count > 0)
        {
            UnifiedLogger.LogInventory(LogLevel.DEBUG, $"Cleared {moduleItems.Count} module items from palette");
        }
    }

    /// <summary>
    /// Clear and reload all palette caches. Called from Settings.
    /// </summary>
    public async Task ClearAndReloadPaletteCacheAsync()
    {
        // Create new CTS for this operation (links to window CTS if available)
        _paletteCacheCts?.Cancel();
        _paletteCacheCts = _windowCts != null
            ? CancellationTokenSource.CreateLinkedTokenSource(_windowCts.Token)
            : new CancellationTokenSource();
        var token = _paletteCacheCts.Token;

        _sharedCacheService.ClearAllCaches();
        _cachePreWarmed = false;
        _paletteLoaded = false;
        _lastModuleDirectory = null;
        _activeHakPaths = null;
        InventoryPanelContent.PaletteItems.Clear();

        // Re-resolve active HAKs and rebuild cache
        _activeHakPaths = ResolveActiveHakPaths();
        await BuildAllCachesAsync(token);
        await LoadPaletteItemsAsync(token);
    }

    /// <summary>
    /// Resolve the active HAK file paths from the current module's IFO.
    /// Returns null if no module is configured (includes all HAKs in aggregation).
    /// Returns empty list if module has no HAKs (excludes all HAKs from aggregation).
    /// </summary>
    private List<string>? ResolveActiveHakPaths()
    {
        var moduleDir = GetModuleWorkingDirectory();
        if (string.IsNullOrEmpty(moduleDir))
            return null; // No module — include all cached HAKs

        var hakSearchPaths = RadoubSettings.Instance.GetAllHakSearchPaths();
        return _hakScanner.ResolveModuleHakPaths(moduleDir, hakSearchPaths);
    }

    /// <summary>
    /// Scan module HAK files and cache items. Skips HAKs with valid caches.
    /// After scanning, refreshes the aggregated cache data to include HAK items.
    /// </summary>
    private async Task ScanModuleHaksAsync(CancellationToken token)
    {
        var moduleDir = GetModuleWorkingDirectory();
        if (string.IsNullOrEmpty(moduleDir))
            return;

        var hakSearchPaths = RadoubSettings.Instance.GetAllHakSearchPaths();
        var result = await _hakScanner.ScanAndCacheModuleHaksAsync(
            moduleDir, hakSearchPaths, _sharedCacheService, token);

        if (result.HaksScanned > 0)
        {
            UnifiedLogger.LogInventory(LogLevel.INFO,
                $"HAK scan: {result.HaksScanned} scanned, {result.HaksSkipped} cached, {result.TotalItemsScanned} items");

            // Invalidate aggregated cache so next read includes HAK items
            _sharedCacheService.InvalidateAggregatedCache();
        }
    }

    /// <summary>
    /// Check if the current module has HAK files referenced in its IFO.
    /// </summary>
    private bool HasModuleWithHaks()
    {
        var moduleDir = GetModuleWorkingDirectory();
        if (string.IsNullOrEmpty(moduleDir))
            return false;

        var ifoPath = Path.Combine(moduleDir, "module.ifo");
        if (!File.Exists(ifoPath))
            return false;

        try
        {
            var ifo = Radoub.Formats.Ifo.IfoReader.Read(ifoPath);
            return ifo.HakList.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get the module working directory (unpacked module folder).
    /// Resolves .mod file paths to their unpacked directories.
    /// </summary>
    private static string? GetModuleWorkingDirectory()
    {
        var modulePath = RadoubSettings.Instance.CurrentModulePath;
        if (!RadoubSettings.IsValidModulePath(modulePath))
            return null;

        // If it's a .mod file, look for the unpacked directory alongside it
        if (File.Exists(modulePath) && modulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
        {
            var moduleName = Path.GetFileNameWithoutExtension(modulePath);
            var moduleDir = Path.GetDirectoryName(modulePath);
            if (!string.IsNullOrEmpty(moduleDir))
            {
                var candidate = Path.Combine(moduleDir, moduleName);
                if (Directory.Exists(candidate))
                    return candidate;
            }
        }

        // It's already a directory path
        if (Directory.Exists(modulePath))
            return modulePath;

        return null;
    }

    /// <summary>
    /// Get cache statistics for display in Settings.
    /// </summary>
    public SharedPaletteCacheStatistics GetPaletteCacheStatistics() => _sharedCacheService.GetCacheStatistics();

    /// <summary>
    /// Handles the Refresh Item Palette menu item. Clears and reloads all palette caches.
    /// </summary>
    private async void OnRefreshItemPaletteClick(object? sender, RoutedEventArgs e)
    {
        UpdateStatus("Refreshing item palette...");
        ShowProgress(true);
        try
        {
            await ClearAndReloadPaletteCacheAsync();
        }
        finally
        {
            ShowProgress(false);
        }
    }

    /// <summary>
    /// Sets up lazy icon loading for an item ViewModel.
    /// </summary>
    private void SetupLazyIconLoading(ItemViewModel itemVm)
    {
        if (!IconService.IsGameDataAvailable)
            return;

        itemVm.SetIconLoader(item => IconService.GetItemIcon(item));
    }
}
