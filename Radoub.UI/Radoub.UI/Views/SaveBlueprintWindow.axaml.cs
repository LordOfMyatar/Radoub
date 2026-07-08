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
/// Entry for the save-blueprint file list, containing file info.
/// </summary>
public class SaveBlueprintEntry
{
    public string Name { get; set; } = "";
    public string? FilePath { get; set; }
}

/// <summary>
/// Shared internal Save dialog for blueprint documents (#2515). Defaults to the
/// active module directory (via IScriptBrowserContext.CurrentFileDirectory),
/// lists existing blueprints of the selected extension, validates the filename
/// inline against Aurora rules, and offers a deliberate Browse override.
/// Mirrors the structure of StoreBrowserWindow.
/// </summary>
public partial class SaveBlueprintWindow : Window
{
    private readonly IScriptBrowserContext? _context;
    private readonly IReadOnlyList<string> _extensions;
    private List<SaveBlueprintEntry> _allFiles = new();
    private List<SaveBlueprintEntry> _filteredFiles = new();
    private string? _overridePath;
    private SaveBlueprintResult? _result;
    private bool _confirmed;

    /// <summary>
    /// Gets the confirmed save result. Null until Save is confirmed (or cancelled).
    /// </summary>
    public SaveBlueprintResult? Result => _confirmed ? _result : null;

    // Parameterless ctor for designer only.
    public SaveBlueprintWindow() : this(new SaveBlueprintOptions("Save Blueprint", new[] { "utm" }, "", null))
    {
    }

    public SaveBlueprintWindow(SaveBlueprintOptions options)
    {
        InitializeComponent();

        _context = options.Context;
        _extensions = options.Extensions != null && options.Extensions.Count > 0
            ? options.Extensions
            : new[] { "utm" };

        Title = options.Title;
        TitleBarLabel.Text = options.Title;

        // Extension label vs combo box
        if (_extensions.Count == 1)
        {
            ExtensionLabel.Text = "." + _extensions[0];
            ExtensionLabel.IsVisible = true;
            ExtensionCombo.IsVisible = false;
        }
        else
        {
            ExtensionCombo.ItemsSource = _extensions.Select(e => "." + e).ToList();
            ExtensionCombo.SelectedIndex = 0;
            ExtensionCombo.IsVisible = true;
            ExtensionLabel.IsVisible = false;
        }

        FileNameBox.Text = options.DefaultResRef ?? "";

        UpdateLocationDisplay();
        LoadFiles();
        ValidateFileName();
    }

    private string SelectedExtension()
    {
        if (_extensions.Count > 1 && ExtensionCombo.SelectedIndex >= 0)
        {
            return _extensions[ExtensionCombo.SelectedIndex];
        }
        return _extensions[0];
    }

    private void UpdateLocationDisplay()
    {
        if (!string.IsNullOrEmpty(_overridePath))
        {
            LocationPathLabel.Text = UnifiedLogger.SanitizePath(_overridePath);
            LocationPathLabel.Foreground = this.FindResource("SystemControlForegroundBaseHighBrush") as IBrush ?? BrushManager.GetInfoBrush();
            ResetLocationButton.IsVisible = true;
        }
        else
        {
            var currentDir = _context?.CurrentFileDirectory;
            if (!string.IsNullOrEmpty(currentDir))
            {
                LocationPathLabel.Text = UnifiedLogger.SanitizePath(currentDir);
                LocationPathLabel.Foreground = this.FindResource("SystemControlForegroundBaseMediumBrush") as IBrush ?? BrushManager.GetDisabledBrush();
            }
            else
            {
                LocationPathLabel.Text = "(no module loaded - use browse...)";
                LocationPathLabel.Foreground = BrushManager.GetWarningBrush(this);
            }
            ResetLocationButton.IsVisible = false;
        }
    }

    private string? GetCurrentDirectory()
    {
        return SaveBlueprintPathResolver.ResolveDirectory(_overridePath, _context?.CurrentFileDirectory);
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
                Title = "Select Save Folder",
                AllowMultiple = false,
                SuggestedStartLocation = suggestedStart
            });

            if (folders.Count > 0)
            {
                var folder = folders[0];
                _overridePath = folder.Path.LocalPath;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Save blueprint: Override path set to {UnifiedLogger.SanitizePath(_overridePath)}");

                UpdateLocationDisplay();
                LoadFiles();
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
        UnifiedLogger.LogApplication(LogLevel.INFO, "Save blueprint: Reset to auto-detected path");
        UpdateLocationDisplay();
        LoadFiles();
    }

    private void LoadFiles()
    {
        _allFiles.Clear();
        _filteredFiles.Clear();
        FileListBox.Items.Clear();

        var currentDir = GetCurrentDirectory();
        if (string.IsNullOrEmpty(currentDir) || !Directory.Exists(currentDir))
        {
            FileCountLabel.Text = "No module folder selected";
            FileCountLabel.Foreground = BrushManager.GetWarningBrush(this);
            return;
        }

        try
        {
            var ext = SelectedExtension();
            var files = Directory.GetFiles(currentDir, $"*.{ext}", SearchOption.TopDirectoryOnly);

            foreach (var file in files)
            {
                _allFiles.Add(new SaveBlueprintEntry
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    FilePath = file
                });
            }

            _allFiles = _allFiles.OrderBy(f => f.Name).ToList();
            ApplyFilter();

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Save Blueprint: Found {_allFiles.Count} .{ext} files in {UnifiedLogger.SanitizePath(currentDir)}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning for blueprints: {ex.Message}");
            FileCountLabel.Text = $"Error: {ex.Message}";
            FileCountLabel.Foreground = BrushManager.GetErrorBrush(this);
        }
    }

    private void ApplyFilter()
    {
        FileListBox.Items.Clear();

        var searchText = SearchBox?.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(searchText))
        {
            _filteredFiles = _allFiles.ToList();
        }
        else
        {
            _filteredFiles = _allFiles
                .Where(f => f.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        foreach (var file in _filteredFiles)
        {
            FileListBox.Items.Add(file);
        }

        var ext = SelectedExtension();
        if (_filteredFiles.Count == 0)
        {
            FileCountLabel.Text = _allFiles.Count == 0
                ? $"No .{ext} files found in folder"
                : "No matches for filter";
            FileCountLabel.Foreground = BrushManager.GetWarningBrush(this);
        }
        else
        {
            FileCountLabel.Text = $"{_filteredFiles.Count} file{(_filteredFiles.Count == 1 ? "" : "s")}";
            FileCountLabel.Foreground = this.FindResource("SystemControlForegroundBaseHighBrush") as IBrush ?? BrushManager.GetInfoBrush();
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void OnExtensionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Changing the selected extension re-scans the list for that extension.
        LoadFiles();
        ValidateFileName();
    }

    private void OnFileSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (FileListBox.SelectedItem is SaveBlueprintEntry entry)
        {
            // Copy the bare name into the File-name box so users can overwrite.
            FileNameBox.Text = entry.Name;
        }
    }

    private void OnFileNameChanged(object? sender, TextChangedEventArgs e)
    {
        ValidateFileName();
    }

    private bool ValidateFileName()
    {
        var name = FileNameBox?.Text?.Trim() ?? "";
        var valid = SaveBlueprintPathResolver.IsValidAuroraFilename(name);

        if (!valid)
        {
            ValidationLabel.Text = string.IsNullOrEmpty(name)
                ? "Enter a file name."
                : "File name must be 1-16 characters: letters, numbers, or underscore (no spaces or symbols).";
            ValidationLabel.Foreground = BrushManager.GetWarningBrush(this);
            ValidationLabel.IsVisible = true;
        }
        else
        {
            ValidationLabel.Text = "";
            ValidationLabel.IsVisible = false;
        }

        if (SaveButton != null)
        {
            SaveButton.IsEnabled = valid;
        }
        return valid;
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (!ValidateFileName())
        {
            return;
        }

        var dir = GetCurrentDirectory();
        if (string.IsNullOrEmpty(dir))
        {
            ValidationLabel.Text = "No folder selected. Use Browse... to choose a save location.";
            ValidationLabel.Foreground = BrushManager.GetWarningBrush(this);
            ValidationLabel.IsVisible = true;
            return;
        }

        var resRef = FileNameBox.Text!.Trim();
        var ext = SelectedExtension();
        var fullPath = SaveBlueprintPathResolver.ComposePath(dir, resRef, ext);

        if (SaveBlueprintPathResolver.WouldOverwrite(fullPath))
        {
            var confirm = await DialogHelper.ShowOkCancelAsync(
                this,
                "Overwrite?",
                $"{resRef}.{ext} already exists in this folder. Overwrite it?");
            if (!confirm)
            {
                return;
            }
        }

        _result = new SaveBlueprintResult(fullPath, ext);
        _confirmed = true;
        Close(_result);
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
