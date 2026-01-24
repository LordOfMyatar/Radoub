using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Radoub.Formats.Logging;
using Radoub.UI.Services;

namespace Radoub.UI.Views;

/// <summary>
/// Entry for the creature browser, containing file info.
/// </summary>
public class CreatureBrowserEntry
{
    public string Name { get; set; } = "";
    public string? FilePath { get; set; }
    public bool IsBic { get; set; }
    public string Source { get; set; } = "Module"; // "Module", "LocalVault", or "ServerVault"
    public string TypeIndicator => IsBic ? $"[BIC:{Source}]" : "[UTC:Module]";
}

/// <summary>
/// Browser window for selecting creature files (.utc/.bic) from module directory and vaults.
/// </summary>
public partial class CreatureBrowserWindow : Window
{
    private readonly IScriptBrowserContext? _context;
    private List<CreatureBrowserEntry> _allCreatures = new();
    private List<CreatureBrowserEntry> _filteredCreatures = new();
    private CreatureBrowserEntry? _selectedEntry;
    private string? _overridePath;
    private bool _confirmed;

    /// <summary>
    /// Gets the full selected entry with file path info. Only valid if confirmed.
    /// </summary>
    public CreatureBrowserEntry? SelectedEntry => _confirmed ? _selectedEntry : null;

    public CreatureBrowserWindow() : this(null)
    {
    }

    public CreatureBrowserWindow(IScriptBrowserContext? context)
    {
        InitializeComponent();
        _context = context;

        UpdateLocationDisplay();
        LoadCreatures();
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
                LocationPathLabel.Text = "(no module loaded - use browse...)";
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
                Title = "Select Module Folder",
                AllowMultiple = false,
                SuggestedStartLocation = suggestedStart
            });

            if (folders.Count > 0)
            {
                var folder = folders[0];
                _overridePath = folder.Path.LocalPath;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Creature browser: Override path set to {UnifiedLogger.SanitizePath(_overridePath)}");

                UpdateLocationDisplay();
                LoadCreatures();
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error selecting folder: {ex.Message}");
        }
    }

    private void OnResetLocationClick(object? sender, RoutedEventArgs e)
    {
        _overridePath = null;
        UnifiedLogger.LogApplication(LogLevel.INFO, "Creature browser: Reset to auto-detected path");
        UpdateLocationDisplay();
        LoadCreatures();
    }

    private void LoadCreatures()
    {
        _allCreatures.Clear();
        _filteredCreatures.Clear();
        CreatureListBox.Items.Clear();

        var currentDir = GetCurrentDirectory();
        var hasModuleDir = !string.IsNullOrEmpty(currentDir) && Directory.Exists(currentDir);
        var localVaultPath = GetLocalVaultPath();
        var hasLocalVault = !string.IsNullOrEmpty(localVaultPath) && Directory.Exists(localVaultPath);
        var serverVaultPath = GetServerVaultPath();
        var hasServerVault = !string.IsNullOrEmpty(serverVaultPath) && Directory.Exists(serverVaultPath);

        if (!hasModuleDir && !hasLocalVault && !hasServerVault)
        {
            CreatureCountLabel.Text = "No module folder or vaults found";
            CreatureCountLabel.Foreground = new SolidColorBrush(Colors.Orange);
            return;
        }

        try
        {
            // Load files from module directory
            if (hasModuleDir)
            {
                // UTC files (creature blueprints)
                var utcFiles = Directory.GetFiles(currentDir!, "*.utc", SearchOption.TopDirectoryOnly);
                foreach (var file in utcFiles)
                {
                    _allCreatures.Add(new CreatureBrowserEntry
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        FilePath = file,
                        IsBic = false,
                        Source = "Module"
                    });
                }

                // BIC files from module directory
                var bicFiles = Directory.GetFiles(currentDir!, "*.bic", SearchOption.TopDirectoryOnly);
                foreach (var file in bicFiles)
                {
                    _allCreatures.Add(new CreatureBrowserEntry
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        FilePath = file,
                        IsBic = true,
                        Source = "Module"
                    });
                }
            }

            // Load BIC files from localvault
            if (hasLocalVault)
            {
                var localVaultBics = Directory.GetFiles(localVaultPath!, "*.bic", SearchOption.TopDirectoryOnly);
                foreach (var file in localVaultBics)
                {
                    _allCreatures.Add(new CreatureBrowserEntry
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        FilePath = file,
                        IsBic = true,
                        Source = "LocalVault"
                    });
                }
            }

            // Load BIC files from servervault (scan subdirectories - each player has a folder)
            if (hasServerVault)
            {
                try
                {
                    // ServerVault has subdirectories per player
                    foreach (var playerDir in Directory.GetDirectories(serverVaultPath!))
                    {
                        var serverVaultBics = Directory.GetFiles(playerDir, "*.bic", SearchOption.TopDirectoryOnly);
                        foreach (var file in serverVaultBics)
                        {
                            var playerName = Path.GetFileName(playerDir);
                            _allCreatures.Add(new CreatureBrowserEntry
                            {
                                Name = $"{Path.GetFileNameWithoutExtension(file)} ({playerName})",
                                FilePath = file,
                                IsBic = true,
                                Source = "ServerVault"
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Error scanning servervault: {ex.Message}");
                }
            }

            _allCreatures = _allCreatures.OrderBy(c => c.Name).ToList();

            ApplyFilter();

            var sources = new List<string>();
            if (hasModuleDir && currentDir != null)
            {
                var sanitized = UnifiedLogger.SanitizePath(currentDir);
                if (sanitized != null) sources.Add(sanitized);
            }
            if (hasLocalVault) sources.Add("localvault");
            if (hasServerVault) sources.Add("servervault");
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Creature Browser: Found {_allCreatures.Count} creatures from {string.Join(", ", sources)}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning for creatures: {ex.Message}");
            CreatureCountLabel.Text = $"Error: {ex.Message}";
            CreatureCountLabel.Foreground = new SolidColorBrush(Colors.Red);
        }
    }

    /// <summary>
    /// Get the localvault path for player character BIC files.
    /// </summary>
    private string? GetLocalVaultPath()
    {
        var nwnPath = _context?.NeverwinterNightsPath;
        if (string.IsNullOrEmpty(nwnPath))
            return null;

        var localVault = Path.Combine(nwnPath, "localvault");
        return Directory.Exists(localVault) ? localVault : null;
    }

    /// <summary>
    /// Get the servervault path for server-stored player character BIC files.
    /// </summary>
    private string? GetServerVaultPath()
    {
        var nwnPath = _context?.NeverwinterNightsPath;
        if (string.IsNullOrEmpty(nwnPath))
            return null;

        var serverVault = Path.Combine(nwnPath, "servervault");
        return Directory.Exists(serverVault) ? serverVault : null;
    }

    private void ApplyFilter()
    {
        CreatureListBox.Items.Clear();

        bool showModule = ShowModuleCheck.IsChecked == true;
        bool showLocalVault = ShowLocalVaultCheck.IsChecked == true;
        bool showServerVault = ShowServerVaultCheck.IsChecked == true;
        var searchText = SearchBox?.Text?.Trim() ?? "";

        _filteredCreatures = _allCreatures
            .Where(c =>
            {
                // Source filter
                var sourceMatch = (c.Source == "Module" && showModule) ||
                                  (c.Source == "LocalVault" && showLocalVault) ||
                                  (c.Source == "ServerVault" && showServerVault);
                if (!sourceMatch) return false;

                // Search filter
                if (!string.IsNullOrEmpty(searchText))
                {
                    return c.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase);
                }

                return true;
            })
            .ToList();

        foreach (var creature in _filteredCreatures)
        {
            CreatureListBox.Items.Add(creature);
        }

        if (_filteredCreatures.Count == 0)
        {
            CreatureCountLabel.Text = _allCreatures.Count == 0
                ? "No creature files found"
                : "No matches for filter";
            CreatureCountLabel.Foreground = new SolidColorBrush(Colors.Orange);
        }
        else
        {
            var moduleCount = _filteredCreatures.Count(c => c.Source == "Module");
            var localVaultCount = _filteredCreatures.Count(c => c.Source == "LocalVault");
            var serverVaultCount = _filteredCreatures.Count(c => c.Source == "ServerVault");

            var parts = new List<string>();
            if (moduleCount > 0) parts.Add($"{moduleCount} module");
            if (localVaultCount > 0) parts.Add($"{localVaultCount} localvault");
            if (serverVaultCount > 0) parts.Add($"{serverVaultCount} servervault");

            CreatureCountLabel.Text = $"{_filteredCreatures.Count} creature{(_filteredCreatures.Count == 1 ? "" : "s")} ({string.Join(", ", parts)})";
            CreatureCountLabel.Foreground = new SolidColorBrush(Colors.White);
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void OnSourceFilterChanged(object? sender, RoutedEventArgs e)
    {
        ApplyFilter();
    }

    private void OnCreatureSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (CreatureListBox.SelectedItem is CreatureBrowserEntry entry)
        {
            _selectedEntry = entry;
            SelectedCreatureLabel.Text = $"{entry.Name} ({entry.TypeIndicator})";
        }
        else
        {
            _selectedEntry = null;
            SelectedCreatureLabel.Text = "(none)";
        }
    }

    private void OnCreatureDoubleClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedEntry != null)
        {
            _confirmed = true;
            Close(_selectedEntry.Name);
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        _confirmed = true;
        Close(_selectedEntry?.Name);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
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

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    #endregion
}
