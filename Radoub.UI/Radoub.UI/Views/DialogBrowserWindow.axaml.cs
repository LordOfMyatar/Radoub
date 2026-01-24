using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Logging;
using Radoub.UI.Services;

namespace Radoub.UI.Views;

/// <summary>
/// Simple dialog entry for the browser.
/// </summary>
public class DialogEntry
{
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public string? FilePath { get; set; }
    public bool IsFromHak { get; set; }
    public string? HakPath { get; set; }

    public string DisplayName => IsFromHak ? $"{Name} ({Source})" : Name;
}

/// <summary>
/// Cached HAK file dialog data to avoid re-scanning on each browser open.
/// </summary>
internal class DialogHakCacheEntry
{
    public string HakPath { get; set; } = "";
    public DateTime LastModified { get; set; }
    public List<DialogEntry> Dialogs { get; set; } = new();
}

public partial class DialogBrowserWindow : Window
{
    private readonly IScriptBrowserContext? _context;
    private List<DialogEntry> _moduleDialogs = new();
    private List<DialogEntry> _hakDialogs = new();
    private string? _selectedDialog;
    private DialogEntry? _selectedEntry;
    private string? _overridePath;
    private bool _showHakDialogs;
    private bool _hakDialogsLoaded;
    private bool _confirmed;

    // Static cache for HAK file contents - persists across window instances
    private static readonly Dictionary<string, DialogHakCacheEntry> _hakCache = new();

    /// <summary>
    /// Gets the selected dialog name. Only valid if confirmed (OK or double-click).
    /// </summary>
    public string? SelectedDialog => _confirmed ? _selectedDialog : null;

    /// <summary>
    /// Gets the full selected entry with file path info. Only valid if confirmed.
    /// </summary>
    public DialogEntry? SelectedEntry => _confirmed ? _selectedEntry : null;

    // Parameterless constructor for XAML designer/runtime loader
    public DialogBrowserWindow() : this(null)
    {
    }

    public DialogBrowserWindow(IScriptBrowserContext? context)
    {
        InitializeComponent();
        _context = context;

        UpdateLocationDisplay();
        LoadDialogs();
    }

    private void UpdateLocationDisplay()
    {
        if (!string.IsNullOrEmpty(_overridePath))
        {
            LocationPathLabel.Text = UnifiedLogger.SanitizePath(_overridePath);
            LocationPathLabel.Foreground = new SolidColorBrush(Colors.White);
            ResetLocationButton.IsVisible = true;
        }
        else
        {
            var currentDir = _context?.CurrentFileDirectory;
            if (!string.IsNullOrEmpty(currentDir))
            {
                LocationPathLabel.Text = UnifiedLogger.SanitizePath(currentDir);
                LocationPathLabel.Foreground = new SolidColorBrush(Colors.LightGray);
            }
            else
            {
                LocationPathLabel.Text = "(no file loaded - use browse...)";
                LocationPathLabel.Foreground = new SolidColorBrush(Colors.Orange);
            }
            ResetLocationButton.IsVisible = false;
        }
    }

    private string? GetCurrentDirectory()
    {
        return _overridePath ?? _context?.CurrentFileDirectory;
    }

    private async void OnBrowseLocationClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            IStorageFolder? suggestedStart = null;
            var currentDir = GetCurrentDirectory();
            if (!string.IsNullOrEmpty(currentDir))
            {
                var parentDir = Path.GetDirectoryName(currentDir);
                if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                {
                    suggestedStart = await StorageProvider.TryGetFolderFromPathAsync(parentDir);
                }
            }

            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Dialog Location",
                AllowMultiple = false,
                SuggestedStartLocation = suggestedStart
            });

            if (folders.Count > 0)
            {
                var folder = folders[0];
                _overridePath = folder.Path.LocalPath;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Dialog browser: Override path set to {UnifiedLogger.SanitizePath(_overridePath)}");

                UpdateLocationDisplay();
                await LoadDialogsAsync();
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error selecting folder: {ex.Message}");
        }
    }

    private async void OnResetLocationClick(object? sender, RoutedEventArgs e)
    {
        _overridePath = null;
        UnifiedLogger.LogApplication(LogLevel.INFO, "Dialog browser: Reset to auto-detected paths");
        UpdateLocationDisplay();
        await LoadDialogsAsync();
    }

    private async void OnShowHakChanged(object? sender, RoutedEventArgs e)
    {
        _showHakDialogs = ShowHakCheckBox.IsChecked == true;
        UnifiedLogger.LogApplication(LogLevel.INFO, $"Dialog browser: Show HAK dialogs = {_showHakDialogs}");

        if (_showHakDialogs && !_hakDialogsLoaded)
        {
            await LoadHakDialogsAsync();
        }

        UpdateDialogList();
    }

    private async void LoadDialogs()
    {
        await LoadDialogsAsync();
    }

    private async Task LoadDialogsAsync()
    {
        try
        {
            var currentDir = GetCurrentDirectory();
            if (!string.IsNullOrEmpty(currentDir))
            {
                _moduleDialogs = await LoadDialogsFromPathAsync(currentDir);
            }
            else
            {
                _moduleDialogs = new List<DialogEntry>();
            }

            UpdateDialogList();
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load dialogs: {ex.Message}");
            DialogCountLabel.Text = $"Error loading dialogs: {ex.Message}";
            DialogCountLabel.Foreground = new SolidColorBrush(Colors.Red);
        }
    }

    private async Task LoadHakDialogsAsync()
    {
        try
        {
            _hakDialogs = new List<DialogEntry>();

            var hakPaths = new List<string>();

            // Current file directory
            var currentDir = GetCurrentDirectory();
            if (!string.IsNullOrEmpty(currentDir))
            {
                hakPaths.AddRange(GetHakFilesFromPath(currentDir));
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
                UnifiedLogger.LogApplication(LogLevel.INFO, "Dialog Browser: No HAK files found to scan");
                _hakDialogsLoaded = true;
                return;
            }

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Dialog Browser: Scanning {hakPaths.Count} HAK files for dialogs");

            for (int i = 0; i < hakPaths.Count; i++)
            {
                var hakPath = hakPaths[i];
                var hakName = Path.GetFileName(hakPath);
                DialogCountLabel.Text = $"Loading HAK {i + 1}/{hakPaths.Count}: {hakName}...";

                await Task.Run(() => ScanHakForDialogs(hakPath));
            }

            _hakDialogs = _hakDialogs.OrderBy(d => d.Name).ToList();
            _hakDialogsLoaded = true;

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Dialog Browser: Loaded {_hakDialogs.Count} dialogs from HAK files");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load HAK dialogs: {ex.Message}");
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
                    if (_moduleDialogs.Any(d => d.Name.Equals(dialog.Name, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    if (_hakDialogs.Any(d => d.Name.Equals(dialog.Name, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    _hakDialogs.Add(new DialogEntry
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
                Dialogs = new List<DialogEntry>()
            };

            foreach (var resource in dlgResources)
            {
                var dialogEntry = new DialogEntry
                {
                    Name = resource.ResRef,
                    Source = $"HAK: {hakFileName}",
                    IsFromHak = true,
                    HakPath = hakPath
                };

                newCacheEntry.Dialogs.Add(dialogEntry);

                if (_moduleDialogs.Any(d => d.Name.Equals(resource.ResRef, StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (_hakDialogs.Any(d => d.Name.Equals(resource.ResRef, StringComparison.OrdinalIgnoreCase)))
                    continue;

                _hakDialogs.Add(dialogEntry);
            }

            _hakCache[hakPath] = newCacheEntry;

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Dialog Browser: Scanned and cached {dlgResources.Count} dialogs in {hakFileName}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning HAK {UnifiedLogger.SanitizePath(hakPath)}: {ex.Message}");
        }
    }

    private Task<List<DialogEntry>> LoadDialogsFromPathAsync(string path)
    {
        var dialogs = new List<DialogEntry>();

        try
        {
            if (Directory.Exists(path))
            {
                var dialogFiles = Directory.GetFiles(path, "*.dlg", SearchOption.AllDirectories);

                foreach (var dialogFile in dialogFiles)
                {
                    var dialogName = Path.GetFileNameWithoutExtension(dialogFile);
                    if (!dialogs.Any(d => d.Name.Equals(dialogName, StringComparison.OrdinalIgnoreCase)))
                    {
                        dialogs.Add(new DialogEntry
                        {
                            Name = dialogName,
                            Source = "Module",
                            FilePath = dialogFile,
                            IsFromHak = false
                        });
                    }
                }

                dialogs = dialogs.OrderBy(d => d.Name).ToList();
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Dialog Browser: Found {dialogs.Count} module dialogs");
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning path for dialogs: {ex.Message}");
        }

        return Task.FromResult(dialogs);
    }

    private void UpdateDialogList()
    {
        DialogListBox.Items.Clear();

        var searchText = SearchBox?.Text?.ToLowerInvariant();

        var allDialogs = _moduleDialogs.ToList();
        if (_showHakDialogs)
        {
            foreach (var hakDialog in _hakDialogs)
            {
                if (!allDialogs.Any(d => d.Name.Equals(hakDialog.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    allDialogs.Add(hakDialog);
                }
            }
        }

        var dialogsToDisplay = allDialogs;

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            dialogsToDisplay = allDialogs
                .Where(d => d.Name.ToLowerInvariant().Contains(searchText))
                .ToList();
        }

        // Sort: module dialogs first, then HAK, alphabetical
        dialogsToDisplay = dialogsToDisplay
            .OrderBy(d => d.IsFromHak ? 1 : 0)
            .ThenBy(d => d.Name)
            .ToList();

        foreach (var dialog in dialogsToDisplay)
        {
            DialogListBox.Items.Add(dialog);
        }

        // Update count label
        var moduleCount = dialogsToDisplay.Count(d => !d.IsFromHak);
        var hakCount = dialogsToDisplay.Count(d => d.IsFromHak);

        if (dialogsToDisplay.Count == 0)
        {
            if (string.IsNullOrEmpty(GetCurrentDirectory()))
            {
                DialogCountLabel.Text = "No dialogs found - use browse... to select folder";
            }
            else
            {
                DialogCountLabel.Text = "No matching dialogs";
            }
            DialogCountLabel.Foreground = new SolidColorBrush(Colors.Orange);
        }
        else
        {
            var countText = $"{moduleCount} module";
            if (hakCount > 0)
            {
                countText += $" + {hakCount} HAK";
            }
            DialogCountLabel.Text = countText;
            DialogCountLabel.Foreground = new SolidColorBrush(Colors.White);
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateDialogList();
    }

    private void OnDialogSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (DialogListBox.SelectedItem is DialogEntry dialogEntry)
        {
            _selectedDialog = dialogEntry.Name;
            _selectedEntry = dialogEntry;
            SelectedDialogLabel.Text = dialogEntry.DisplayName;
        }
        else
        {
            _selectedDialog = null;
            _selectedEntry = null;
            SelectedDialogLabel.Text = "(none)";
        }
    }

    private void OnDialogDoubleClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedDialog != null)
        {
            _confirmed = true;
            Close(_selectedDialog);
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        _confirmed = true;
        Close(_selectedDialog);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        // Don't set _confirmed - SelectedDialog will return null
        Close(null);
    }

    #region Title Bar Events

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnTitleBarDoubleTapped(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    #endregion
}
