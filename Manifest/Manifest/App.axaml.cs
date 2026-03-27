using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Media;
using System;
using System.IO;
using System.Linq;
using Avalonia.Markup.Xaml;
using Manifest.Services;
using Radoub.Formats.Logging;
using Manifest.Views;
using Radoub.UI.Services;
using ThemeManager = Radoub.UI.Services.ThemeManager;
using EasterEggService = Radoub.UI.Services.EasterEggService;
using SpellCheckService = Radoub.UI.Services.SpellCheckService;

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
            UnifiedLogger.LogApplication(LogLevel.INFO, "SafeMode enabled - using light theme and default fonts");
        }

        // Record tool launch for easter egg tracking
        EasterEggService.Instance.RecordToolLaunch("Manifest");

        // Initialize and discover themes
        ThemeManager.Initialize("Manifest");
        ThemeManager.Instance.DiscoverThemes();

        // Apply theme
        if (isSafeMode)
        {
            ThemeManager.Instance.ApplyTheme("org.radoub.theme.light");
        }
        else
        {
            ThemeManager.Instance.ApplySharedTheme();
        }

        // Apply font settings
        if (isSafeMode)
        {
            ApplySafeModeFontSettings();
        }
        else
        {
            ApplyFontSettings();
        }

        // Clean up old log sessions
        UnifiedLogger.CleanupOldSessions(SettingsService.Instance.LogRetentionSessions);

        // Clean up old backups
        Radoub.UI.Services.BackupCleanupService.CleanupExpiredBackups(
            Radoub.Formats.Settings.RadoubSettings.Instance.BackupRetentionDays);

        // Initialize spell-checking (async, non-blocking)
        _ = SpellCheckService.Instance.InitializeAsync();
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

    private void ApplyFontSettings()
    {
        var sharedSettings = Radoub.Formats.Settings.RadoubSettings.Instance;
        var fontSize = sharedSettings.SharedFontSize;
        var fontFamily = sharedSettings.SharedFontFamily;

        // Apply font size
        if (Resources != null)
        {
            Resources["GlobalFontSize"] = fontSize;
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Applied font size: {fontSize}pt");
        }

        // Apply font family (overrides theme default)
        if (Resources != null)
        {
            if (!string.IsNullOrEmpty(fontFamily))
            {
                try
                {
                    Resources["GlobalFontFamily"] = new FontFamily(fontFamily);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Applied font family: {fontFamily}");
                }
                catch
                {
                    // Invalid font family - fall back to system default
                    Resources["GlobalFontFamily"] = FontFamily.Default;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "Applied font family: System Default (fallback)");
                }
            }
            else
            {
                // Empty string means system default
                Resources["GlobalFontFamily"] = FontFamily.Default;
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Applied font family: System Default");
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
