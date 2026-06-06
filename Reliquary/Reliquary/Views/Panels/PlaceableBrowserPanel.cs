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
using Radoub.Formats.Utp;
using Radoub.UI.Controls;
using Radoub.UI.Services;

namespace PlaceableEditor.Views.Panels;

/// <summary>
/// Placeable-specific entry with HAK and BIF support.
/// </summary>
public class PlaceableBrowserEntry : FileBrowserEntry
{
    /// <summary>True if this resource is from a base game BIF file.</summary>
    public bool IsFromBif { get; set; }

    /// <summary>Display name with source indicator for BIF entries.</summary>
    public override string DisplayName => IsFromBif ? $"{Name} ({Source})" : base.DisplayName;
}

/// <summary>Cached HAK file placeable data to avoid re-scanning on each refresh.</summary>
internal class PlaceableHakCacheEntry
{
    public string HakPath { get; set; } = "";
    public DateTime LastModified { get; set; }
    public List<PlaceableBrowserEntry> Placeables { get; set; } = new();
}

/// <summary>
/// Placeable browser panel for Reliquary's main window (#2294). Lists .utp files
/// from the module folder with optional HAK and base-game (BIF) scanning. Reads
/// Name (LocName) + Tag for the browser columns. Mirrors ItemBrowserPanel.
/// Delete-with-backup is inherited from <see cref="FileBrowserPanelBase"/> (#2350).
/// </summary>
public class PlaceableBrowserPanel : FileBrowserPanelBase, IBrowserRowRefresher
{
    private readonly CheckBox _showModuleCheckBox;
    private readonly CheckBox _showHakCheckBox;
    private readonly CheckBox _showBifCheckBox;
    private bool _showHakPlaceables;
    private bool _hakPlaceablesLoaded;
    private bool _showBifPlaceables;
    private bool _bifPlaceablesLoaded;
    private List<PlaceableBrowserEntry> _hakPlaceables = new();
    private List<PlaceableBrowserEntry> _bifPlaceables = new();

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, PlaceableHakCacheEntry> _hakCache = new();

    public PlaceableBrowserPanel()
    {
        FileExtension = ".utp";
        SearchWatermark = "Type to filter placeables...";
        HeaderTextContent = "Placeables";

        _showModuleCheckBox = new CheckBox
        {
            Content = "Module",
            IsChecked = true,
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
        };
        ToolTip.SetTip(_showModuleCheckBox, "Show .utp files from module folder");
        _showModuleCheckBox.IsCheckedChanged += (_, _) => OnFilterOptionsChanged();

        _showHakCheckBox = new CheckBox
        {
            Content = "Show HAK",
            IsChecked = false,
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
        };
        ToolTip.SetTip(_showHakCheckBox, "Include placeables from HAK files");
        _showHakCheckBox.IsCheckedChanged += OnShowHakChanged;

        _showBifCheckBox = new CheckBox
        {
            Content = "Base Game",
            IsChecked = false,
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
        };
        ToolTip.SetTip(_showBifCheckBox, "Show placeable blueprints from base game BIF archives");
        _showBifCheckBox.IsCheckedChanged += OnShowBifChanged;

        var filterPanel = new StackPanel { Spacing = 2 };
        filterPanel.Children.Add(_showModuleCheckBox);
        filterPanel.Children.Add(_showHakCheckBox);
        filterPanel.Children.Add(_showBifCheckBox);
        FilterOptionsContent = filterPanel;
    }

    /// <summary>Game data service for BIF resource access. Set before BIF scanning.</summary>
    public IGameDataService? GameDataService { get; set; }

    /// <summary>Optional shared palette cache for zero-I/O Tag/Name on HAK/BIF rows.</summary>
    public ISharedPaletteCacheService? PaletteCache { get; set; }

    /// <summary>
    /// Pure UTP metadata read (Tag + default LocName). Static seam so the read is
    /// unit-testable without Avalonia. Returns empty strings on parse failure.
    /// </summary>
    public static (string tag, string name) ReadUtpMetadata(byte[] bytes)
    {
        try
        {
            var utp = UtpReader.Read(bytes);
            return (utp.Tag ?? string.Empty, utp.LocName.GetDefault() ?? string.Empty);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"PlaceableBrowserPanel.ReadUtpMetadata: {ex.Message}");
            return (string.Empty, string.Empty);
        }
    }

    protected override Task<(string tag, string name)> ReadSourceMetadataAsync(byte[] bytes)
        => Task.FromResult(ReadUtpMetadata(bytes));

    protected override bool IsArchiveEntry(FileBrowserEntry entry)
        => entry is PlaceableBrowserEntry p && (p.IsFromHak || p.IsFromBif);

    protected override Task<byte[]?> ExtractArchiveBytesAsync(FileBrowserEntry entry)
        => Task.FromResult(ExtractPlaceableArchiveBytes(entry, GameDataService));

    /// <summary>Extract UTP bytes for a HAK/BIF entry so the host can open it read-only. Null on miss.</summary>
    public byte[]? ExtractArchiveBytes(FileBrowserEntry entry)
        => ExtractPlaceableArchiveBytes(entry, GameDataService);

    private static byte[]? ExtractPlaceableArchiveBytes(FileBrowserEntry entry, IGameDataService? gameDataService)
    {
        if (entry is not PlaceableBrowserEntry pe) return null;

        if (pe.IsFromBif)
        {
            if (gameDataService is { IsConfigured: true })
                return gameDataService.FindResource(pe.Name, ResourceTypes.Utp);
            return null;
        }

        if (pe.IsFromHak && !string.IsNullOrEmpty(pe.HakPath))
            return ExtractFromHak(pe.HakPath, pe.Name, ResourceTypes.Utp);

        return null;
    }

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
                        var (tag, name) = ReadUtpMetadata(bytes);
                        entry.Tag = tag;
                        entry.DisplayLabel = name;
                    }
                    entry.MetadataLoaded = true;
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"PlaceableBrowserPanel.IndexMetadataAsync({entry.Name}): {ex.Message}");
                entry.MetadataLoaded = true;
            }

            processed++;
            if (processed % 50 == 0)
                await Task.Yield();
        }
    }

    /// <summary>
    /// Build a ResRef → cache-item lookup from the active palette cache. Returns
    /// an empty dictionary when no cache is wired or the cache is empty.
    /// First-write-wins preserves Module > Override > HAK > BIF insertion priority.
    /// </summary>
    private Dictionary<string, SharedPaletteCacheItem> BuildPaletteLookup()
    {
        if (PaletteCache == null) return new Dictionary<string, SharedPaletteCacheItem>();

        var items = PaletteCache.GetAggregatedCache();
        if (items == null || items.Count == 0)
            return new Dictionary<string, SharedPaletteCacheItem>();

        var dict = new Dictionary<string, SharedPaletteCacheItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
            dict.TryAdd(item.ResRef, item);
        return dict;
    }

    /// <summary>
    /// Pure-logic test seam: try to populate Tag + DisplayLabel from a cache
    /// lookup. Returns true on hit, false on miss. Lookup is keyed by ResRef
    /// (case-insensitive — caller responsible for using OrdinalIgnoreCase).
    /// Mirrors ItemBrowserPanel.TryFillFromCache for parity.
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

    private async Task<byte[]?> ReadEntryBytesAsync(FileBrowserEntry entry, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(entry.FilePath) && File.Exists(entry.FilePath))
            return await File.ReadAllBytesAsync(entry.FilePath, cancellationToken);

        return await Task.Run(() => ExtractPlaceableArchiveBytes(entry, GameDataService), cancellationToken);
    }

    public override async Task RefreshEntryMetadataAsync(FileBrowserEntry entry)
    {
        try
        {
            var bytes = await ReadEntryBytesAsync(entry, CancellationToken.None);
            if (bytes == null) return;
            var (tag, name) = ReadUtpMetadata(bytes);
            entry.Tag = tag;
            entry.DisplayLabel = name;
            entry.MetadataLoaded = true;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"PlaceableBrowserPanel.RefreshEntryMetadataAsync({entry.Name}): {ex.Message}");
        }
    }

    public Task RefreshRowAsync(string filePath)
    {
        var entry = FindEntryByFilePath(filePath);
        return entry == null ? Task.CompletedTask : RefreshEntryMetadataAsync(entry);
    }

    private async void OnShowHakChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _showHakPlaceables = _showHakCheckBox.IsChecked == true;
        if (_showHakPlaceables && !_hakPlaceablesLoaded)
        {
            await LoadHakPlaceablesAsync();
            MergeAdditionalEntries(_hakPlaceables);
        }
        OnFilterOptionsChanged();
    }

    private async void OnShowBifChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _showBifPlaceables = _showBifCheckBox.IsChecked == true;
        if (_showBifPlaceables && !_bifPlaceablesLoaded)
        {
            await LoadBifPlaceablesAsync();
            MergeAdditionalEntries(_bifPlaceables);
        }
        OnFilterOptionsChanged();
    }

    protected override async Task<List<FileBrowserEntry>> LoadFilesFromModuleAsync(string modulePath)
    {
        _hakPlaceablesLoaded = false;
        _hakPlaceables.Clear();
        _bifPlaceablesLoaded = false;
        _bifPlaceables.Clear();

        return await Task.Run(() =>
        {
            var entries = new List<FileBrowserEntry>();
            try
            {
                if (!Directory.Exists(modulePath))
                    return entries;

                var files = Directory.GetFiles(modulePath, "*.utp", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (!entries.Any(en => en.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    {
                        entries.Add(new PlaceableBrowserEntry
                        {
                            Name = name,
                            FilePath = file,
                            Source = "Module",
                            IsFromHak = false
                        });
                    }
                }

                entries = entries.OrderBy(en => en.Name).ToList();
                UnifiedLogger.LogApplication(LogLevel.INFO, $"PlaceableBrowserPanel: Found {entries.Count} module placeables");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"PlaceableBrowserPanel: Error loading placeables: {ex.Message}");
            }
            return entries;
        });
    }

    protected override async Task<List<FileBrowserEntry>> LoadAdditionalFilesAsync()
    {
        if (_showHakPlaceables && !_hakPlaceablesLoaded)
            await LoadHakPlaceablesAsync();
        if (_showBifPlaceables && !_bifPlaceablesLoaded)
            await LoadBifPlaceablesAsync();

        var additional = new List<FileBrowserEntry>();
        additional.AddRange(_hakPlaceables);
        additional.AddRange(_bifPlaceables);
        return additional;
    }

    protected override IEnumerable<FileBrowserEntry> ApplyCustomFilters(IEnumerable<FileBrowserEntry> entries)
    {
        bool showModule = _showModuleCheckBox.IsChecked == true;
        return entries.Where(en => PassesPlaceableFilter(en, showModule, _showHakPlaceables, _showBifPlaceables));
    }

    /// <summary>
    /// Pure-logic test seam: classify a row against the Module / HAK / BIF filter
    /// checkboxes. Mirrors ItemBrowserPanel.PassesItemFilter for parity.
    /// </summary>
    internal static bool PassesPlaceableFilter(FileBrowserEntry entry, bool showModule, bool showHak, bool showBif)
    {
        if (entry is PlaceableBrowserEntry pe)
        {
            if (pe.IsFromBif) return showBif;
            if (pe.IsFromHak) return showHak;
            return showModule;
        }
        if (entry.IsFromHak) return showHak;
        return showModule;
    }

    private async Task LoadBifPlaceablesAsync()
    {
        if (GameDataService == null || !GameDataService.IsConfigured)
        {
            _bifPlaceablesLoaded = true;
            return;
        }

        try
        {
            _bifPlaceables.Clear();
            ShowLoading("Scanning base game placeables...");

            await Task.Run(() =>
            {
                var resources = GameDataService.ListResources(ResourceTypes.Utp)
                    .Where(r => r.Source == GameResourceSource.Bif)
                    .ToList();

                foreach (var resource in resources)
                {
                    _bifPlaceables.Add(new PlaceableBrowserEntry
                    {
                        Name = resource.ResRef,
                        Source = "Base Game",
                        IsFromHak = false,
                        IsFromBif = true
                    });
                }
            });

            _bifPlaceables = _bifPlaceables.OrderBy(en => en.Name).ToList();
            _bifPlaceablesLoaded = true;
            UnifiedLogger.LogApplication(LogLevel.INFO, $"PlaceableBrowserPanel: Found {_bifPlaceables.Count} base game placeables");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"PlaceableBrowserPanel: Failed to load BIF placeables: {ex.Message}");
        }
        finally
        {
            HideLoading();
        }
    }

    private async Task LoadHakPlaceablesAsync()
    {
        try
        {
            _hakPlaceables.Clear();

            if (string.IsNullOrEmpty(ModulePath) || !Directory.Exists(ModulePath))
            {
                _hakPlaceablesLoaded = true;
                return;
            }

            var hakSearchPaths = RadoubSettings.Instance.GetAllHakSearchPaths().ToList();
            var hakPaths = ModuleHakResolver.ResolveModuleHakPaths(ModulePath, hakSearchPaths);

            if (hakPaths.Count == 0)
            {
                _hakPlaceablesLoaded = true;
                return;
            }

            ShowLoading($"Scanning {hakPaths.Count} module HAK files...");
            for (int i = 0; i < hakPaths.Count; i++)
            {
                var hakName = Path.GetFileName(hakPaths[i]);
                UpdateLoadingStatus($"HAK {i + 1}/{hakPaths.Count}: {hakName}...");
                await Task.Run(() => ScanHakForPlaceables(hakPaths[i]));
            }

            _hakPlaceables = _hakPlaceables.OrderBy(en => en.Name).ToList();
            _hakPlaceablesLoaded = true;
            UnifiedLogger.LogApplication(LogLevel.INFO, $"PlaceableBrowserPanel: Loaded {_hakPlaceables.Count} placeables from HAK files");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"PlaceableBrowserPanel: Failed to load HAK placeables: {ex.Message}");
        }
        finally
        {
            HideLoading();
        }
    }

    private void ScanHakForPlaceables(string hakPath)
    {
        try
        {
            var hakFileName = Path.GetFileName(hakPath);
            var lastModified = File.GetLastWriteTimeUtc(hakPath);

            if (_hakCache.TryGetValue(hakPath, out var cached) && cached.LastModified == lastModified)
            {
                foreach (var item in cached.Placeables)
                {
                    _hakPlaceables.Add(new PlaceableBrowserEntry
                    {
                        Name = item.Name,
                        Source = item.Source,
                        IsFromHak = true,
                        HakPath = item.HakPath
                    });
                }
                return;
            }

            var erf = ErfReader.ReadMetadataOnly(hakPath);
            var resources = erf.GetResourcesByType(ResourceTypes.Utp).ToList();
            var newCacheEntry = new PlaceableHakCacheEntry
            {
                HakPath = hakPath,
                LastModified = lastModified,
                Placeables = new List<PlaceableBrowserEntry>()
            };

            foreach (var resource in resources)
            {
                var entry = new PlaceableBrowserEntry
                {
                    Name = resource.ResRef,
                    Source = $"HAK: {hakFileName}",
                    IsFromHak = true,
                    HakPath = hakPath
                };
                newCacheEntry.Placeables.Add(entry);
                _hakPlaceables.Add(entry);
            }

            _hakCache[hakPath] = newCacheEntry;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning HAK {UnifiedLogger.SanitizePath(hakPath)}: {ex.Message}");
        }
    }
}
