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
using Radoub.Formats.Logging;

namespace Radoub.UI.Views;

/// <summary>
/// Entry representing a module file.
/// </summary>
public class ModuleEntry
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public long SizeBytes { get; set; }

    public string DisplayName => Name;
    public string SizeText => FormatSize(SizeBytes);

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}

/// <summary>
/// Browser window for selecting NWN module files (.mod).
/// </summary>
public partial class ModuleBrowserWindow : Window
{
    private readonly string? _defaultModulesPath;
    private List<ModuleEntry> _modules = new();
    private string? _selectedModulePath;
    private string? _overridePath;
    private bool _confirmed;

    /// <summary>
    /// Gets the selected module path. Only valid if confirmed (OK or double-click).
    /// </summary>
    public string? SelectedModulePath => _confirmed ? _selectedModulePath : null;

    // Parameterless constructor for XAML designer/runtime loader
    public ModuleBrowserWindow() : this(null)
    {
    }

    /// <summary>
    /// Creates a new ModuleBrowserWindow.
    /// </summary>
    /// <param name="nwnUserPath">Path to NWN user directory (e.g., ~/Neverwinter Nights)</param>
    public ModuleBrowserWindow(string? nwnUserPath)
    {
        InitializeComponent();

        if (!string.IsNullOrEmpty(nwnUserPath))
        {
            _defaultModulesPath = Path.Combine(nwnUserPath, "modules");
        }

        UpdateLocationDisplay();
        LoadModules();
    }

    private void UpdateLocationDisplay()
    {
        if (!string.IsNullOrEmpty(_overridePath))
        {
            LocationPathLabel.Text = UnifiedLogger.SanitizePath(_overridePath);
            LocationPathLabel.Foreground = new SolidColorBrush(Colors.White);
            ResetLocationButton.IsVisible = true;
        }
        else if (!string.IsNullOrEmpty(_defaultModulesPath) && Directory.Exists(_defaultModulesPath))
        {
            LocationPathLabel.Text = UnifiedLogger.SanitizePath(_defaultModulesPath);
            LocationPathLabel.Foreground = new SolidColorBrush(Colors.LightGray);
            ResetLocationButton.IsVisible = false;
        }
        else
        {
            LocationPathLabel.Text = "(no modules folder found - use browse...)";
            LocationPathLabel.Foreground = new SolidColorBrush(Colors.Orange);
            ResetLocationButton.IsVisible = false;
        }
    }

    private string? GetCurrentDirectory()
    {
        return _overridePath ?? _defaultModulesPath;
    }

    private async void OnBrowseLocationClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            IStorageFolder? suggestedStart = null;
            var currentDir = GetCurrentDirectory();
            if (!string.IsNullOrEmpty(currentDir) && Directory.Exists(currentDir))
            {
                suggestedStart = await StorageProvider.TryGetFolderFromPathAsync(currentDir);
            }

            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Modules Folder",
                AllowMultiple = false,
                SuggestedStartLocation = suggestedStart
            });

            if (folders.Count > 0)
            {
                var folder = folders[0];
                _overridePath = folder.Path.LocalPath;
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Module browser: Override path set to {UnifiedLogger.SanitizePath(_overridePath)}");

                UpdateLocationDisplay();
                await LoadModulesAsync();
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
        UnifiedLogger.LogApplication(LogLevel.INFO, "Module browser: Reset to default modules path");
        UpdateLocationDisplay();
        await LoadModulesAsync();
    }

    private async void LoadModules()
    {
        await LoadModulesAsync();
    }

    private Task LoadModulesAsync()
    {
        try
        {
            var currentDir = GetCurrentDirectory();
            if (!string.IsNullOrEmpty(currentDir) && Directory.Exists(currentDir))
            {
                _modules = LoadModulesFromPath(currentDir);
            }
            else
            {
                _modules = new List<ModuleEntry>();
            }

            UpdateModuleList();
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load modules: {ex.Message}");
            ModuleCountLabel.Text = $"Error loading modules: {ex.Message}";
            ModuleCountLabel.Foreground = new SolidColorBrush(Colors.Red);
        }

        return Task.CompletedTask;
    }

    private List<ModuleEntry> LoadModulesFromPath(string path)
    {
        var modules = new List<ModuleEntry>();

        try
        {
            if (Directory.Exists(path))
            {
                var modFiles = Directory.GetFiles(path, "*.mod", SearchOption.TopDirectoryOnly);

                foreach (var modFile in modFiles)
                {
                    var fileInfo = new FileInfo(modFile);
                    modules.Add(new ModuleEntry
                    {
                        Name = Path.GetFileNameWithoutExtension(modFile),
                        FullPath = modFile,
                        SizeBytes = fileInfo.Length
                    });
                }

                modules = modules.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Module Browser: Found {modules.Count} modules");
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning path for modules: {ex.Message}");
        }

        return modules;
    }

    private void UpdateModuleList()
    {
        ModuleListBox.ItemsSource = null;

        var searchText = SearchBox?.Text?.ToLowerInvariant();
        var modulesToDisplay = _modules;

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            modulesToDisplay = _modules
                .Where(m => m.Name.ToLowerInvariant().Contains(searchText))
                .ToList();
        }

        ModuleListBox.ItemsSource = modulesToDisplay;

        // Update count label
        if (modulesToDisplay.Count == 0)
        {
            if (string.IsNullOrEmpty(GetCurrentDirectory()))
            {
                ModuleCountLabel.Text = "No modules folder configured - use browse...";
            }
            else if (!string.IsNullOrWhiteSpace(searchText))
            {
                ModuleCountLabel.Text = "No matching modules";
            }
            else
            {
                ModuleCountLabel.Text = "No .mod files found";
            }
            ModuleCountLabel.Foreground = new SolidColorBrush(Colors.Orange);
        }
        else
        {
            ModuleCountLabel.Text = $"{modulesToDisplay.Count} module{(modulesToDisplay.Count == 1 ? "" : "s")}";
            ModuleCountLabel.Foreground = new SolidColorBrush(Colors.White);
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateModuleList();
    }

    private void OnModuleSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (ModuleListBox.SelectedItem is ModuleEntry moduleEntry)
        {
            _selectedModulePath = moduleEntry.FullPath;
            SelectedModuleLabel.Text = moduleEntry.DisplayName;
        }
        else
        {
            _selectedModulePath = null;
            SelectedModuleLabel.Text = "(none)";
        }
    }

    private void OnModuleDoubleClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedModulePath != null)
        {
            _confirmed = true;
            Close(_selectedModulePath);
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        _confirmed = true;
        Close(_selectedModulePath);
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

    private void OnTitleBarDoubleTapped(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    #endregion
}
