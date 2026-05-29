using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Logging;
using Radoub.Formats.Resolver;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Radoub.UI.Services;

namespace Radoub.UI.Controls;

/// <summary>
/// Store-specific entry with HAK and BIF support.
/// </summary>
public class StoreBrowserEntry : FileBrowserEntry
{
    /// <summary>
    /// True if this resource is from a base game BIF file.
    /// </summary>
    public bool IsFromBif { get; set; }

    /// <summary>
    /// Display name with source indicator for archive entries.
    /// </summary>
    public override string DisplayName => IsFromBif ? $"{Name} ({Source})" : base.DisplayName;
}

/// <summary>
/// Cached HAK file store data to avoid re-scanning on each refresh.
/// </summary>
internal class StoreHakCacheEntry
{
    public string HakPath { get; set; } = "";
    public DateTime LastModified { get; set; }
    public List<StoreBrowserEntry> Stores { get; set; } = new();
}

/// <summary>
/// Store browser panel for embedding in Fence's main window.
/// Provides file list with optional HAK scanning.
/// </summary>
public class StoreBrowserPanel : FileBrowserPanelBase, IBrowserRowRefresher
{
    private readonly IScriptBrowserContext? _context;
    private readonly CheckBox _showModuleCheckBox;
    private readonly CheckBox _showHakCheckBox;
    private readonly CheckBox _showBifCheckBox;
    private bool _showHakStores;
    private bool _hakStoresLoaded;
    private bool _showBifStores;
    private bool _bifStoresLoaded;
    private List<StoreBrowserEntry> _hakStores = new();
    private List<StoreBrowserEntry> _bifStores = new();

    // Static cache for HAK file contents - persists across panel instances.
    // ConcurrentDictionary so concurrent panel instances can safely race on
    // Task.Run scans (#2262).
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, StoreHakCacheEntry> _hakCache = new();

    public StoreBrowserPanel() : this(null)
    {
    }

    public StoreBrowserPanel(IScriptBrowserContext? context)
    {
        _context = context;

        FileExtension = ".utm";
        SearchWatermark = "Type to filter stores...";
        HeaderTextContent = "Stores";

        // Create Module checkbox (checked by default)
        _showModuleCheckBox = new CheckBox
        {
            Content = "Module",
            IsChecked = true,
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
        };
        ToolTip.SetTip(_showModuleCheckBox, "Show .utm files from module folder");
        _showModuleCheckBox.IsCheckedChanged += OnModuleFilterChanged;

        // Create and wire up HAK checkbox
        _showHakCheckBox = new CheckBox
        {
            Content = "Show HAK",
            IsChecked = false,
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
        };
        ToolTip.SetTip(_showHakCheckBox, "Include stores from HAK files");
        _showHakCheckBox.IsCheckedChanged += OnShowHakChanged;

        // Create and wire up BIF checkbox
        _showBifCheckBox = new CheckBox
        {
            Content = "Base Game",
            IsChecked = false,
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
        };
        ToolTip.SetTip(_showBifCheckBox, "Show store blueprints from base game BIF archives");
        _showBifCheckBox.IsCheckedChanged += OnShowBifChanged;

        var filterPanel = new StackPanel { Spacing = 2 };
        filterPanel.Children.Add(_showModuleCheckBox);
        filterPanel.Children.Add(_showHakCheckBox);
        filterPanel.Children.Add(_showBifCheckBox);
        FilterOptionsContent = filterPanel;
    }

    protected override bool SupportsCopyToModule() => true;

    protected override bool IsArchiveEntry(FileBrowserEntry entry)
        => entry is StoreBrowserEntry s && (s.IsFromHak || s.IsFromBif);

    protected override Task<byte[]?> ExtractArchiveBytesAsync(FileBrowserEntry entry)
    {
        if (entry is not StoreBrowserEntry storeEntry) return Task.FromResult<byte[]?>(null);

        if (storeEntry.IsFromBif && GameDataService is { IsConfigured: true })
        {
            return Task.FromResult(GameDataService.FindResource(storeEntry.Name, ResourceTypes.Utm));
        }
        if (storeEntry.IsFromHak && !string.IsNullOrEmpty(storeEntry.HakPath))
        {
            return Task.FromResult(ExtractFromHak(storeEntry.HakPath, storeEntry.Name, ResourceTypes.Utm));
        }
        return Task.FromResult<byte[]?>(null);
    }

    protected override Task<(string tag, string name)> ReadSourceMetadataAsync(byte[] bytes)
    {
        try
        {
            var utm = Radoub.Formats.Utm.UtmReader.Read(bytes);
            return Task.FromResult((utm.Tag ?? string.Empty, utm.LocName.GetDefault() ?? string.Empty));
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"StoreBrowserPanel.ReadSourceMetadataAsync: {ex.Message}");
            return Task.FromResult((string.Empty, string.Empty));
        }
    }

    protected override Task<byte[]> ApplyCopyCustomizationsAsync(byte[] sourceBytes, CopyToModuleResult result)
        => Task.FromResult(ApplyUtmCopyCustomizations(sourceBytes, result));

    /// <summary>
    /// Rewrite a UTM byte blob with the user's new ResRef/Tag/Name. Pure, testable.
    /// </summary>
    internal static byte[] ApplyUtmCopyCustomizations(byte[] sourceBytes, CopyToModuleResult result)
    {
        var utm = Radoub.Formats.Utm.UtmReader.Read(sourceBytes);
        utm.ResRef = result.NewResRef;
        if (result.NewTag != null) utm.Tag = result.NewTag;
        if (result.NewName != null) utm.LocName.SetString(0, result.NewName);
        return Radoub.Formats.Utm.UtmWriter.Write(utm);
    }

    /// <summary>
    /// Optional shared UTM palette cache. When provided, BIF/HAK entries pull
    /// Tag/DisplayLabel from the cache (zero disk I/O) before falling back to
    /// GFF extraction. Module entries always read GFF directly.
    /// Callers should construct a <see cref="SharedPaletteCacheService"/>
    /// pointing at a UTM-specific subdirectory (e.g. ~/Radoub/Cache/StorePalette/)
    /// so it does not collide with the UTI cache.
    /// </summary>
    public ISharedPaletteCacheService? PaletteCache { get; set; }

    /// <summary>
    /// Background pass: populate Tag + DisplayLabel on every entry that does
    /// not yet have metadata. Yields every 50 entries to keep the UI thread
    /// responsive. Honors <paramref name="cancellationToken"/> between batches.
    /// </summary>
    protected override async Task IndexMetadataAsync(
        IReadOnlyList<FileBrowserEntry> entries,
        CancellationToken cancellationToken)
    {
        var paletteLookup = BuildPaletteLookup();
        int processed = 0;

        foreach (var entry in entries)
        {
            if (cancellationToken.IsCancellationRequested) return;
            if (entry.MetadataLoaded) { processed++; continue; }

            try
            {
                if (TryFillFromCache(entry, paletteLookup))
                {
                    entry.MetadataLoaded = true;
                }
                else
                {
                    var bytes = await ReadEntryBytesAsync(entry, cancellationToken);
                    if (bytes != null)
                    {
                        var (tag, name) = await ReadSourceMetadataAsync(bytes);
                        entry.Tag = tag;
                        entry.DisplayLabel = name;
                    }
                    entry.MetadataLoaded = true;
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"StoreBrowserPanel.IndexMetadataAsync({entry.Name}): {ex.Message}");
                entry.MetadataLoaded = true; // don't retry forever
            }

            processed++;
            if (processed % 50 == 0)
            {
                await Task.Yield();
            }
        }
    }

    /// <summary>
    /// Build a ResRef → cache-item lookup from the active palette cache. Returns
    /// an empty dictionary when no cache is wired or the cache is empty.
    /// </summary>
    private Dictionary<string, SharedPaletteCacheItem> BuildPaletteLookup()
    {
        if (PaletteCache == null) return new Dictionary<string, SharedPaletteCacheItem>();

        var items = PaletteCache.GetAggregatedCache();
        if (items == null || items.Count == 0)
            return new Dictionary<string, SharedPaletteCacheItem>();

        var dict = new Dictionary<string, SharedPaletteCacheItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            dict.TryAdd(item.ResRef, item);
        }
        return dict;
    }

    /// <summary>
    /// Pure-logic test seam: try to populate Tag + DisplayLabel from a cache
    /// lookup. Returns true on hit, false on miss. Lookup is keyed by ResRef
    /// (case-insensitive — caller responsible for using OrdinalIgnoreCase).
    /// </summary>
    internal static bool TryFillFromCache(
        FileBrowserEntry entry,
        Dictionary<string, SharedPaletteCacheItem> lookup)
    {
        if (lookup.Count == 0) return false;
        if (!lookup.TryGetValue(entry.Name, out var item)) return false;

        entry.Tag = item.Tag ?? string.Empty;
        entry.DisplayLabel = item.DisplayName ?? string.Empty;
        return true;
    }

    private async Task<byte[]?> ReadEntryBytesAsync(
        FileBrowserEntry entry,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(entry.FilePath) && File.Exists(entry.FilePath))
        {
            return await File.ReadAllBytesAsync(entry.FilePath, cancellationToken);
        }

        // HAK/BIF/archive entry — synchronous extraction wrapped in Task.Run
        return await Task.Run(() => ExtractStoreArchiveBytes(entry, GameDataService), cancellationToken);
    }

    /// <summary>
    /// Route an archive-sourced StoreBrowserEntry to the correct extraction path
    /// (BIF via GameDataService, HAK via shared ExtractFromHak helper). Returns
    /// null when the entry is not archive-sourced or required dependencies are missing.
    /// </summary>
    private static byte[]? ExtractStoreArchiveBytes(FileBrowserEntry entry, IGameDataService? gameDataService)
    {
        if (entry is not StoreBrowserEntry storeEntry) return null;

        if (storeEntry.IsFromBif)
        {
            if (gameDataService is { IsConfigured: true })
                return gameDataService.FindResource(storeEntry.Name, ResourceTypes.Utm);
            return null;
        }

        if (storeEntry.IsFromHak && !string.IsNullOrEmpty(storeEntry.HakPath))
            return ExtractFromHak(storeEntry.HakPath, storeEntry.Name, ResourceTypes.Utm);

        return null;
    }

    /// <summary>
    /// Re-read metadata for a single entry — called by the host tool after a
    /// save so the browser row reflects the new Tag/Name without a full reindex.
    /// </summary>
    public override async Task RefreshEntryMetadataAsync(FileBrowserEntry entry)
    {
        try
        {
            var bytes = await ReadEntryBytesAsync(entry, CancellationToken.None);
            if (bytes == null) return;
            var (tag, name) = await ReadSourceMetadataAsync(bytes);
            entry.Tag = tag;
            entry.DisplayLabel = name;
            entry.MetadataLoaded = true;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"StoreBrowserPanel.RefreshEntryMetadataAsync({entry.Name}): {ex.Message}");
        }
    }

    /// <summary>
    /// <see cref="IBrowserRowRefresher"/> implementation. Host tools should
    /// depend on the interface (via <see cref="BrowserSaveNotifier"/>) rather
    /// than calling the static method directly, so the post-save wire-up is
    /// testable with a fake refresher.
    /// </summary>
    public Task RefreshRowAsync(string filePath)
    {
        var entry = FindEntryByFilePath(filePath);
        return entry == null ? Task.CompletedTask : RefreshEntryFromDiskAsync(entry);
    }

    /// <summary>
    /// Pure-logic helper: parse a UTM byte blob into a <see cref="SharedPaletteCacheItem"/>
    /// for cache persistence. Returns null on parse failure so the populator can
    /// skip corrupt entries without aborting an entire HAK scan. Only ResRef, Tag,
    /// DisplayName, and SourceLocation are populated — UTM has no BaseItem fields.
    /// </summary>
    public static SharedPaletteCacheItem? BuildPaletteItemFromUtm(
        byte[] bytes,
        string resRef,
        string sourceLocation)
    {
        try
        {
            var utm = Radoub.Formats.Utm.UtmReader.Read(bytes);
            return new SharedPaletteCacheItem
            {
                ResRef = resRef,
                Tag = utm.Tag ?? string.Empty,
                DisplayName = utm.LocName.GetDefault() ?? string.Empty,
                SourceLocation = sourceLocation
            };
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"StoreBrowserPanel.BuildPaletteItemFromUtm({resRef}): {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Save-flow hook for module entries: re-read Tag + DisplayLabel from
    /// the on-disk UTM bytes. Pure-static so host tools (Fence) can call
    /// without holding a StoreBrowserPanel reference, and so the round-trip
    /// is unit-testable without Avalonia (#2200).
    ///
    /// No-op for entries without a FilePath (HAK/BIF rows are cache-driven).
    /// On read or parse failure the entry is left untouched.
    /// </summary>
    public static async Task RefreshEntryFromDiskAsync(FileBrowserEntry entry)
    {
        if (string.IsNullOrEmpty(entry.FilePath)) return;
        if (!File.Exists(entry.FilePath)) return;

        try
        {
            var bytes = await File.ReadAllBytesAsync(entry.FilePath);
            var utm = Radoub.Formats.Utm.UtmReader.Read(bytes);
            entry.Tag = utm.Tag ?? string.Empty;
            entry.DisplayLabel = utm.LocName.GetDefault() ?? string.Empty;
            entry.MetadataLoaded = true;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"StoreBrowserPanel.RefreshEntryFromDiskAsync({entry.Name}): {ex.Message}");
        }
    }

    /// <summary>
    /// Gets or sets whether HAK stores are shown.
    /// </summary>
    public bool ShowHakStores
    {
        get => _showHakStores;
        set
        {
            if (_showHakStores != value)
            {
                _showHakStores = value;
                _showHakCheckBox.IsChecked = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets the game data service for BIF resource access.
    /// Must be set before BIF scanning will work.
    /// </summary>
    public IGameDataService? GameDataService { get; set; }

    private void OnModuleFilterChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        OnFilterOptionsChanged();
    }

    private async void OnShowBifChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _showBifStores = _showBifCheckBox.IsChecked == true;
        UnifiedLogger.LogApplication(LogLevel.INFO, $"StoreBrowserPanel: Show BIF stores = {_showBifStores}");

        if (_showBifStores && !_bifStoresLoaded)
        {
            await LoadBifStoresAsync();
            MergeAdditionalEntries(_bifStores);
        }

        OnFilterOptionsChanged();
    }

    private async void OnShowHakChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _showHakStores = _showHakCheckBox.IsChecked == true;
        UnifiedLogger.LogApplication(LogLevel.INFO, $"StoreBrowserPanel: Show HAK stores = {_showHakStores}");

        if (_showHakStores && !_hakStoresLoaded)
        {
            await LoadHakStoresAsync();
            MergeAdditionalEntries(_hakStores);
        }

        OnFilterOptionsChanged();
    }

    protected override async Task<List<FileBrowserEntry>> LoadFilesFromModuleAsync(string modulePath)
    {
        // Reset HAK and BIF state when module changes
        _hakStoresLoaded = false;
        _hakStores.Clear();
        _bifStoresLoaded = false;
        _bifStores.Clear();

        return await Task.Run(() =>
        {
            var entries = new List<FileBrowserEntry>();

            try
            {
                if (!Directory.Exists(modulePath))
                    return entries;

                // Scan for .utm files (top-level only for modules)
                var storeFiles = Directory.GetFiles(modulePath, "*.utm", SearchOption.TopDirectoryOnly);

                foreach (var file in storeFiles)
                {
                    var storeName = Path.GetFileNameWithoutExtension(file);
                    if (!entries.Any(e => e.Name.Equals(storeName, StringComparison.OrdinalIgnoreCase)))
                    {
                        entries.Add(new StoreBrowserEntry
                        {
                            Name = storeName,
                            FilePath = file,
                            Source = "Module",
                            IsFromHak = false
                        });
                    }
                }

                entries = entries.OrderBy(e => e.Name).ToList();
                UnifiedLogger.LogApplication(LogLevel.INFO, $"StoreBrowserPanel: Found {entries.Count} module stores");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"StoreBrowserPanel: Error loading stores: {ex.Message}");
            }

            return entries;
        });
    }

    protected override async Task<List<FileBrowserEntry>> LoadAdditionalFilesAsync()
    {
        if (_showHakStores && !_hakStoresLoaded)
        {
            await LoadHakStoresAsync();
        }

        if (_showBifStores && !_bifStoresLoaded)
        {
            await LoadBifStoresAsync();
        }

        var additional = new List<FileBrowserEntry>();
        additional.AddRange(_hakStores);
        additional.AddRange(_bifStores);
        return additional;
    }

    protected override IEnumerable<FileBrowserEntry> ApplyCustomFilters(IEnumerable<FileBrowserEntry> entries)
    {
        bool showModule = _showModuleCheckBox.IsChecked == true;

        return entries.Where(e =>
        {
            if (e is StoreBrowserEntry se)
            {
                if (se.IsFromBif) return _showBifStores;
                if (se.IsFromHak) return _showHakStores;
            }
            else if (e.IsFromHak)
            {
                return _showHakStores;
            }
            return showModule;
        });
    }

    protected override string FormatCountLabel(int moduleCount, int hakCount, int totalCount)
    {
        if (totalCount == 0)
        {
            if (string.IsNullOrEmpty(ModulePath))
                return "No module loaded";
            return "No stores found";
        }

        // Separate BIF count from HAK count for distinct display
        var bifCount = _bifStores.Count(s => _showBifStores);
        var actualHakCount = hakCount - (_showBifStores ? bifCount : 0);

        var countText = $"{moduleCount} module";
        if (actualHakCount > 0)
        {
            countText += $" + {actualHakCount} HAK";
        }
        if (_showBifStores && _bifStores.Count > 0)
        {
            countText += $" + {_bifStores.Count} base game";
        }
        return countText;
    }

    private async Task LoadBifStoresAsync()
    {
        if (GameDataService == null || !GameDataService.IsConfigured)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, "StoreBrowserPanel: GameDataService not available for BIF scanning");
            _bifStoresLoaded = true;
            return;
        }

        try
        {
            _bifStores.Clear();
            ShowLoading("Scanning base game stores...");

            // Capture GameDataService into local for closure use inside Task.Run
            var gameDataService = GameDataService;
            var populate = PaletteCache != null && !PaletteCache.HasValidSourceCache("bif");
            List<SharedPaletteCacheItem>? paletteItems = null;

            await Task.Run(() =>
            {
                var resources = gameDataService.ListResources(ResourceTypes.Utm)
                    .Where(r => r.Source == GameResourceSource.Bif)
                    .ToList();

                if (populate) paletteItems = new List<SharedPaletteCacheItem>();

                foreach (var resource in resources)
                {
                    _bifStores.Add(new StoreBrowserEntry
                    {
                        Name = resource.ResRef,
                        Source = "Base Game",
                        IsFromHak = false,
                        IsFromBif = true
                    });

                    if (populate && paletteItems != null)
                    {
                        var bytes = gameDataService.FindResource(resource.ResRef, ResourceTypes.Utm);
                        if (bytes != null)
                        {
                            var item = BuildPaletteItemFromUtm(bytes, resource.ResRef, "Base Game");
                            if (item != null) paletteItems.Add(item);
                        }
                    }
                }
            });

            if (populate && paletteItems != null && PaletteCache != null)
            {
                _ = PaletteCache.SaveSourceCacheAsync("bif", paletteItems);
            }

            _bifStoresLoaded = true;
            UnifiedLogger.LogApplication(LogLevel.INFO, $"StoreBrowserPanel: Found {_bifStores.Count} base game stores from BIF");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"StoreBrowserPanel: Failed to load BIF stores: {ex.Message}");
        }
        finally
        {
            HideLoading();
        }
    }

    private async Task LoadHakStoresAsync()
    {
        try
        {
            _hakStores.Clear();

            // Use ModuleHakResolver to only scan module-referenced HAKs (#1687)
            if (string.IsNullOrEmpty(ModulePath) || !Directory.Exists(ModulePath))
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, "StoreBrowserPanel: No module path for HAK scanning");
                _hakStoresLoaded = true;
                return;
            }

            var hakSearchPaths = RadoubSettings.Instance.GetAllHakSearchPaths().ToList();
            var hakPaths = ModuleHakResolver.ResolveModuleHakPaths(ModulePath, hakSearchPaths);

            if (hakPaths.Count == 0)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, "StoreBrowserPanel: No module-referenced HAK files found");
                _hakStoresLoaded = true;
                return;
            }

            ShowLoading($"Scanning {hakPaths.Count} module HAK files...");
            UnifiedLogger.LogApplication(LogLevel.INFO, $"StoreBrowserPanel: Scanning {hakPaths.Count} module-referenced HAK files");

            for (int i = 0; i < hakPaths.Count; i++)
            {
                var hakPath = hakPaths[i];
                var hakName = Path.GetFileName(hakPath);
                UpdateLoadingStatus($"HAK {i + 1}/{hakPaths.Count}: {hakName}...");

                await Task.Run(() => ScanHakForStores(hakPath));
            }

            _hakStores = _hakStores.OrderBy(s => s.Name).ToList();
            _hakStoresLoaded = true;

            UnifiedLogger.LogApplication(LogLevel.INFO, $"StoreBrowserPanel: Loaded {_hakStores.Count} stores from HAK files");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"StoreBrowserPanel: Failed to load HAK stores: {ex.Message}");
        }
        finally
        {
            HideLoading();
        }
    }

    private void ScanHakForStores(string hakPath)
    {
        try
        {
            var hakFileName = Path.GetFileName(hakPath);
            var lastModified = File.GetLastWriteTimeUtc(hakPath);

            // Check in-memory HAK index cache first
            if (_hakCache.TryGetValue(hakPath, out var cached) && cached.LastModified == lastModified)
            {
                foreach (var store in cached.Stores)
                {
                    // Skip if already have this store from module or another HAK
                    if (_hakStores.Any(s => s.Name.Equals(store.Name, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    _hakStores.Add(new StoreBrowserEntry
                    {
                        Name = store.Name,
                        Source = store.Source,
                        IsFromHak = true,
                        HakPath = store.HakPath
                    });
                }
                return;
            }

            // Scan HAK
            var erf = ErfReader.ReadMetadataOnly(hakPath);
            var utmResources = erf.GetResourcesByType(ResourceTypes.Utm).ToList();
            var newCacheEntry = new StoreHakCacheEntry
            {
                HakPath = hakPath,
                LastModified = lastModified,
                Stores = new List<StoreBrowserEntry>()
            };

            // Persistent palette cache: populate Tag/DisplayName for instant
            // sort/search on subsequent panel loads (#2200).
            var populate = ShouldPopulatePaletteCacheForHak(hakPath, lastModified);
            var paletteItems = populate ? new List<SharedPaletteCacheItem>() : null;

            foreach (var resource in utmResources)
            {
                var storeEntry = new StoreBrowserEntry
                {
                    Name = resource.ResRef,
                    Source = $"HAK: {hakFileName}",
                    IsFromHak = true,
                    HakPath = hakPath
                };

                newCacheEntry.Stores.Add(storeEntry);

                if (populate && paletteItems != null)
                {
                    var bytes = ExtractFromHak(hakPath, resource.ResRef, ResourceTypes.Utm);
                    if (bytes != null)
                    {
                        var item = BuildPaletteItemFromUtm(bytes, resource.ResRef, $"HAK: {hakFileName}");
                        if (item != null) paletteItems.Add(item);
                    }
                }

                // Skip duplicates in visible list
                if (_hakStores.Any(s => s.Name.Equals(resource.ResRef, StringComparison.OrdinalIgnoreCase)))
                    continue;

                _hakStores.Add(storeEntry);
            }

            _hakCache[hakPath] = newCacheEntry;

            if (populate && paletteItems != null && PaletteCache != null)
            {
                _ = PaletteCache.SaveSourceCacheAsync(
                    "hak",
                    paletteItems,
                    validationPath: hakPath,
                    sourceModified: lastModified);
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"StoreBrowserPanel: Cached {utmResources.Count} stores from {hakFileName}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning HAK {UnifiedLogger.SanitizePath(hakPath)}: {ex.Message}");
        }
    }

    /// <summary>
    /// True when the persistent palette cache for this HAK is missing or stale —
    /// the populator should extract+parse the UTMs and write the cache. False
    /// when no PaletteCache is wired or the existing cache is fresh.
    /// </summary>
    private bool ShouldPopulatePaletteCacheForHak(string hakPath, DateTime lastModified)
    {
        if (PaletteCache == null) return false;
        if (PaletteCache.HasValidSourceCache("hak", hakPath)) return false;
        return true;
    }
}
