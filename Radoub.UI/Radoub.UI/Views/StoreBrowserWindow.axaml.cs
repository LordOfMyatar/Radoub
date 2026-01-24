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
/// Entry for the store browser, containing file info.
/// </summary>
public class StoreBrowserEntry
{
    public string Name { get; set; } = "";
    public string? FilePath { get; set; }
}

/// <summary>
/// Browser window for selecting store/merchant files (.utm) from a module directory.
/// </summary>
public partial class StoreBrowserWindow : Window
{
    private readonly IScriptBrowserContext? _context;
    private List<StoreBrowserEntry> _allStores = new();
    private List<StoreBrowserEntry> _filteredStores = new();
    private StoreBrowserEntry? _selectedEntry;
    private string? _overridePath;
    private bool _confirmed;

    /// <summary>
    /// Gets the full selected entry with file path info. Only valid if confirmed.
    /// </summary>
    public StoreBrowserEntry? SelectedEntry => _confirmed ? _selectedEntry : null;

    public StoreBrowserWindow() : this(null)
    {
    }

    public StoreBrowserWindow(IScriptBrowserContext? context)
    {
        InitializeComponent();
        _context = context;

        UpdateLocationDisplay();
        LoadStores();
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
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Store browser: Override path set to {UnifiedLogger.SanitizePath(_overridePath)}");

                UpdateLocationDisplay();
                LoadStores();
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
        UnifiedLogger.LogApplication(LogLevel.INFO, "Store browser: Reset to auto-detected path");
        UpdateLocationDisplay();
        LoadStores();
    }

    private void LoadStores()
    {
        _allStores.Clear();
        _filteredStores.Clear();
        StoreListBox.Items.Clear();

        var currentDir = GetCurrentDirectory();
        if (string.IsNullOrEmpty(currentDir) || !Directory.Exists(currentDir))
        {
            StoreCountLabel.Text = "No module folder selected";
            StoreCountLabel.Foreground = new SolidColorBrush(Colors.Orange);
            return;
        }

        try
        {
            var storeFiles = Directory.GetFiles(currentDir, "*.utm", SearchOption.TopDirectoryOnly);

            foreach (var storeFile in storeFiles)
            {
                var storeName = Path.GetFileNameWithoutExtension(storeFile);
                _allStores.Add(new StoreBrowserEntry
                {
                    Name = storeName,
                    FilePath = storeFile
                });
            }

            _allStores = _allStores.OrderBy(s => s.Name).ToList();

            ApplyFilter();

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Store Browser: Found {_allStores.Count} stores in {UnifiedLogger.SanitizePath(currentDir)}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning for stores: {ex.Message}");
            StoreCountLabel.Text = $"Error: {ex.Message}";
            StoreCountLabel.Foreground = new SolidColorBrush(Colors.Red);
        }
    }

    private void ApplyFilter()
    {
        StoreListBox.Items.Clear();

        var searchText = SearchBox?.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(searchText))
        {
            _filteredStores = _allStores.ToList();
        }
        else
        {
            _filteredStores = _allStores
                .Where(s => s.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        foreach (var store in _filteredStores)
        {
            StoreListBox.Items.Add(store);
        }

        if (_filteredStores.Count == 0)
        {
            StoreCountLabel.Text = _allStores.Count == 0
                ? "No .utm files found in module folder"
                : "No matches for filter";
            StoreCountLabel.Foreground = new SolidColorBrush(Colors.Orange);
        }
        else
        {
            StoreCountLabel.Text = $"{_filteredStores.Count} store{(_filteredStores.Count == 1 ? "" : "s")}";
            StoreCountLabel.Foreground = new SolidColorBrush(Colors.White);
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void OnStoreSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (StoreListBox.SelectedItem is StoreBrowserEntry entry)
        {
            _selectedEntry = entry;
            SelectedStoreLabel.Text = entry.Name;
        }
        else
        {
            _selectedEntry = null;
            SelectedStoreLabel.Text = "(none)";
        }
    }

    private void OnStoreDoubleClicked(object? sender, RoutedEventArgs e)
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
