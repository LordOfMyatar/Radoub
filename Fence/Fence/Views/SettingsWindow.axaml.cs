using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using MerchantEditor.Services;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.UI.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MerchantEditor.Views;

public partial class SettingsWindow : Window
{
    private bool _isInitializing = true;
    private MainWindow? _mainWindow;

    public SettingsWindow()
    {
        InitializeComponent();

        LoadSettings();
        PopulateThemes();
        PopulateFontFamilies();
        UpdateCacheInfo();

        _isInitializing = false;
    }

    /// <summary>
    /// Set the main window reference for cache operations.
    /// </summary>
    public void SetMainWindow(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
        UpdateCacheInfo();
    }

    private void UpdateCacheInfo()
    {
        var cacheInfo = _mainWindow?.GetPaletteCacheInfo();

        if (cacheInfo != null)
        {
            CacheStatusText.Text = "Cached";
            CacheStatusText.Foreground = GetSuccessBrush();
            CacheItemCountText.Text = $"{cacheInfo.ItemCount:N0}";
            CacheSizeText.Text = $"{cacheInfo.FileSizeKB:N1} KB";
            CacheCreatedText.Text = cacheInfo.CreatedAt.ToLocalTime().ToString("g");
        }
        else
        {
            CacheStatusText.Text = "No cache";
            CacheStatusText.Foreground = GetWarningBrush();
            CacheItemCountText.Text = "-";
            CacheSizeText.Text = "-";
            CacheCreatedText.Text = "-";
        }
    }

    private async void OnClearCacheClick(object? sender, RoutedEventArgs e)
    {
        if (_mainWindow == null)
            return;

        CacheStatusText.Text = "Rebuilding...";
        CacheStatusText.Foreground = GetWarningBrush();
        ClearCacheButton.IsEnabled = false;

        try
        {
            await _mainWindow.ClearAndReloadPaletteCacheAsync();
            CacheStatusText.Text = "Rebuilt";
            CacheStatusText.Foreground = GetSuccessBrush();
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Cache rebuild failed: {ex.Message}");
            CacheStatusText.Text = "Rebuild failed";
            CacheStatusText.Foreground = GetErrorBrush();
        }
        finally
        {
            ClearCacheButton.IsEnabled = true;
            UpdateCacheInfo();
        }
    }

    private void LoadSettings()
    {
        var sharedSettings = RadoubSettings.Instance;
        var settings = SettingsService.Instance;

        // Paths
        BaseGamePathTextBox.Text = sharedSettings.BaseGameInstallPath;
        GamePathTextBox.Text = sharedSettings.NeverwinterNightsPath;

        ValidateBaseGamePath();
        ValidateGamePath();

        // Font
        FontSizeSlider.Value = settings.FontSize;
        FontSizeLabel.Text = settings.FontSize.ToString();
    }

    private void PopulateThemes()
    {
        var themes = ThemeManager.Instance.AvailableThemes;

        // Deduplicate by name: prefer shared themes (org.radoub.*) over tool-specific
        // This prevents "Dark" appearing twice when both org.radoub.theme.dark and org.fence.theme.dark exist
        var deduplicatedThemes = themes
            .GroupBy(t => t.Plugin.Name)
            .Select(g => g.OrderByDescending(t => t.Plugin.Id.StartsWith("org.radoub.")).First())
            .OrderBy(t => t.Plugin.Name)
            .ToList();

        ThemeComboBox.ItemsSource = deduplicatedThemes.Select(t => t.Plugin.Name).ToList();

        var currentTheme = ThemeManager.Instance.CurrentTheme;
        if (currentTheme != null)
        {
            var index = deduplicatedThemes.FindIndex(t => t.Plugin.Id == currentTheme.Plugin.Id);
            if (index >= 0)
            {
                ThemeComboBox.SelectedIndex = index;
            }
        }
    }

    private void PopulateFontFamilies()
    {
        var fontFamilies = new List<string>
        {
            "(System Default)",
            "Segoe UI",
            "Arial",
            "Verdana",
            "Tahoma",
            "Consolas",
            "Courier New"
        };

        FontFamilyComboBox.ItemsSource = fontFamilies;

        var currentFamily = SettingsService.Instance.FontFamily;
        if (string.IsNullOrEmpty(currentFamily))
        {
            FontFamilyComboBox.SelectedIndex = 0;
        }
        else
        {
            var index = fontFamilies.FindIndex(f => f == currentFamily);
            FontFamilyComboBox.SelectedIndex = index >= 0 ? index : 0;
        }

        UpdateFontPreview();
    }

    private void OnThemeComboBoxChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || ThemeComboBox.SelectedIndex < 0)
            return;

        // Use same deduplication logic as PopulateThemes
        var deduplicatedThemes = ThemeManager.Instance.AvailableThemes
            .GroupBy(t => t.Plugin.Name)
            .Select(g => g.OrderByDescending(t => t.Plugin.Id.StartsWith("org.radoub.")).First())
            .OrderBy(t => t.Plugin.Name)
            .ToList();

        if (ThemeComboBox.SelectedIndex < deduplicatedThemes.Count)
        {
            var theme = deduplicatedThemes[ThemeComboBox.SelectedIndex];
            SettingsService.Instance.CurrentThemeId = theme.Plugin.Id;
        }
    }

    private void OnFontSizeChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing)
            return;

        var size = (int)FontSizeSlider.Value;
        FontSizeLabel.Text = size.ToString();
        SettingsService.Instance.FontSize = size;
        UpdateFontPreview();
    }

    private void OnFontFamilyChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || FontFamilyComboBox.SelectedIndex < 0)
            return;

        var selected = FontFamilyComboBox.SelectedItem as string;
        if (selected == "(System Default)")
        {
            SettingsService.Instance.FontFamily = "";
        }
        else if (!string.IsNullOrEmpty(selected))
        {
            SettingsService.Instance.FontFamily = selected;
        }

        UpdateFontPreview();
    }

    private void UpdateFontPreview()
    {
        var settings = SettingsService.Instance;
        FontPreviewText.FontSize = settings.FontSize;

        if (!string.IsNullOrEmpty(settings.FontFamily))
        {
            FontPreviewText.FontFamily = new FontFamily(settings.FontFamily);
        }
        else
        {
            FontPreviewText.FontFamily = FontFamily.Default;
        }
    }

    private async void OnBrowseBaseGamePathClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select NWN Game Installation Folder",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            BaseGamePathTextBox.Text = folders[0].Path.LocalPath;
            RadoubSettings.Instance.BaseGameInstallPath = folders[0].Path.LocalPath;
            ValidateBaseGamePath();
        }
    }

    private void OnAutoDetectBaseGamePathClick(object? sender, RoutedEventArgs e)
    {
        var detected = ResourcePathDetector.AutoDetectBaseGamePath();
        if (!string.IsNullOrEmpty(detected))
        {
            BaseGamePathTextBox.Text = detected;
            RadoubSettings.Instance.BaseGameInstallPath = detected;
            ValidateBaseGamePath();
        }
        else
        {
            BaseGamePathValidation.Text = "Could not auto-detect game path";
            BaseGamePathValidation.Foreground = GetWarningBrush();
        }
    }

    private async void OnBrowseGamePathClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select NWN User Data Folder",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            GamePathTextBox.Text = folders[0].Path.LocalPath;
            RadoubSettings.Instance.NeverwinterNightsPath = folders[0].Path.LocalPath;
            ValidateGamePath();
        }
    }

    private void OnAutoDetectGamePathClick(object? sender, RoutedEventArgs e)
    {
        var detected = ResourcePathDetector.AutoDetectGamePath();
        if (!string.IsNullOrEmpty(detected))
        {
            GamePathTextBox.Text = detected;
            RadoubSettings.Instance.NeverwinterNightsPath = detected;
            ValidateGamePath();
        }
        else
        {
            GamePathValidation.Text = "Could not auto-detect user data path";
            GamePathValidation.Foreground = GetWarningBrush();
        }
    }

    private void ValidateBaseGamePath()
    {
        var path = BaseGamePathTextBox.Text;
        if (string.IsNullOrEmpty(path))
        {
            BaseGamePathValidation.Text = "";
            return;
        }

        if (Directory.Exists(path))
        {
            var dataPath = Path.Combine(path, "data");
            var keyFile = Path.Combine(path, "data", "nwn_base.key");

            if (Directory.Exists(dataPath) || File.Exists(keyFile))
            {
                BaseGamePathValidation.Text = "\u2713 Valid NWN installation detected";
                BaseGamePathValidation.Foreground = GetSuccessBrush();
            }
            else
            {
                BaseGamePathValidation.Text = "\u26A0 Directory exists but may not be NWN installation";
                BaseGamePathValidation.Foreground = GetWarningBrush();
            }
        }
        else
        {
            BaseGamePathValidation.Text = "\u2717 Directory does not exist";
            BaseGamePathValidation.Foreground = GetErrorBrush();
        }
    }

    private void ValidateGamePath()
    {
        var path = GamePathTextBox.Text;
        if (string.IsNullOrEmpty(path))
        {
            GamePathValidation.Text = "";
            return;
        }

        if (Directory.Exists(path))
        {
            var modulesPath = Path.Combine(path, "modules");
            if (Directory.Exists(modulesPath))
            {
                GamePathValidation.Text = "\u2713 Valid NWN user data directory";
                GamePathValidation.Foreground = GetSuccessBrush();
            }
            else
            {
                GamePathValidation.Text = "\u26A0 Directory exists but no modules folder found";
                GamePathValidation.Foreground = GetWarningBrush();
            }
        }
        else
        {
            GamePathValidation.Text = "\u2717 Directory does not exist";
            GamePathValidation.Foreground = GetErrorBrush();
        }
    }

    private IBrush GetSuccessBrush() => BrushManager.GetSuccessBrush(this);
    private IBrush GetWarningBrush() => BrushManager.GetWarningBrush(this);
    private IBrush GetErrorBrush() => BrushManager.GetErrorBrush(this);

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
