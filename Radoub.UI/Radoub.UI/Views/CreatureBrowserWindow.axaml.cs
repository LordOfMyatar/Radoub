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
    public string TypeIndicator => IsBic ? "[BIC]" : "[UTC]";
}

/// <summary>
/// Browser window for selecting creature files (.utc/.bic) from a module directory.
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
        if (string.IsNullOrEmpty(currentDir) || !Directory.Exists(currentDir))
        {
            CreatureCountLabel.Text = "No module folder selected";
            CreatureCountLabel.Foreground = new SolidColorBrush(Colors.Orange);
            return;
        }

        try
        {
            // Load UTC files
            var utcFiles = Directory.GetFiles(currentDir, "*.utc", SearchOption.TopDirectoryOnly);
            foreach (var file in utcFiles)
            {
                _allCreatures.Add(new CreatureBrowserEntry
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    FilePath = file,
                    IsBic = false
                });
            }

            // Load BIC files
            var bicFiles = Directory.GetFiles(currentDir, "*.bic", SearchOption.TopDirectoryOnly);
            foreach (var file in bicFiles)
            {
                _allCreatures.Add(new CreatureBrowserEntry
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    FilePath = file,
                    IsBic = true
                });
            }

            _allCreatures = _allCreatures.OrderBy(c => c.Name).ToList();

            ApplyFilter();

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Creature Browser: Found {_allCreatures.Count} creatures in {UnifiedLogger.SanitizePath(currentDir)}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning for creatures: {ex.Message}");
            CreatureCountLabel.Text = $"Error: {ex.Message}";
            CreatureCountLabel.Foreground = new SolidColorBrush(Colors.Red);
        }
    }

    private void ApplyFilter()
    {
        CreatureListBox.Items.Clear();

        bool showUtc = ShowAllRadio.IsChecked == true || ShowUtcRadio.IsChecked == true;
        bool showBic = ShowAllRadio.IsChecked == true || ShowBicRadio.IsChecked == true;

        _filteredCreatures = _allCreatures
            .Where(c => (showUtc && !c.IsBic) || (showBic && c.IsBic))
            .ToList();

        foreach (var creature in _filteredCreatures)
        {
            CreatureListBox.Items.Add(creature);
        }

        if (_filteredCreatures.Count == 0)
        {
            CreatureCountLabel.Text = "No creature files found";
            CreatureCountLabel.Foreground = new SolidColorBrush(Colors.Orange);
        }
        else
        {
            var utcCount = _filteredCreatures.Count(c => !c.IsBic);
            var bicCount = _filteredCreatures.Count(c => c.IsBic);

            if (ShowAllRadio.IsChecked == true)
            {
                CreatureCountLabel.Text = $"{_filteredCreatures.Count} creature{(_filteredCreatures.Count == 1 ? "" : "s")} ({utcCount} UTC, {bicCount} BIC)";
            }
            else
            {
                CreatureCountLabel.Text = $"{_filteredCreatures.Count} creature{(_filteredCreatures.Count == 1 ? "" : "s")}";
            }
            CreatureCountLabel.Foreground = new SolidColorBrush(Colors.White);
        }
    }

    private void OnFileTypeChanged(object? sender, RoutedEventArgs e)
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
