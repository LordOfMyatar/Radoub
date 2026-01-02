using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Quartermaster.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using ThemeManager = Radoub.UI.Services.ThemeManager;
using ThemeManifest = Radoub.UI.Models.ThemeManifest;

namespace Quartermaster.Views.Dialogs;

public partial class SettingsWindow : Window
{
    private bool _isInitializing = true;

    public SettingsWindow()
    {
        InitializeComponent();
        LoadSettings();
        _isInitializing = false;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void LoadSettings()
    {
        LoadResourcePathSettings();
        LoadUISettings();
        LoadLoggingSettings();
    }

    #region Resource Paths

    private void LoadResourcePathSettings()
    {
        var sharedSettings = RadoubSettings.Instance;

        var baseGamePathTextBox = this.FindControl<TextBox>("BaseGamePathTextBox");
        if (baseGamePathTextBox != null)
        {
            baseGamePathTextBox.Text = sharedSettings.BaseGameInstallPath ?? "";
            ValidateBaseGamePath(sharedSettings.BaseGameInstallPath);
        }

        var gamePathTextBox = this.FindControl<TextBox>("GamePathTextBox");
        if (gamePathTextBox != null)
        {
            gamePathTextBox.Text = sharedSettings.NeverwinterNightsPath ?? "";
            ValidateGamePath(sharedSettings.NeverwinterNightsPath);
        }

        UpdatePlatformPathsInfo();
    }

    private void UpdatePlatformPathsInfo()
    {
        var platformPathsInfo = this.FindControl<TextBlock>("PlatformPathsInfo");
        if (platformPathsInfo == null) return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            platformPathsInfo.Text = "Windows:\n" +
                "• Steam: C:\\Program Files (x86)\\Steam\\steamapps\\common\\Neverwinter Nights\n" +
                "• GOG: C:\\GOG Games\\Neverwinter Nights Enhanced Edition\n" +
                "• Beamdog: C:\\Program Files\\Beamdog\\Neverwinter Nights";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            platformPathsInfo.Text = "Linux:\n" +
                "• Steam: ~/.steam/steam/steamapps/common/Neverwinter Nights\n" +
                "• GOG: ~/GOG Games/Neverwinter Nights";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            platformPathsInfo.Text = "macOS:\n" +
                "• Steam: ~/Library/Application Support/Steam/steamapps/common/Neverwinter Nights\n" +
                "• Beamdog: ~/Library/Application Support/Beamdog/Neverwinter Nights";
        }
    }

    private async void OnBrowseBaseGamePathClick(object? sender, RoutedEventArgs e)
    {
        var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Neverwinter Nights Installation Folder",
            AllowMultiple = false
        });

        if (result.Count > 0)
        {
            var path = result[0].Path.LocalPath;
            var baseGamePathTextBox = this.FindControl<TextBox>("BaseGamePathTextBox");
            if (baseGamePathTextBox != null)
            {
                baseGamePathTextBox.Text = path;
                ValidateBaseGamePath(path);
            }
        }
    }

    private void OnAutoDetectBaseGamePathClick(object? sender, RoutedEventArgs e)
    {
        var detectedPath = DetectBaseGamePath();
        if (!string.IsNullOrEmpty(detectedPath))
        {
            var baseGamePathTextBox = this.FindControl<TextBox>("BaseGamePathTextBox");
            if (baseGamePathTextBox != null)
            {
                baseGamePathTextBox.Text = detectedPath;
                ValidateBaseGamePath(detectedPath);
            }
        }
        else
        {
            var validation = this.FindControl<TextBlock>("BaseGamePathValidation");
            if (validation != null)
            {
                validation.Text = "Could not auto-detect installation path";
                validation.Foreground = Brushes.Orange;
            }
        }
    }

    private async void OnBrowseGamePathClick(object? sender, RoutedEventArgs e)
    {
        var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Neverwinter Nights User Data Folder",
            AllowMultiple = false
        });

        if (result.Count > 0)
        {
            var path = result[0].Path.LocalPath;
            var gamePathTextBox = this.FindControl<TextBox>("GamePathTextBox");
            if (gamePathTextBox != null)
            {
                gamePathTextBox.Text = path;
                ValidateGamePath(path);
            }
        }
    }

    private void OnAutoDetectGamePathClick(object? sender, RoutedEventArgs e)
    {
        var detectedPath = DetectUserDataPath();
        if (!string.IsNullOrEmpty(detectedPath))
        {
            var gamePathTextBox = this.FindControl<TextBox>("GamePathTextBox");
            if (gamePathTextBox != null)
            {
                gamePathTextBox.Text = detectedPath;
                ValidateGamePath(detectedPath);
            }
        }
        else
        {
            var validation = this.FindControl<TextBlock>("GamePathValidation");
            if (validation != null)
            {
                validation.Text = "Could not auto-detect user data path";
                validation.Foreground = Brushes.Orange;
            }
        }
    }

    private void ValidateBaseGamePath(string? path)
    {
        var validation = this.FindControl<TextBlock>("BaseGamePathValidation");
        if (validation == null) return;

        if (string.IsNullOrEmpty(path))
        {
            validation.Text = "";
            return;
        }

        if (!Directory.Exists(path))
        {
            validation.Text = "Directory does not exist";
            validation.Foreground = Brushes.Red;
            return;
        }

        var dataPath = Path.Combine(path, "data");
        if (Directory.Exists(dataPath))
        {
            validation.Text = "Valid installation path detected";
            validation.Foreground = Brushes.Green;
        }
        else
        {
            validation.Text = "Warning: 'data' folder not found - may not be correct path";
            validation.Foreground = Brushes.Orange;
        }
    }

    private void ValidateGamePath(string? path)
    {
        var validation = this.FindControl<TextBlock>("GamePathValidation");
        if (validation == null) return;

        if (string.IsNullOrEmpty(path))
        {
            validation.Text = "";
            return;
        }

        if (!Directory.Exists(path))
        {
            validation.Text = "Directory does not exist";
            validation.Foreground = Brushes.Red;
            return;
        }

        var modulesPath = Path.Combine(path, "modules");
        if (Directory.Exists(modulesPath))
        {
            validation.Text = "Valid user data path detected";
            validation.Foreground = Brushes.Green;
        }
        else
        {
            validation.Text = "Warning: 'modules' folder not found - may not be correct path";
            validation.Foreground = Brushes.Orange;
        }
    }

    private string? DetectBaseGamePath()
    {
        var possiblePaths = new List<string>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            possiblePaths.AddRange(new[]
            {
                @"C:\Program Files (x86)\Steam\steamapps\common\Neverwinter Nights",
                @"C:\Program Files\Steam\steamapps\common\Neverwinter Nights",
                @"C:\GOG Games\Neverwinter Nights Enhanced Edition",
                @"C:\Program Files\Beamdog\Neverwinter Nights"
            });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            possiblePaths.AddRange(new[]
            {
                Path.Combine(home, ".steam/steam/steamapps/common/Neverwinter Nights"),
                Path.Combine(home, "GOG Games/Neverwinter Nights")
            });
        }

        foreach (var path in possiblePaths)
        {
            if (Directory.Exists(path) && Directory.Exists(Path.Combine(path, "data")))
            {
                return path;
            }
        }

        return null;
    }

    private string? DetectUserDataPath()
    {
        var possiblePaths = new List<string>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            possiblePaths.Add(Path.Combine(docs, "Neverwinter Nights"));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            possiblePaths.Add(Path.Combine(home, ".local/share/Neverwinter Nights"));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            possiblePaths.Add(Path.Combine(home, "Library/Application Support/Neverwinter Nights"));
        }

        foreach (var path in possiblePaths)
        {
            if (Directory.Exists(path))
            {
                return path;
            }
        }

        return null;
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
        var themes = ThemeManager.Instance.AvailableThemes
            .Where(t => !t.Plugin.Tags.Contains("easter-egg"))
            .OrderBy(t => t.Plugin.Name)
            .ToList();

        comboBox.ItemsSource = themes;
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
            catch
            {
                // Invalid font family - ignore
            }
        }
    }

    #endregion

    #region Logging Settings

    private void LoadLoggingSettings()
    {
        var settings = SettingsService.Instance;

        // Log Level
        var logLevelComboBox = this.FindControl<ComboBox>("LogLevelComboBox");
        if (logLevelComboBox != null)
        {
            logLevelComboBox.ItemsSource = Enum.GetValues(typeof(LogLevel)).Cast<LogLevel>().ToList();
            logLevelComboBox.SelectedItem = settings.CurrentLogLevel;
        }

        // Log Retention
        var logRetentionSlider = this.FindControl<Slider>("LogRetentionSlider");
        var logRetentionLabel = this.FindControl<TextBlock>("LogRetentionLabel");
        if (logRetentionSlider != null)
        {
            logRetentionSlider.Value = settings.LogRetentionSessions;
            if (logRetentionLabel != null)
            {
                logRetentionLabel.Text = $"{settings.LogRetentionSessions} sessions";
            }
        }
    }

    private void OnLogLevelChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        var comboBox = sender as ComboBox;
        if (comboBox?.SelectedItem is LogLevel level)
        {
            SettingsService.Instance.CurrentLogLevel = level;
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Log level changed to: {level}");
        }
    }

    private void OnLogRetentionChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;

        var slider = sender as Slider;
        if (slider == null) return;

        var retention = (int)slider.Value;
        var logRetentionLabel = this.FindControl<TextBlock>("LogRetentionLabel");
        if (logRetentionLabel != null)
        {
            logRetentionLabel.Text = $"{retention} sessions";
        }

        SettingsService.Instance.LogRetentionSessions = retention;
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
}
