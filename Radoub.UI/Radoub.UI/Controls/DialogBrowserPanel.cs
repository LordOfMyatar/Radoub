using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Logging;
using Radoub.UI.Services;

namespace Radoub.UI.Controls;

/// <summary>
/// Dialog-specific entry with HAK support.
/// </summary>
public class DialogBrowserEntry : FileBrowserEntry
{
}

/// <summary>
/// Cached HAK file dialog data to avoid re-scanning on each refresh.
/// </summary>
internal class DialogHakCacheEntry
{
    public string HakPath { get; set; } = "";
    public DateTime LastModified { get; set; }
    public List<DialogBrowserEntry> Dialogs { get; set; } = new();
}

/// <summary>
/// Dialog browser panel for embedding in Parley's main window.
/// Provides file list with optional HAK scanning.
/// </summary>
public class DialogBrowserPanel : FileBrowserPanelBase
{
    private readonly IScriptBrowserContext? _context;
    private readonly CheckBox _showHakCheckBox;
    private bool _showHakDialogs;
    private bool _hakDialogsLoaded;
    private List<DialogBrowserEntry> _hakDialogs = new();

    // Static cache for HAK file contents - persists across panel instances
    private static readonly Dictionary<string, DialogHakCacheEntry> _hakCache = new();

    public DialogBrowserPanel() : this(null)
    {
    }

    public DialogBrowserPanel(IScriptBrowserContext? context)
    {
        _context = context;

        FileExtension = ".dlg";
        SearchWatermark = "Type to filter dialogs...";
        HeaderTextContent = "Dialogs";

        // Create and wire up HAK checkbox
        _showHakCheckBox = new CheckBox
        {
            Content = "Show HAK",
            IsChecked = false,
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
        };
        ToolTip.SetTip(_showHakCheckBox, "Include dialogs from HAK files");
        _showHakCheckBox.IsCheckedChanged += OnShowHakChanged;

        FilterOptionsContent = _showHakCheckBox;
    }

    /// <summary>
    /// Gets or sets whether HAK dialogs are shown.
    /// </summary>
    public bool ShowHakDialogs
    {
        get => _showHakDialogs;
        set
        {
            if (_showHakDialogs != value)
            {
                _showHakDialogs = value;
                _showHakCheckBox.IsChecked = value;
            }
        }
    }

    private async void OnShowHakChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _showHakDialogs = _showHakCheckBox.IsChecked == true;
        UnifiedLogger.LogApplication(LogLevel.INFO, $"DialogBrowserPanel: Show HAK dialogs = {_showHakDialogs}");

        if (_showHakDialogs && !_hakDialogsLoaded)
        {
            await LoadHakDialogsAsync();
        }

        OnFilterOptionsChanged();
    }

    protected override async Task<List<FileBrowserEntry>> LoadFilesFromModuleAsync(string modulePath)
    {
        // Reset HAK state when module changes
        _hakDialogsLoaded = false;
        _hakDialogs.Clear();

        return await Task.Run(() =>
        {
            var entries = new List<FileBrowserEntry>();

            try
            {
                if (!Directory.Exists(modulePath))
                    return entries;

                // Scan for .dlg files (include subdirectories for module structures)
                var dialogFiles = Directory.GetFiles(modulePath, "*.dlg", SearchOption.AllDirectories);

                foreach (var file in dialogFiles)
                {
                    var dialogName = Path.GetFileNameWithoutExtension(file);
                    if (!entries.Any(e => e.Name.Equals(dialogName, StringComparison.OrdinalIgnoreCase)))
                    {
                        entries.Add(new DialogBrowserEntry
                        {
                            Name = dialogName,
                            FilePath = file,
                            Source = "Module",
                            IsFromHak = false
                        });
                    }
                }

                entries = entries.OrderBy(e => e.Name).ToList();
                UnifiedLogger.LogApplication(LogLevel.INFO, $"DialogBrowserPanel: Found {entries.Count} module dialogs");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"DialogBrowserPanel: Error loading dialogs: {ex.Message}");
            }

            return entries;
        });
    }

    protected override async Task<List<FileBrowserEntry>> LoadAdditionalFilesAsync()
    {
        if (_showHakDialogs && !_hakDialogsLoaded)
        {
            await LoadHakDialogsAsync();
        }

        return _hakDialogs.Cast<FileBrowserEntry>().ToList();
    }

    protected override IEnumerable<FileBrowserEntry> ApplyCustomFilters(IEnumerable<FileBrowserEntry> entries)
    {
        // Filter out HAK entries if not showing HAK
        if (!_showHakDialogs)
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
            return "No dialogs found";
        }

        var countText = $"{moduleCount} module";
        if (hakCount > 0)
        {
            countText += $" + {hakCount} HAK";
        }
        return countText;
    }

    private async Task LoadHakDialogsAsync()
    {
        try
        {
            _hakDialogs.Clear();

            var hakPaths = new List<string>();

            // Current module directory
            if (!string.IsNullOrEmpty(ModulePath) && Directory.Exists(ModulePath))
            {
                hakPaths.AddRange(GetHakFilesFromPath(ModulePath));
            }

            // NWN user hak folder
            var userPath = _context?.NeverwinterNightsPath;
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
                UnifiedLogger.LogApplication(LogLevel.INFO, "DialogBrowserPanel: No HAK files found to scan");
                _hakDialogsLoaded = true;
                return;
            }

            ShowLoading($"Scanning {hakPaths.Count} HAK files...");
            UnifiedLogger.LogApplication(LogLevel.INFO, $"DialogBrowserPanel: Scanning {hakPaths.Count} HAK files");

            for (int i = 0; i < hakPaths.Count; i++)
            {
                var hakPath = hakPaths[i];
                var hakName = Path.GetFileName(hakPath);
                UpdateLoadingStatus($"HAK {i + 1}/{hakPaths.Count}: {hakName}...");

                await Task.Run(() => ScanHakForDialogs(hakPath));
            }

            _hakDialogs = _hakDialogs.OrderBy(d => d.Name).ToList();
            _hakDialogsLoaded = true;

            UnifiedLogger.LogApplication(LogLevel.INFO, $"DialogBrowserPanel: Loaded {_hakDialogs.Count} dialogs from HAK files");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"DialogBrowserPanel: Failed to load HAK dialogs: {ex.Message}");
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

    private void ScanHakForDialogs(string hakPath)
    {
        try
        {
            var hakFileName = Path.GetFileName(hakPath);
            var lastModified = File.GetLastWriteTimeUtc(hakPath);

            // Check cache first
            if (_hakCache.TryGetValue(hakPath, out var cached) && cached.LastModified == lastModified)
            {
                foreach (var dialog in cached.Dialogs)
                {
                    // Skip if already have this dialog from module or another HAK
                    if (_hakDialogs.Any(d => d.Name.Equals(dialog.Name, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    _hakDialogs.Add(new DialogBrowserEntry
                    {
                        Name = dialog.Name,
                        Source = dialog.Source,
                        IsFromHak = true,
                        HakPath = dialog.HakPath
                    });
                }
                return;
            }

            // Scan HAK
            var erf = ErfReader.ReadMetadataOnly(hakPath);
            var dlgResources = erf.GetResourcesByType(ResourceTypes.Dlg).ToList();
            var newCacheEntry = new DialogHakCacheEntry
            {
                HakPath = hakPath,
                LastModified = lastModified,
                Dialogs = new List<DialogBrowserEntry>()
            };

            foreach (var resource in dlgResources)
            {
                var dialogEntry = new DialogBrowserEntry
                {
                    Name = resource.ResRef,
                    Source = $"HAK: {hakFileName}",
                    IsFromHak = true,
                    HakPath = hakPath
                };

                newCacheEntry.Dialogs.Add(dialogEntry);

                // Skip if already have this dialog
                if (_hakDialogs.Any(d => d.Name.Equals(resource.ResRef, StringComparison.OrdinalIgnoreCase)))
                    continue;

                _hakDialogs.Add(dialogEntry);
            }

            _hakCache[hakPath] = newCacheEntry;

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"DialogBrowserPanel: Cached {dlgResources.Count} dialogs from {hakFileName}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning HAK {UnifiedLogger.SanitizePath(hakPath)}: {ex.Message}");
        }
    }
}
