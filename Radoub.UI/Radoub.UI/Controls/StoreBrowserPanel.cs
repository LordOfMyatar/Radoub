using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Logging;
using Radoub.Formats.Resolver;
using Radoub.Formats.Settings;
using Radoub.UI.Services;

namespace Radoub.UI.Controls;

/// <summary>
/// Store-specific entry with HAK support.
/// </summary>
public class StoreBrowserEntry : FileBrowserEntry
{
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
public class StoreBrowserPanel : FileBrowserPanelBase
{
    private readonly IScriptBrowserContext? _context;
    private readonly CheckBox _showHakCheckBox;
    private bool _showHakStores;
    private bool _hakStoresLoaded;
    private List<StoreBrowserEntry> _hakStores = new();

    // Static cache for HAK file contents - persists across panel instances
    private static readonly Dictionary<string, StoreHakCacheEntry> _hakCache = new();

    public StoreBrowserPanel() : this(null)
    {
    }

    public StoreBrowserPanel(IScriptBrowserContext? context)
    {
        _context = context;

        FileExtension = ".utm";
        SearchWatermark = "Type to filter stores...";
        HeaderTextContent = "Stores";

        // Create and wire up HAK checkbox
        _showHakCheckBox = new CheckBox
        {
            Content = "Show HAK",
            IsChecked = false,
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
        };
        ToolTip.SetTip(_showHakCheckBox, "Include stores from HAK files");
        _showHakCheckBox.IsCheckedChanged += OnShowHakChanged;

        FilterOptionsContent = _showHakCheckBox;
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
        // Reset HAK state when module changes
        _hakStoresLoaded = false;
        _hakStores.Clear();

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

        return _hakStores.Cast<FileBrowserEntry>().ToList();
    }

    protected override IEnumerable<FileBrowserEntry> ApplyCustomFilters(IEnumerable<FileBrowserEntry> entries)
    {
        // Filter out HAK entries if not showing HAK
        if (!_showHakStores)
        {
            return entries.Where(e => !e.IsFromHak);
        }

        return entries;
    }

    protected override string FormatCountLabel(int moduleCount, int hakCount, int totalCount)
    {
        if (totalCount == 0)
        {
            if (string.IsNullOrEmpty(ModulePath))
                return "No module loaded";
            return "No stores found";
        }

        var countText = $"{moduleCount} module";
        if (hakCount > 0)
        {
            countText += $" + {hakCount} HAK";
        }
        return countText;
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

            // Check cache first
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

                // Skip if already have this store
                if (_hakStores.Any(s => s.Name.Equals(resource.ResRef, StringComparison.OrdinalIgnoreCase)))
                    continue;

                _hakStores.Add(storeEntry);
            }

            _hakCache[hakPath] = newCacheEntry;

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"StoreBrowserPanel: Cached {utmResources.Count} stores from {hakFileName}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning HAK {UnifiedLogger.SanitizePath(hakPath)}: {ex.Message}");
        }
    }
}
