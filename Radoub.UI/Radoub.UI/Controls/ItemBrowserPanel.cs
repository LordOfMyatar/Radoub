using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;

namespace Radoub.UI.Controls;

/// <summary>
/// Item-specific entry with HAK support.
/// </summary>
public class ItemBrowserEntry : FileBrowserEntry
{
}

/// <summary>
/// Cached HAK file item data to avoid re-scanning on each refresh.
/// </summary>
internal class ItemHakCacheEntry
{
    public string HakPath { get; set; } = "";
    public DateTime LastModified { get; set; }
    public List<ItemBrowserEntry> Items { get; set; } = new();
}

/// <summary>
/// Item browser panel for embedding in Relique's main window.
/// Provides file list with optional HAK scanning for .uti files.
/// </summary>
public class ItemBrowserPanel : FileBrowserPanelBase
{
    private readonly CheckBox _showHakCheckBox;
    private bool _showHakItems;
    private bool _hakItemsLoaded;
    private List<ItemBrowserEntry> _hakItems = new();

    // Static cache for HAK file contents - persists across panel instances
    private static readonly Dictionary<string, ItemHakCacheEntry> _hakCache = new();

    public ItemBrowserPanel()
    {
        FileExtension = ".uti";
        SearchWatermark = "Type to filter items...";
        HeaderTextContent = "Items";

        // Create and wire up HAK checkbox
        _showHakCheckBox = new CheckBox
        {
            Content = "Show HAK",
            IsChecked = false,
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
        };
        ToolTip.SetTip(_showHakCheckBox, "Include items from HAK files");
        _showHakCheckBox.IsCheckedChanged += OnShowHakChanged;

        FilterOptionsContent = _showHakCheckBox;
    }

    /// <summary>
    /// Gets or sets whether HAK items are shown.
    /// </summary>
    public bool ShowHakItems
    {
        get => _showHakItems;
        set
        {
            if (_showHakItems != value)
            {
                _showHakItems = value;
                _showHakCheckBox.IsChecked = value;
            }
        }
    }

    private async void OnShowHakChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _showHakItems = _showHakCheckBox.IsChecked == true;
        UnifiedLogger.LogApplication(LogLevel.INFO, $"ItemBrowserPanel: Show HAK items = {_showHakItems}");

        if (_showHakItems && !_hakItemsLoaded)
        {
            await LoadHakItemsAsync();
            MergeAdditionalEntries(_hakItems);
        }

        OnFilterOptionsChanged();
    }

    protected override async Task<List<FileBrowserEntry>> LoadFilesFromModuleAsync(string modulePath)
    {
        // Reset HAK state when module changes
        _hakItemsLoaded = false;
        _hakItems.Clear();

        return await Task.Run(() =>
        {
            var entries = new List<FileBrowserEntry>();

            try
            {
                if (!Directory.Exists(modulePath))
                    return entries;

                var itemFiles = Directory.GetFiles(modulePath, "*.uti", SearchOption.TopDirectoryOnly);

                foreach (var file in itemFiles)
                {
                    var itemName = Path.GetFileNameWithoutExtension(file);
                    if (!entries.Any(e => e.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase)))
                    {
                        entries.Add(new ItemBrowserEntry
                        {
                            Name = itemName,
                            FilePath = file,
                            Source = "Module",
                            IsFromHak = false
                        });
                    }
                }

                entries = entries.OrderBy(e => e.Name).ToList();
                UnifiedLogger.LogApplication(LogLevel.INFO, $"ItemBrowserPanel: Found {entries.Count} module items");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"ItemBrowserPanel: Error loading items: {ex.Message}");
            }

            return entries;
        });
    }

    protected override async Task<List<FileBrowserEntry>> LoadAdditionalFilesAsync()
    {
        if (_showHakItems && !_hakItemsLoaded)
        {
            await LoadHakItemsAsync();
        }

        return _hakItems.Cast<FileBrowserEntry>().ToList();
    }

    protected override IEnumerable<FileBrowserEntry> ApplyCustomFilters(IEnumerable<FileBrowserEntry> entries)
    {
        if (!_showHakItems)
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
            return "No items found";
        }

        var countText = $"{moduleCount} module";
        if (hakCount > 0)
        {
            countText += $" + {hakCount} HAK";
        }
        return countText;
    }

    private async Task LoadHakItemsAsync()
    {
        try
        {
            _hakItems.Clear();

            var hakPaths = new List<string>();

            if (!string.IsNullOrEmpty(ModulePath) && Directory.Exists(ModulePath))
            {
                hakPaths.AddRange(GetHakFilesFromPath(ModulePath));
            }

            var userPath = RadoubSettings.Instance.NeverwinterNightsPath;
            if (!string.IsNullOrEmpty(userPath) && Directory.Exists(userPath))
            {
                var hakFolder = Path.Combine(userPath, "hak");
                if (Directory.Exists(hakFolder))
                {
                    hakPaths.AddRange(GetHakFilesFromPath(hakFolder));
                }
            }

            hakPaths = hakPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (hakPaths.Count == 0)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, "ItemBrowserPanel: No HAK files found to scan");
                _hakItemsLoaded = true;
                return;
            }

            ShowLoading($"Scanning {hakPaths.Count} HAK files...");
            UnifiedLogger.LogApplication(LogLevel.INFO, $"ItemBrowserPanel: Scanning {hakPaths.Count} HAK files");

            for (int i = 0; i < hakPaths.Count; i++)
            {
                var hakPath = hakPaths[i];
                var hakName = Path.GetFileName(hakPath);
                UpdateLoadingStatus($"HAK {i + 1}/{hakPaths.Count}: {hakName}...");

                await Task.Run(() => ScanHakForItems(hakPath));
            }

            _hakItems = _hakItems.OrderBy(s => s.Name).ToList();
            _hakItemsLoaded = true;

            UnifiedLogger.LogApplication(LogLevel.INFO, $"ItemBrowserPanel: Loaded {_hakItems.Count} items from HAK files");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"ItemBrowserPanel: Failed to load HAK items: {ex.Message}");
        }
        finally
        {
            HideLoading();
        }
    }

    private IEnumerable<string> GetHakFilesFromPath(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                return Directory.GetFiles(path, "*.hak", SearchOption.TopDirectoryOnly);
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Error scanning for HAKs in {UnifiedLogger.SanitizePath(path)}: {ex.Message}");
        }
        return Enumerable.Empty<string>();
    }

    private void ScanHakForItems(string hakPath)
    {
        try
        {
            var hakFileName = Path.GetFileName(hakPath);
            var lastModified = File.GetLastWriteTimeUtc(hakPath);

            // Check cache first
            if (_hakCache.TryGetValue(hakPath, out var cached) && cached.LastModified == lastModified)
            {
                foreach (var item in cached.Items)
                {
                    if (_hakItems.Any(s => s.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    _hakItems.Add(new ItemBrowserEntry
                    {
                        Name = item.Name,
                        Source = item.Source,
                        IsFromHak = true,
                        HakPath = item.HakPath
                    });
                }
                return;
            }

            // Scan HAK
            var erf = ErfReader.ReadMetadataOnly(hakPath);
            var utiResources = erf.GetResourcesByType(ResourceTypes.Uti).ToList();
            var newCacheEntry = new ItemHakCacheEntry
            {
                HakPath = hakPath,
                LastModified = lastModified,
                Items = new List<ItemBrowserEntry>()
            };

            foreach (var resource in utiResources)
            {
                var itemEntry = new ItemBrowserEntry
                {
                    Name = resource.ResRef,
                    Source = $"HAK: {hakFileName}",
                    IsFromHak = true,
                    HakPath = hakPath
                };

                newCacheEntry.Items.Add(itemEntry);

                if (_hakItems.Any(s => s.Name.Equals(resource.ResRef, StringComparison.OrdinalIgnoreCase)))
                    continue;

                _hakItems.Add(itemEntry);
            }

            _hakCache[hakPath] = newCacheEntry;

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"ItemBrowserPanel: Cached {utiResources.Count} items from {hakFileName}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning HAK {UnifiedLogger.SanitizePath(hakPath)}: {ex.Message}");
        }
    }
}
