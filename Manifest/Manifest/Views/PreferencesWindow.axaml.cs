using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Manifest.Models;
using Manifest.Services;
using Radoub.Formats.Settings;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Manifest.Views;

public partial class PreferencesWindow : Window
{
    private bool _isLoading = true;

    public PreferencesWindow()
    {
        InitializeComponent();
        LoadSettings();
        _isLoading = false;
    }

    private void LoadSettings()
    {
        var settings = SettingsService.Instance;

        // Load game path from shared settings
        LoadGamePath();

        // Load themes
        LoadThemeList();

        // Load font size
        FontSizeSlider.Value = settings.FontSize;
        FontSizeLabel.Text = $"{settings.FontSize:0}pt";
        UpdateFontPreview();

        // Load font family list
        LoadFontFamilyList();

        // Load log level
        LoadLogLevelList();

        // Load log retention
        LogRetentionSlider.Value = settings.LogRetentionSessions;
        LogRetentionLabel.Text = settings.LogRetentionSessions.ToString();
    }

    private void LoadThemeList()
    {
        ThemeComboBox.Items.Clear();

        var themes = ThemeManager.Instance.AvailableThemes;
        var currentThemeId = SettingsService.Instance.CurrentThemeId;

        // Group themes: standard first, then accessibility
        var standardThemes = themes.Where(t => t.Accessibility?.Type != "colorblind").OrderBy(t => t.Plugin.Name);
        var accessibilityThemes = themes.Where(t => t.Accessibility?.Type == "colorblind").OrderBy(t => t.Plugin.Name);

        foreach (var theme in standardThemes.Concat(accessibilityThemes))
        {
            var item = new ComboBoxItem
            {
                Content = theme.Plugin.Name,
                Tag = theme
            };

            ThemeComboBox.Items.Add(item);

            if (theme.Plugin.Id == currentThemeId)
            {
                ThemeComboBox.SelectedItem = item;
            }
        }

        // Update description for current theme
        UpdateThemeDescription();
    }

    private void UpdateThemeDescription()
    {
        if (ThemeComboBox.SelectedItem is ComboBoxItem item && item.Tag is ThemeManifest theme)
        {
            var desc = theme.Plugin.Description;
            if (theme.Accessibility?.Type == "colorblind")
            {
                desc += $" ({theme.Accessibility.Condition})";
            }
            ThemeDescriptionText.Text = desc;
        }
    }

    private void OnThemeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        if (ThemeComboBox.SelectedItem is ComboBoxItem item && item.Tag is ThemeManifest theme)
        {
            SettingsService.Instance.CurrentThemeId = theme.Plugin.Id;
            UpdateThemeDescription();
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Theme changed to: {theme.Plugin.Name}");
        }
    }

    private void LoadFontFamilyList()
    {
        FontFamilyComboBox.Items.Clear();

        // Add default option
        FontFamilyComboBox.Items.Add("(Use theme default)");

        // Get available font families
        var fontFamilies = new List<string>
        {
            "Segoe UI",
            "Arial",
            "Verdana",
            "Tahoma",
            "Consolas",
            "Courier New",
            "Times New Roman",
            "Georgia",
            "Trebuchet MS",
            "Calibri"
        };

        // Add Inter if available (Avalonia's default)
        fontFamilies.Insert(0, "Inter");

        foreach (var font in fontFamilies)
        {
            FontFamilyComboBox.Items.Add(font);
        }

        // Select current font
        var currentFont = SettingsService.Instance.FontFamily;
        if (string.IsNullOrEmpty(currentFont))
        {
            FontFamilyComboBox.SelectedIndex = 0; // (Use theme default)
        }
        else
        {
            var index = fontFamilies.IndexOf(currentFont);
            if (index >= 0)
            {
                FontFamilyComboBox.SelectedIndex = index + 1; // +1 for the default option
            }
            else
            {
                FontFamilyComboBox.SelectedIndex = 0;
            }
        }
    }

    private void OnFontSizeChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isLoading) return;

        var newSize = FontSizeSlider.Value;
        FontSizeLabel.Text = $"{newSize:0}pt";
        SettingsService.Instance.FontSize = newSize;
        UpdateFontPreview();
    }

    private void OnFontFamilyChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        if (FontFamilyComboBox.SelectedIndex == 0)
        {
            // Use theme default
            SettingsService.Instance.FontFamily = "";
        }
        else if (FontFamilyComboBox.SelectedItem is string fontName)
        {
            SettingsService.Instance.FontFamily = fontName;
        }
        UpdateFontPreview();
    }

    private void UpdateFontPreview()
    {
        var fontSize = SettingsService.Instance.FontSize;
        FontPreviewText.FontSize = fontSize;

        var fontFamily = SettingsService.Instance.FontFamily;
        if (!string.IsNullOrEmpty(fontFamily))
        {
            try
            {
                FontPreviewText.FontFamily = new FontFamily(fontFamily);
            }
            catch
            {
                // Invalid font family - use default
            }
        }
    }

    private void LoadLogLevelList()
    {
        LogLevelComboBox.Items.Clear();

        foreach (LogLevel level in Enum.GetValues<LogLevel>())
        {
            var item = new ComboBoxItem
            {
                Content = level.ToString(),
                Tag = level
            };
            LogLevelComboBox.Items.Add(item);

            if (level == SettingsService.Instance.CurrentLogLevel)
            {
                LogLevelComboBox.SelectedItem = item;
            }
        }
    }

    private void OnLogLevelChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        if (LogLevelComboBox.SelectedItem is ComboBoxItem item && item.Tag is LogLevel level)
        {
            SettingsService.Instance.CurrentLogLevel = level;
        }
    }

    private void OnLogRetentionChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isLoading) return;

        var value = (int)LogRetentionSlider.Value;
        LogRetentionLabel.Text = value.ToString();
        SettingsService.Instance.LogRetentionSessions = value;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    #region Game Path

    private void LoadGamePath()
    {
        var radoubSettings = RadoubSettings.Instance;
        GamePathTextBox.Text = radoubSettings.BaseGameInstallPath;
        UpdateGamePathValidation();
    }

    private void OnGamePathTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isLoading) return;

        var path = GamePathTextBox.Text ?? "";
        RadoubSettings.Instance.BaseGameInstallPath = path;
        UpdateGamePathValidation();
        UnifiedLogger.LogApplication(LogLevel.INFO, $"Game path changed to: {SanitizePath(path)}");
    }

    private async void OnBrowseGamePath(object? sender, RoutedEventArgs e)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null) return;

        var options = new FolderPickerOpenOptions
        {
            Title = "Select Neverwinter Nights Installation Folder",
            AllowMultiple = false
        };

        var result = await storageProvider.OpenFolderPickerAsync(options);
        if (result.Count > 0)
        {
            var path = result[0].Path.LocalPath;
            GamePathTextBox.Text = path;
            RadoubSettings.Instance.BaseGameInstallPath = path;
            UpdateGamePathValidation();
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Game path set via browse: {SanitizePath(path)}");
        }
    }

    private void OnAutoDetectGamePath(object? sender, RoutedEventArgs e)
    {
        var detectedPath = ResourcePathDetector.AutoDetectBaseGamePath();

        if (!string.IsNullOrEmpty(detectedPath))
        {
            GamePathTextBox.Text = detectedPath;
            RadoubSettings.Instance.BaseGameInstallPath = detectedPath;
            UpdateGamePathValidation();
            GamePathValidationText.Text = "Auto-detected game installation";
            GamePathValidationText.Foreground = Avalonia.Media.Brushes.Green;
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Game path auto-detected: {SanitizePath(detectedPath)}");
        }
        else
        {
            GamePathValidationText.Text = "Could not auto-detect game path. Please browse manually.";
            GamePathValidationText.Foreground = Avalonia.Media.Brushes.Orange;
            UnifiedLogger.LogApplication(LogLevel.WARN, "Game path auto-detection failed");
        }
    }

    private void UpdateGamePathValidation()
    {
        var path = GamePathTextBox.Text ?? "";

        if (string.IsNullOrEmpty(path))
        {
            GamePathValidationText.Text = "";
            return;
        }

        var result = ResourcePathDetector.ValidateBaseGamePathWithMessage(path);
        GamePathValidationText.Text = result.Message;
        GamePathValidationText.Foreground = result.IsValid
            ? Avalonia.Media.Brushes.Green
            : Avalonia.Media.Brushes.Red;
    }

    private static string SanitizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile) && path.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase))
        {
            return "~" + path.Substring(userProfile.Length);
        }
        return path;
    }

    #endregion
}
