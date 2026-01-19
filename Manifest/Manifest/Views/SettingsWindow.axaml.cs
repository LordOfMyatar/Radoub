using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Manifest.Services;
using Radoub.Formats.Logging;
using Radoub.UI.Services;
using ThemeManager = Radoub.UI.Services.ThemeManager;
using ThemeManifest = Radoub.UI.Models.ThemeManifest;
using EasterEggService = Radoub.UI.Services.EasterEggService;
using Radoub.Dictionary;
using Radoub.Formats.Settings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Manifest.Views;

public partial class SettingsWindow : Window
{
    private bool _isLoading = true;

    public SettingsWindow()
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

        // Load spell check settings
        LoadSpellCheckSettings();

        // Load dictionary settings
        LoadDictionarySettings();
    }

    private void LoadThemeList()
    {
        ThemeComboBox.Items.Clear();

        // Check if Sea-Sick easter egg is unlocked (all 3 tools launched)
        var includeEasterEggs = EasterEggService.Instance.IsSeaSickUnlocked();

        var themes = ThemeManager.Instance.AvailableThemes
            .Where(t => includeEasterEggs || !t.Plugin.Tags.Contains("easter-egg"));
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

        UserPathTextBox.Text = radoubSettings.NeverwinterNightsPath;
        UpdateUserPathValidation();
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
            GamePathValidationText.Text = StatusIndicatorHelper.FormatValidation("Auto-detected game installation", true);
            GamePathValidationText.Foreground = GetSuccessBrush();
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Game path auto-detected: {SanitizePath(detectedPath)}");
        }
        else
        {
            GamePathValidationText.Text = StatusIndicatorHelper.FormatWarning("Could not auto-detect game path. Please browse manually.");
            GamePathValidationText.Foreground = GetWarningBrush();
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
        GamePathValidationText.Text = StatusIndicatorHelper.FormatValidation(result.Message, result.IsValid);
        GamePathValidationText.Foreground = result.IsValid
            ? GetSuccessBrush()
            : GetErrorBrush();
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

    #region User Path

    private void OnUserPathTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isLoading) return;

        var path = UserPathTextBox.Text ?? "";
        RadoubSettings.Instance.NeverwinterNightsPath = path;
        UpdateUserPathValidation();
        UnifiedLogger.LogApplication(LogLevel.INFO, $"User path changed to: {SanitizePath(path)}");
    }

    private async void OnBrowseUserPath(object? sender, RoutedEventArgs e)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null) return;

        var options = new FolderPickerOpenOptions
        {
            Title = "Select Neverwinter Nights User Folder",
            AllowMultiple = false
        };

        var result = await storageProvider.OpenFolderPickerAsync(options);
        if (result.Count > 0)
        {
            var path = result[0].Path.LocalPath;
            UserPathTextBox.Text = path;
            RadoubSettings.Instance.NeverwinterNightsPath = path;
            UpdateUserPathValidation();
            UnifiedLogger.LogApplication(LogLevel.INFO, $"User path set via browse: {SanitizePath(path)}");
        }
    }

    private void OnAutoDetectUserPath(object? sender, RoutedEventArgs e)
    {
        var detectedPath = ResourcePathDetector.AutoDetectGamePath();

        if (!string.IsNullOrEmpty(detectedPath))
        {
            UserPathTextBox.Text = detectedPath;
            RadoubSettings.Instance.NeverwinterNightsPath = detectedPath;
            UpdateUserPathValidation();
            UserPathValidationText.Text = StatusIndicatorHelper.FormatValidation("Auto-detected user documents", true);
            UserPathValidationText.Foreground = GetSuccessBrush();
            UnifiedLogger.LogApplication(LogLevel.INFO, $"User path auto-detected: {SanitizePath(detectedPath)}");
        }
        else
        {
            UserPathValidationText.Text = StatusIndicatorHelper.FormatWarning("Could not auto-detect user path. Please browse manually.");
            UserPathValidationText.Foreground = GetWarningBrush();
            UnifiedLogger.LogApplication(LogLevel.WARN, "User path auto-detection failed");
        }
    }

    private void UpdateUserPathValidation()
    {
        var path = UserPathTextBox.Text ?? "";

        if (string.IsNullOrEmpty(path))
        {
            UserPathValidationText.Text = "";
            return;
        }

        var result = ResourcePathDetector.ValidateGamePathWithMessage(path);
        UserPathValidationText.Text = StatusIndicatorHelper.FormatValidation(result.Message, result.IsValid);
        UserPathValidationText.Foreground = result.IsValid
            ? GetSuccessBrush()
            : GetErrorBrush();
    }

    #endregion

    #region Spell Check

    private void LoadSpellCheckSettings()
    {
        SpellCheckEnabledCheckBox.IsChecked = SettingsService.Instance.SpellCheckEnabled;
        UpdateDictionaryInfo();
    }

    private void OnSpellCheckEnabledChanged(object? sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        SettingsService.Instance.SpellCheckEnabled = SpellCheckEnabledCheckBox.IsChecked ?? true;
    }

    private void UpdateDictionaryInfo()
    {
        var wordCount = SpellCheckService.Instance.GetCustomWordCount();
        DictionaryInfoText.Text = $"Custom dictionary: {wordCount} words";
    }

    #endregion

    #region Dictionaries

    private DictionaryDiscovery? _discovery;

    private void LoadDictionarySettings()
    {
        _discovery = new DictionaryDiscovery(DictionaryDiscovery.GetDefaultUserDictionaryPath());
        LoadPrimaryLanguageList();
        LoadCustomDictionaryList();
    }

    private void LoadPrimaryLanguageList()
    {
        if (_discovery == null) return;

        PrimaryLanguageComboBox.Items.Clear();
        var languages = _discovery.GetAvailableLanguages();
        var currentLanguage = DictionarySettingsService.Instance.PrimaryLanguage;

        foreach (var lang in languages)
        {
            var item = new LanguageListItem
            {
                Id = lang.Id,
                DisplayName = lang.Name,
                IsBundled = lang.IsBundled
            };

            PrimaryLanguageComboBox.Items.Add(item);

            if (lang.Id == currentLanguage)
            {
                PrimaryLanguageComboBox.SelectedItem = item;
            }
        }

        // Default to first item if no selection
        if (PrimaryLanguageComboBox.SelectedItem == null && PrimaryLanguageComboBox.Items.Count > 0)
        {
            PrimaryLanguageComboBox.SelectedIndex = 0;
        }
    }

    private void LoadCustomDictionaryList()
    {
        if (_discovery == null) return;

        CustomDictionariesListBox.Items.Clear();
        var customDicts = _discovery.GetAvailableCustomDictionaries();

        foreach (var dict in customDicts)
        {
            var isEnabled = DictionarySettingsService.Instance.IsCustomDictionaryEnabled(dict.Id);

            var checkBox = new CheckBox
            {
                Content = dict.Name,
                IsChecked = isEnabled,
                Tag = dict.Id
            };

            ToolTip.SetTip(checkBox, dict.Description ?? $"Custom dictionary: {dict.Id}");
            checkBox.IsCheckedChanged += OnCustomDictionaryToggled;

            CustomDictionariesListBox.Items.Add(checkBox);
        }

        // Show message if no custom dictionaries found
        if (customDicts.Count == 0)
        {
            CustomDictionariesListBox.Items.Add(new TextBlock
            {
                Text = "No custom dictionaries found",
                FontStyle = FontStyle.Italic,
                Foreground = Brushes.Gray,
                Margin = new Avalonia.Thickness(5)
            });
        }
    }

    private void OnPrimaryLanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        if (PrimaryLanguageComboBox.SelectedItem is LanguageListItem item)
        {
            DictionarySettingsService.Instance.PrimaryLanguage = item.Id;
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Primary dictionary language changed to: {item.DisplayName}");
        }
    }

    private void OnCustomDictionaryToggled(object? sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        if (sender is CheckBox checkBox && checkBox.Tag is string dictionaryId)
        {
            var isEnabled = checkBox.IsChecked ?? false;
            DictionarySettingsService.Instance.SetCustomDictionaryEnabled(dictionaryId, isEnabled);
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Custom dictionary '{dictionaryId}' {(isEnabled ? "enabled" : "disabled")}");
        }
    }

    private void OnOpenDictionariesFolderClick(object? sender, RoutedEventArgs e)
    {
        var path = DictionaryDiscovery.GetDefaultUserDictionaryPath();

        // Ensure directory exists
        Directory.CreateDirectory(path);

        try
        {
            // Open in file explorer
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Opened dictionaries folder: {SanitizePath(path)}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to open dictionaries folder: {ex.Message}");
        }
    }

    private void OnRefreshDictionariesClick(object? sender, RoutedEventArgs e)
    {
        _discovery?.ClearCache();
        LoadPrimaryLanguageList();
        LoadCustomDictionaryList();
        UnifiedLogger.LogApplication(LogLevel.INFO, "Dictionary list refreshed");
    }

    /// <summary>
    /// Helper class for language combo box items.
    /// </summary>
    private class LanguageListItem
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public bool IsBundled { get; set; }

        public override string ToString()
        {
            return IsBundled ? DisplayName : $"{DisplayName} (installed)";
        }
    }

    #endregion

    #region Theme-Aware Colors

    private IBrush GetErrorBrush() => BrushManager.GetErrorBrush(this);
    private IBrush GetSuccessBrush() => BrushManager.GetSuccessBrush(this);
    private IBrush GetWarningBrush() => BrushManager.GetWarningBrush(this);

    #endregion
}
