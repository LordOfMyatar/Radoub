using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Quartermaster.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.UI.Services;
using ThemeManager = Radoub.UI.Services.ThemeManager;
using ThemeManifest = Radoub.UI.Models.ThemeManifest;
using EasterEggService = Radoub.UI.Services.EasterEggService;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Settings window: module configuration, resource paths (Paths.cs), UI settings, cache management.
/// </summary>
public partial class SettingsWindow : Window
{
    private bool _isInitializing = true;
    private MainWindow? _mainWindow;

    public SettingsWindow()
    {
        InitializeComponent();
        LoadSettings();
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

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void LoadSettings()
    {
        LoadModuleConfiguration();
        LoadResourcePathSettings();
        LoadUISettings();
    }

    #region Module Configuration (#1325, #1322)

    private void LoadModuleConfiguration()
    {
        var currentModuleText = this.FindControl<TextBlock>("CurrentModuleText");
        if (currentModuleText == null) return;

        var modulePath = RadoubSettings.Instance.CurrentModulePath;
        if (RadoubSettings.IsValidModulePath(modulePath))
        {
            // Try to get module name from IFO
            string? workingDir = modulePath;
            if (File.Exists(modulePath) && modulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
            {
                // Resolve .mod to unpacked dir for IFO reading
                var moduleName = Path.GetFileNameWithoutExtension(modulePath);
                var moduleDir = Path.GetDirectoryName(modulePath);
                if (!string.IsNullOrEmpty(moduleDir))
                {
                    var candidate = Path.Combine(moduleDir, moduleName);
                    if (Directory.Exists(candidate))
                        workingDir = candidate;
                }
            }

            string? displayName = null;
            if (!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir))
            {
                var ifoPath = Path.Combine(workingDir, "module.ifo");
                if (File.Exists(ifoPath))
                {
                    try
                    {
                        var ifo = Radoub.Formats.Ifo.IfoReader.Read(ifoPath);
                        displayName = ifo.ModuleName.GetDefault();
                    }
                    catch { /* fall through to path-based name */ }
                }
            }

            currentModuleText.Text = displayName ?? Path.GetFileName(modulePath);
            currentModuleText.Foreground = BrushManager.GetInfoBrush(this);
        }
        else
        {
            currentModuleText.Text = "No module selected";
            currentModuleText.Foreground = BrushManager.GetWarningBrush(this);
        }
    }

    private void OnConfigureInTrebuchetClick(object? sender, RoutedEventArgs e)
    {
        var trebuchetPath = RadoubSettings.Instance.TrebuchetPath;
        var statusText = this.FindControl<TextBlock>("TrebuchetStatusText");

        if (string.IsNullOrEmpty(trebuchetPath) || !File.Exists(trebuchetPath))
        {
            if (statusText != null)
            {
                statusText.Text = "Trebuchet not found. Launch Trebuchet once to register its path.";
                statusText.Foreground = BrushManager.GetWarningBrush(this);
            }
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = trebuchetPath,
                UseShellExecute = true
            });

            if (statusText != null)
            {
                statusText.Text = "Trebuchet launched. Restart this tool after changing module.";
                statusText.Foreground = BrushManager.GetInfoBrush(this);
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to launch Trebuchet: {ex.Message}");
            if (statusText != null)
            {
                statusText.Text = $"Failed to launch Trebuchet: {ex.Message}";
                statusText.Foreground = BrushManager.GetErrorBrush(this);
            }
        }
    }

    #endregion

    #region UI Settings

    private void LoadUISettings()
    {
        var settings = SettingsService.Instance;

        // Theme
        var themeComboBox = this.FindControl<ComboBox>("ThemeComboBox");
        if (themeComboBox != null)
        {
            PopulateThemeList(themeComboBox);

            var themes = (IEnumerable<ThemeManifest>?)themeComboBox.ItemsSource;
            var currentTheme = themes?.FirstOrDefault(t => t.Plugin.Id == settings.CurrentThemeId);
            themeComboBox.SelectedItem = currentTheme;

            if (currentTheme != null)
            {
                UpdateThemeDescription(currentTheme);
            }
        }

        // Font Size
        var fontSizeSlider = this.FindControl<Slider>("FontSizeSlider");
        var fontSizeLabel = this.FindControl<TextBlock>("FontSizeLabel");
        if (fontSizeSlider != null)
        {
            fontSizeSlider.Value = settings.FontSize;
            if (fontSizeLabel != null)
            {
                fontSizeLabel.Text = $"{settings.FontSize}pt";
            }
        }

        // Font Family
        var fontFamilyComboBox = this.FindControl<ComboBox>("FontFamilyComboBox");
        if (fontFamilyComboBox != null)
        {
            var fonts = new List<string> { "(System Default)" };
            fonts.AddRange(FontManager.Current.SystemFonts.Select(f => f.Name).OrderBy(n => n));
            fontFamilyComboBox.ItemsSource = fonts;

            if (string.IsNullOrEmpty(settings.FontFamily))
            {
                fontFamilyComboBox.SelectedIndex = 0;
            }
            else
            {
                var index = fonts.IndexOf(settings.FontFamily);
                fontFamilyComboBox.SelectedIndex = index >= 0 ? index : 0;
            }

            UpdateFontPreview();
        }
    }

    private void PopulateThemeList(ComboBox comboBox)
    {
        // Check if Sea-Sick easter egg is unlocked (all 3 tools launched)
        var includeEasterEggs = EasterEggService.Instance.IsSeaSickUnlocked();

        var themes = ThemeManager.Instance.AvailableThemes
            .Where(t => includeEasterEggs || !t.Plugin.Tags.Contains("easter-egg"));

        // Deduplicate by name: prefer shared themes (org.radoub.*) over any user overrides
        var deduplicatedThemes = themes
            .GroupBy(t => t.Plugin.Name)
            .Select(g => g.OrderByDescending(t => t.Plugin.Id.StartsWith("org.radoub.")).First())
            .OrderBy(t => t.Plugin.Name)
            .ToList();

        comboBox.ItemsSource = deduplicatedThemes;
        comboBox.DisplayMemberBinding = new Binding("Plugin.Name");
    }

    private void UpdateThemeDescription(ThemeManifest theme)
    {
        var nameText = this.FindControl<TextBlock>("ThemeNameText");
        var descText = this.FindControl<TextBlock>("ThemeDescriptionText");

        if (nameText != null)
        {
            nameText.Text = theme.Plugin.Name;
        }

        if (descText != null)
        {
            descText.Text = theme.Plugin.Description;
        }
    }

    private void OnThemeComboBoxChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        var comboBox = sender as ComboBox;
        if (comboBox?.SelectedItem is ThemeManifest theme)
        {
            ThemeManager.Instance.ApplyTheme(theme);
            UpdateThemeDescription(theme);
            SettingsService.Instance.CurrentThemeId = theme.Plugin.Id;
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Theme changed to: {theme.Plugin.Name}");
        }
    }

    private void OnFontSizeChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;

        var slider = sender as Slider;
        if (slider == null) return;

        var fontSize = (int)slider.Value;
        var fontSizeLabel = this.FindControl<TextBlock>("FontSizeLabel");
        if (fontSizeLabel != null)
        {
            fontSizeLabel.Text = $"{fontSize}pt";
        }

        SettingsService.Instance.FontSize = fontSize;
        UpdateFontPreview();
    }

    private void OnFontFamilyChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        var comboBox = sender as ComboBox;
        if (comboBox?.SelectedItem is string fontFamily)
        {
            if (fontFamily == "(System Default)")
            {
                SettingsService.Instance.FontFamily = "";
            }
            else
            {
                SettingsService.Instance.FontFamily = fontFamily;
            }
            UpdateFontPreview();
        }
    }

    private void UpdateFontPreview()
    {
        var previewText = this.FindControl<TextBlock>("FontPreviewText");
        if (previewText == null) return;

        var settings = SettingsService.Instance;
        previewText.FontSize = settings.FontSize;

        if (!string.IsNullOrEmpty(settings.FontFamily))
        {
            try
            {
                previewText.FontFamily = new FontFamily(settings.FontFamily);
            }
            catch (ArgumentException ex)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Invalid font family for preview: {ex.Message}");
            }
        }
    }

    #endregion

    #region Cache Management

    private void UpdateCacheInfo()
    {
        var cacheStatusText = this.FindControl<TextBlock>("CacheStatusText");
        var cacheItemCountText = this.FindControl<TextBlock>("CacheItemCountText");
        var cacheSizeText = this.FindControl<TextBlock>("CacheSizeText");
        var cacheSourcesText = this.FindControl<TextBlock>("CacheSourcesText");

        if (cacheStatusText == null) return;

        var stats = _mainWindow?.GetPaletteCacheStatistics();

        if (stats != null && stats.TotalItems > 0)
        {
            cacheStatusText.Text = "Cached";
            cacheStatusText.Foreground = GetSuccessBrush();

            if (cacheItemCountText != null)
                cacheItemCountText.Text = $"{stats.TotalItems:N0}";

            if (cacheSizeText != null)
                cacheSizeText.Text = $"{stats.TotalSizeKB:N1} KB";

            if (cacheSourcesText != null)
            {
                var sources = stats.SourceCounts
                    .Select(kv => $"{kv.Key}: {kv.Value:N0}")
                    .ToList();
                cacheSourcesText.Text = sources.Count > 0 ? string.Join(", ", sources) : "-";
            }
        }
        else
        {
            cacheStatusText.Text = "No cache";
            cacheStatusText.Foreground = GetWarningBrush();

            if (cacheItemCountText != null)
                cacheItemCountText.Text = "-";

            if (cacheSizeText != null)
                cacheSizeText.Text = "-";

            if (cacheSourcesText != null)
                cacheSourcesText.Text = "-";
        }
    }

    private async void OnClearCacheClick(object? sender, RoutedEventArgs e)
    {
        if (_mainWindow == null)
            return;

        var cacheStatusText = this.FindControl<TextBlock>("CacheStatusText");
        var clearCacheButton = this.FindControl<Button>("ClearCacheButton");

        if (cacheStatusText != null)
        {
            cacheStatusText.Text = "Rebuilding...";
            cacheStatusText.Foreground = GetWarningBrush();
        }

        if (clearCacheButton != null)
            clearCacheButton.IsEnabled = false;

        try
        {
            await _mainWindow.ClearAndReloadPaletteCacheAsync();

            if (cacheStatusText != null)
            {
                cacheStatusText.Text = "Rebuilt";
                cacheStatusText.Foreground = GetSuccessBrush();
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Cache rebuild failed: {ex.Message}");

            if (cacheStatusText != null)
            {
                cacheStatusText.Text = "Rebuild failed";
                cacheStatusText.Foreground = GetErrorBrush();
            }
        }
        finally
        {
            if (clearCacheButton != null)
                clearCacheButton.IsEnabled = true;

            UpdateCacheInfo();
        }
    }

    #endregion

    #region Dialog Buttons

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        ApplySettings();
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ApplySettings()
    {
        var sharedSettings = RadoubSettings.Instance;

        // Resource paths
        var baseGamePathTextBox = this.FindControl<TextBox>("BaseGamePathTextBox");
        if (baseGamePathTextBox != null)
        {
            var path = baseGamePathTextBox.Text?.Trim();
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                sharedSettings.BaseGameInstallPath = path;
            }
        }

        var gamePathTextBox = this.FindControl<TextBox>("GamePathTextBox");
        if (gamePathTextBox != null)
        {
            var path = gamePathTextBox.Text?.Trim();
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                sharedSettings.NeverwinterNightsPath = path;
            }
        }

        UnifiedLogger.LogApplication(LogLevel.INFO, "Settings applied successfully");
    }

    #endregion

    #region Theme-Aware Colors

    private IBrush GetErrorBrush() => BrushManager.GetErrorBrush(this);
    private IBrush GetSuccessBrush() => BrushManager.GetSuccessBrush(this);
    private IBrush GetWarningBrush() => BrushManager.GetWarningBrush(this);

    #endregion
}
