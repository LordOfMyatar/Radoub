using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Media;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia.Markup.Xaml;
using Manifest.Services;
using Radoub.Formats.Logging;
using Manifest.Views;
using Radoub.UI.Services;
using ThemeManager = Radoub.UI.Services.ThemeManager;
using EasterEggService = Radoub.UI.Services.EasterEggService;

namespace Manifest;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Register this tool's path in shared Radoub settings for cross-tool discovery
        RegisterToolPath();

        // Check for SafeMode
        var isSafeMode = Program.SafeMode?.SafeModeActive ?? false;

        if (isSafeMode)
        {
            // SafeMode: Reset visual settings to safe defaults
            ApplySafeModeDefaults();
            UnifiedLogger.LogApplication(LogLevel.INFO, "SafeMode enabled - visual settings reset to defaults");
        }

        // Record tool launch for easter egg tracking
        EasterEggService.Instance.RecordToolLaunch("Manifest");

        // Initialize and discover themes
        ThemeManager.Initialize("Manifest");
        ThemeManager.Instance.DiscoverThemes();

        string themeId;
        if (isSafeMode)
        {
            // SafeMode forces light theme
            themeId = "org.manifest.theme.light";
        }
        else
        {
            themeId = SettingsService.Instance.CurrentThemeId;
        }

        if (!ThemeManager.Instance.ApplyTheme(themeId))
        {
            // Fallback to light theme
            ThemeManager.Instance.ApplyTheme("org.manifest.theme.light");
        }

        // Apply font overrides from settings (after theme)
        if (isSafeMode)
        {
            ApplySafeModeFontSettings();
        }
        else
        {
            ApplyFontSettings();
        }

        // Subscribe to settings changes for dynamic theme/font updates
        SettingsService.Instance.PropertyChanged += OnSettingsPropertyChanged;

        // Clean up old log sessions
        UnifiedLogger.CleanupOldSessions(SettingsService.Instance.LogRetentionSessions);

        // Initialize spell-checking (async, non-blocking)
        _ = SpellCheckService.Instance.InitializeAsync();
    }

    /// <summary>
    /// Apply SafeMode defaults to settings - resets theme and fonts.
    /// </summary>
    private void ApplySafeModeDefaults()
    {
        // Reset theme to light
        SettingsService.Instance.CurrentThemeId = "org.manifest.theme.light";

        // Reset fonts to system defaults
        SettingsService.Instance.FontSize = SafeModeService.DefaultFontSize;
        SettingsService.Instance.FontFamily = SafeModeService.DefaultFontFamily;

        UnifiedLogger.LogApplication(LogLevel.INFO, "SafeMode: Reset theme to light, fonts to default");
    }

    /// <summary>
    /// Apply SafeMode font settings (system defaults).
    /// </summary>
    private void ApplySafeModeFontSettings()
    {
        if (Resources != null)
        {
            Resources["GlobalFontSize"] = SafeModeService.DefaultFontSize;
            Resources["GlobalFontFamily"] = FontFamily.Default;
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Applied SafeMode font settings");
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit
            DisableAvaloniaDataAnnotationValidation();

            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SettingsService.CurrentThemeId):
                ThemeManager.Instance.ApplyTheme(SettingsService.Instance.CurrentThemeId);
                ApplyFontSettings(); // Re-apply font overrides after theme change
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

        // Apply font size
        if (Resources != null)
        {
            Resources["GlobalFontSize"] = settings.FontSize;
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Applied font size: {settings.FontSize}pt");
        }

        // Apply font family if set (overrides theme default)
        if (!string.IsNullOrEmpty(settings.FontFamily) && Resources != null)
        {
            try
            {
                Resources["GlobalFontFamily"] = new FontFamily(settings.FontFamily);
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Applied font family: {settings.FontFamily}");
            }
            catch
            {
                // Invalid font family - ignore
            }
        }
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    /// <summary>
    /// Register Manifest's executable path in shared Radoub settings.
    /// This allows other Radoub tools (like Parley) to find Manifest.
    /// </summary>
    private static void RegisterToolPath()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                Radoub.Formats.Settings.RadoubSettings.Instance.ManifestPath = exePath;
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Registered Manifest path in shared settings: {UnifiedLogger.SanitizePath(exePath)}");
            }
        }
        catch (Exception ex)
        {
            // Non-critical - log and continue
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Could not register tool path: {ex.Message}");
        }
    }
}
