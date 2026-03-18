using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Media;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia.Markup.Xaml;
using ItemEditor.Services;
using Radoub.Formats.Logging;
using ItemEditor.Views;
using Radoub.UI.Services;
using ThemeManager = Radoub.UI.Services.ThemeManager;
using EasterEggService = Radoub.UI.Services.EasterEggService;

namespace ItemEditor;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Register this tool's path in shared Radoub settings
        RegisterToolPath();

        // Check for SafeMode
        var isSafeMode = Program.SafeMode?.SafeModeActive ?? false;

        if (isSafeMode)
        {
            ApplySafeModeDefaults();
            UnifiedLogger.LogApplication(LogLevel.INFO, "SafeMode enabled - visual settings reset to defaults");
        }

        // Record tool launch for easter egg tracking
        EasterEggService.Instance.RecordToolLaunch("ItemEditor");

        // Initialize and discover themes
        ThemeManager.Initialize("ItemEditor");
        ThemeManager.Instance.DiscoverThemes();

        string themeId;
        if (isSafeMode)
        {
            themeId = "org.radoub.theme.light";
        }
        else
        {
            themeId = SettingsService.Instance.CurrentThemeId;
        }

        // Use ApplyEffectiveTheme to check for shared Radoub-level theme first
        if (!ThemeManager.Instance.ApplyEffectiveTheme(themeId))
        {
            ThemeManager.Instance.ApplyTheme("org.radoub.theme.light");
        }

        // Apply font overrides from settings
        if (isSafeMode)
        {
            ApplySafeModeFontSettings();
        }
        else
        {
            ApplyFontSettings();
        }

        // Subscribe to settings changes
        SettingsService.Instance.PropertyChanged += OnSettingsPropertyChanged;

        // Clean up old log sessions
        UnifiedLogger.CleanupOldSessions(SettingsService.Instance.LogRetentionSessions);
    }

    private void ApplySafeModeDefaults()
    {
        SettingsService.Instance.CurrentThemeId = "org.radoub.theme.light";
        SettingsService.Instance.FontSize = SafeModeService.DefaultFontSize;
        SettingsService.Instance.FontFamily = SafeModeService.DefaultFontFamily;
        UnifiedLogger.LogApplication(LogLevel.INFO, "SafeMode: Reset theme to light, fonts to default");
    }

    private void ApplySafeModeFontSettings()
    {
        if (Resources != null)
        {
            var baseSize = SafeModeService.DefaultFontSize;
            Resources["GlobalFontSize"] = baseSize;
            Resources["FontSizeXSmall"] = Math.Max(10, baseSize - 2);
            Resources["FontSizeSmall"] = Math.Max(11, baseSize - 1);
            Resources["FontSizeNormal"] = baseSize;
            Resources["FontSizeMedium"] = baseSize + 2;
            Resources["FontSizeLarge"] = baseSize + 4;
            Resources["FontSizeXLarge"] = baseSize + 6;
            Resources["FontSizeTitle"] = baseSize + 10;
            Resources["GlobalFontFamily"] = FontFamily.Default;
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Applied SafeMode font settings");
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow();

            // Unsubscribe from singleton events on app exit (#1282)
            desktop.Exit += (_, _) =>
                SettingsService.Instance.PropertyChanged -= OnSettingsPropertyChanged;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SettingsService.CurrentThemeId):
                ThemeManager.Instance.ApplyTheme(SettingsService.Instance.CurrentThemeId);
                ApplyFontSettings();
                break;
            case nameof(SettingsService.FontSize):
            case nameof(SettingsService.FontFamily):
                ApplyFontSettings();
                break;
        }
    }

    private void ApplyFontSettings()
    {
        var settings = SettingsService.Instance;

        if (Resources != null)
        {
            var baseSize = (double)settings.FontSize;
            Resources["GlobalFontSize"] = baseSize;
            Resources["FontSizeXSmall"] = Math.Max(10, baseSize - 2);
            Resources["FontSizeSmall"] = Math.Max(11, baseSize - 1);
            Resources["FontSizeNormal"] = baseSize;
            Resources["FontSizeMedium"] = baseSize + 2;
            Resources["FontSizeLarge"] = baseSize + 4;
            Resources["FontSizeXLarge"] = baseSize + 6;
            Resources["FontSizeTitle"] = baseSize + 10;
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Applied font size: {settings.FontSize}pt (derived sizes updated)");
        }

        if (Resources != null)
        {
            if (!string.IsNullOrEmpty(settings.FontFamily))
            {
                try
                {
                    Resources["GlobalFontFamily"] = new FontFamily(settings.FontFamily);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Applied font family: {settings.FontFamily}");
                }
                catch
                {
                    Resources["GlobalFontFamily"] = FontFamily.Default;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "Applied font family: System Default (fallback)");
                }
            }
            else
            {
                Resources["GlobalFontFamily"] = FontFamily.Default;
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Applied font family: System Default");
            }
        }
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    private static void RegisterToolPath()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                Radoub.Formats.Settings.RadoubSettings.Instance.ItemEditorPath = exePath;
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Registered ItemEditor path in shared settings: {UnifiedLogger.SanitizePath(exePath)}");
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Could not register tool path: {ex.Message}");
        }
    }
}
