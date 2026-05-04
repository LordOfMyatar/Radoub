using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Radoub.UI.Services;

namespace Radoub.UI.Controls;

/// <summary>
/// Item-specific entry with HAK and BIF support.
/// </summary>
public class ItemBrowserEntry : FileBrowserEntry
{
    /// <summary>
    /// True if this resource is from a base game BIF file.
    /// </summary>
    public bool IsFromBif { get; set; }

    /// <summary>
    /// Display name with source indicator for BIF entries (matches StoreBrowserEntry).
    /// </summary>
    public override string DisplayName => IsFromBif ? $"{Name} ({Source})" : base.DisplayName;
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
/// Provides file list with optional HAK and BIF (base game) scanning for .uti files.
/// </summary>
public class ItemBrowserPanel : FileBrowserPanelBase
{
    private readonly CheckBox _showModuleCheckBox;
    private readonly CheckBox _showHakCheckBox;
    private readonly CheckBox _showBifCheckBox;
    private bool _showHakItems;
    private bool _hakItemsLoaded;
    private bool _showBifItems;
    private bool _bifItemsLoaded;
    private List<ItemBrowserEntry> _hakItems = new();
    private List<ItemBrowserEntry> _bifItems = new();

    // Static cache for HAK file contents - persists across panel instances
    private static readonly Dictionary<string, ItemHakCacheEntry> _hakCache = new();

    public ItemBrowserPanel()
    {
        FileExtension = ".uti";
        SearchWatermark = "Type to filter items...";
        HeaderTextContent = "Items";

        // Module checkbox (checked by default — parity with Store/Creature browsers)
        _showModuleCheckBox = new CheckBox
        {
            Content = "Module",
            IsChecked = true,
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
        };
        ToolTip.SetTip(_showModuleCheckBox, "Show .uti files from module folder");
        _showModuleCheckBox.IsCheckedChanged += OnModuleFilterChanged;

        // Create and wire up HAK checkbox
        _showHakCheckBox = new CheckBox
        {
            Content = "Show HAK",
            IsChecked = false,
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
        };
        ToolTip.SetTip(_showHakCheckBox, "Include items from HAK files");
        _showHakCheckBox.IsCheckedChanged += OnShowHakChanged;

        // Create and wire up BIF (base game) checkbox (#2106)
        _showBifCheckBox = new CheckBox
        {
            Content = "Base Game",
            IsChecked = false,
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
        };
        ToolTip.SetTip(_showBifCheckBox, "Show item blueprints from base game BIF archives");
        _showBifCheckBox.IsCheckedChanged += OnShowBifChanged;

        var filterPanel = new StackPanel { Spacing = 2 };
        filterPanel.Children.Add(_showModuleCheckBox);
        filterPanel.Children.Add(_showHakCheckBox);
        filterPanel.Children.Add(_showBifCheckBox);
        FilterOptionsContent = filterPanel;
    }

    private void OnModuleFilterChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        OnFilterOptionsChanged();
    }

    /// <summary>
    /// Gets or sets the game data service for BIF resource access.
    /// Must be set before BIF scanning will work.
    /// </summary>
    public IGameDataService? GameDataService { get; set; }

    protected override bool SupportsCopyToModule() => true;

    protected override bool IsArchiveEntry(FileBrowserEntry entry) => IsItemArchiveEntry(entry);

    /// <summary>
    /// Pure-logic test seam: archive-entry classification for ItemBrowserPanel.
    /// Returns true for ItemBrowserEntry rows that originated from a HAK or BIF archive.
    /// </summary>
    internal static bool IsItemArchiveEntry(FileBrowserEntry entry)
        => entry is ItemBrowserEntry i && (i.IsFromHak || i.IsFromBif);

    protected override Task<byte[]?> ExtractArchiveBytesAsync(FileBrowserEntry entry)
        => Task.FromResult(ExtractItemArchiveBytes(entry, GameDataService));

    /// <summary>
    /// Pure-logic test seam: route an archive-sourced ItemBrowserEntry to the correct
    /// extraction path (BIF via GameDataService, HAK via shared ExtractFromHak helper).
    /// Returns null when the entry is not archive-sourced or required dependencies are missing.
    /// </summary>
    internal static byte[]? ExtractItemArchiveBytes(FileBrowserEntry entry, IGameDataService? gameDataService)
    {
        if (entry is not ItemBrowserEntry itemEntry) return null;

        if (itemEntry.IsFromBif)
        {
            if (gameDataService is { IsConfigured: true })
                return gameDataService.FindResource(itemEntry.Name, ResourceTypes.Uti);
            return null;
        }

        if (itemEntry.IsFromHak && !string.IsNullOrEmpty(itemEntry.HakPath))
            return ExtractFromHak(itemEntry.HakPath, itemEntry.Name, ResourceTypes.Uti);

        return null;
    }

    protected override Task<(string tag, string name)> ReadSourceMetadataAsync(byte[] bytes)
    {
        try
        {
            var uti = Radoub.Formats.Uti.UtiReader.Read(bytes);
            return Task.FromResult((uti.Tag ?? string.Empty, uti.LocalizedName.GetDefault() ?? string.Empty));
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"ItemBrowserPanel.ReadSourceMetadataAsync: {ex.Message}");
            return Task.FromResult((string.Empty, string.Empty));
        }
    }

    protected override Task<byte[]> ApplyCopyCustomizationsAsync(byte[] sourceBytes, CopyToModuleResult result)
        => Task.FromResult(ApplyUtiCopyCustomizations(sourceBytes, result));

    /// <summary>
    /// Rewrite a UTI byte blob with the user's new TemplateResRef/Tag/LocalizedName.
    /// </summary>
    internal static byte[] ApplyUtiCopyCustomizations(byte[] sourceBytes, CopyToModuleResult result)
    {
        var uti = Radoub.Formats.Uti.UtiReader.Read(sourceBytes);
        uti.TemplateResRef = result.NewResRef;
        if (result.NewTag != null) uti.Tag = result.NewTag;
        if (result.NewName != null) uti.LocalizedName.SetString(0, result.NewName);
        return Radoub.Formats.Uti.UtiWriter.Write(uti);
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

    private async void OnShowBifChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _showBifItems = _showBifCheckBox.IsChecked == true;
        UnifiedLogger.LogApplication(LogLevel.INFO, $"ItemBrowserPanel: Show BIF items = {_showBifItems}");

        if (_showBifItems && !_bifItemsLoaded)
        {
            await LoadBifItemsAsync();
            MergeAdditionalEntries(_bifItems);
        }

        OnFilterOptionsChanged();
    }

    protected override async Task<List<FileBrowserEntry>> LoadFilesFromModuleAsync(string modulePath)
    {
        // Reset HAK and BIF state when module changes
        _hakItemsLoaded = false;
        _hakItems.Clear();
        _bifItemsLoaded = false;
        _bifItems.Clear();

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

        if (_showBifItems && !_bifItemsLoaded)
        {
            await LoadBifItemsAsync();
        }

        var additional = new List<FileBrowserEntry>();
        additional.AddRange(_hakItems);
        additional.AddRange(_bifItems);
        return additional;
    }

    protected override IEnumerable<FileBrowserEntry> ApplyCustomFilters(IEnumerable<FileBrowserEntry> entries)
    {
        bool showModule = _showModuleCheckBox.IsChecked == true;
        return entries.Where(e => PassesItemFilter(e, showModule, _showHakItems, _showBifItems));
    }

    /// <summary>
    /// Pure-logic test seam: classify a row against the three filter checkboxes
    /// (Module / HAK / BIF). Mirrors the filter behavior in StoreBrowserPanel
    /// and CreatureBrowserPanel for parity (#2106 follow-up).
    /// </summary>
    internal static bool PassesItemFilter(FileBrowserEntry entry, bool showModule, bool showHak, bool showBif)
    {
        if (entry is ItemBrowserEntry ie)
        {
            if (ie.IsFromBif) return showBif;
            if (ie.IsFromHak) return showHak;
            return showModule;
        }
        // Fallback for non-ItemBrowserEntry rows
        if (entry.IsFromHak) return showHak;
        return showModule;
    }

    protected override string FormatCountLabel(int moduleCount, int hakCount, int totalCount)
    {
        if (totalCount == 0)
        {
            if (string.IsNullOrEmpty(ModulePath))
                return "No module loaded";
            return "No items found";
        }

        // Separate BIF from HAK in the count display
        var bifCount = _showBifItems ? _bifItems.Count : 0;
        var actualHakCount = hakCount - (_showBifItems ? bifCount : 0);
        var parts = new List<string>();

        if (_showModuleCheckBox.IsChecked == true && moduleCount > 0)
            parts.Add($"{moduleCount} module");
        if (actualHakCount > 0)
            parts.Add($"{actualHakCount} HAK");
        if (_showBifItems && _bifItems.Count > 0)
            parts.Add($"{_bifItems.Count} base game");

        return parts.Count == 0 ? "No items shown" : string.Join(" + ", parts);
    }

    private async Task LoadBifItemsAsync()
    {
        if (GameDataService == null || !GameDataService.IsConfigured)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, "ItemBrowserPanel: GameDataService not available for BIF scanning");
            _bifItemsLoaded = true;
            return;
        }

        try
        {
            _bifItems.Clear();
            ShowLoading("Scanning base game items...");

            await Task.Run(() =>
            {
                var resources = GameDataService.ListResources(ResourceTypes.Uti)
                    .Where(r => r.Source == GameResourceSource.Bif)
                    .ToList();

                foreach (var resource in resources)
                {
                    _bifItems.Add(new ItemBrowserEntry
                    {
                        Name = resource.ResRef,
                        Source = "Base Game",
                        IsFromHak = false,
                        IsFromBif = true
                    });
                }
            });

            _bifItems = _bifItems.OrderBy(e => e.Name).ToList();
            _bifItemsLoaded = true;
            UnifiedLogger.LogApplication(LogLevel.INFO, $"ItemBrowserPanel: Found {_bifItems.Count} base game items from BIF");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"ItemBrowserPanel: Failed to load BIF items: {ex.Message}");
        }
        finally
        {
            HideLoading();
        }
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
