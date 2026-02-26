using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Manifest.Services;
using Radoub.Formats.Logging;
using Radoub.UI.Services;
using ThemeManager = Radoub.UI.Services.ThemeManager;
using ThemeManifest = Radoub.UI.Models.ThemeManifest;
using EasterEggService = Radoub.UI.Services.EasterEggService;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Manifest.Views;

/// <summary>
/// SettingsWindow core: constructor, theme, font, and close.
/// Split into partial files:
///   - SettingsWindow.Paths.cs: Game path, user path, module configuration
///   - SettingsWindow.Dictionary.cs: Spell check and dictionary management
/// </summary>
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

        // Load module configuration (#1325, #1322)
        LoadModuleConfiguration();

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

        // Load spell check settings
        LoadSpellCheckSettings();

        // Load dictionary settings
        LoadDictionarySettings();
    }

    #region Theme

    private void LoadThemeList()
    {
        ThemeComboBox.Items.Clear();

        // Check if Sea-Sick easter egg is unlocked (all 3 tools launched)
        var includeEasterEggs = EasterEggService.Instance.IsSeaSickUnlocked();

        var themes = ThemeManager.Instance.AvailableThemes
            .Where(t => includeEasterEggs || !t.Plugin.Tags.Contains("easter-egg"));

        // Deduplicate by name: prefer shared themes (org.radoub.*) over any user overrides
        var deduplicatedThemes = themes
            .GroupBy(t => t.Plugin.Name)
            .Select(g => g.OrderByDescending(t => t.Plugin.Id.StartsWith("org.radoub.")).First());

        var currentThemeId = SettingsService.Instance.CurrentThemeId;

        // Group themes: standard first, then accessibility
        var standardThemes = deduplicatedThemes.Where(t => t.Accessibility?.Type != "colorblind").OrderBy(t => t.Plugin.Name);
        var accessibilityThemes = deduplicatedThemes.Where(t => t.Accessibility?.Type == "colorblind").OrderBy(t => t.Plugin.Name);

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

    #endregion

    #region Font

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
            catch (ArgumentException ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Invalid font family '{fontFamily}': {ex.Message}");
            }
        }
    }

    #endregion

    #region Theme-Aware Colors

    private IBrush GetErrorBrush() => BrushManager.GetErrorBrush(this);
    private IBrush GetSuccessBrush() => BrushManager.GetSuccessBrush(this);
    private IBrush GetWarningBrush() => BrushManager.GetWarningBrush(this);

    #endregion

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
