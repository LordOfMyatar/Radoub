using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Media;
using System;
using System.IO;
using System.Linq;
using Avalonia.Markup.Xaml;
using MerchantEditor.Services;
using Radoub.Formats.Logging;
using MerchantEditor.Views;
using Radoub.UI.Services;
using ThemeManager = Radoub.UI.Services.ThemeManager;
using EasterEggService = Radoub.UI.Services.EasterEggService;

namespace MerchantEditor;

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
            UnifiedLogger.LogApplication(LogLevel.INFO, "SafeMode enabled - visual settings reset to defaults");
        }

        // Record tool launch for easter egg tracking
        EasterEggService.Instance.RecordToolLaunch("Fence");

        // Initialize and discover themes
        ThemeManager.Initialize("Fence");
        ThemeManager.Instance.DiscoverThemes();

        if (isSafeMode)
        {
            ThemeManager.Instance.ApplyTheme("org.radoub.theme.light");
        }
        else
        {
            ThemeManager.Instance.ApplySharedTheme();
        }

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

            // Re-read shared settings when window regains focus (picks up Trebuchet changes)
            desktop.MainWindow.Activated += OnMainWindowActivated;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnMainWindowActivated(object? sender, EventArgs e)
    {
        Radoub.Formats.Settings.RadoubSettings.Instance.ReloadSettings();
        ThemeManager.Instance.ApplySharedTheme();
        ApplyFontSettings();
    }

    private void ApplyFontSettings()
    {
        var sharedSettings = Radoub.Formats.Settings.RadoubSettings.Instance;
        var baseSize = (double)sharedSettings.SharedFontSize;
        var fontFamily = sharedSettings.SharedFontFamily;

        if (Resources != null)
        {
            // Update base font size
            Resources["GlobalFontSize"] = baseSize;

            // Update derived font sizes (must match ThemeManager.ApplyFontSettings logic)
            Resources["FontSizeXSmall"] = Math.Max(10, baseSize - 2);  // 12 @ base 14
            Resources["FontSizeSmall"] = Math.Max(11, baseSize - 1);   // 13 @ base 14
            Resources["FontSizeNormal"] = baseSize;                     // 14 @ base 14
            Resources["FontSizeMedium"] = baseSize + 2;                 // 16 @ base 14
            Resources["FontSizeLarge"] = baseSize + 4;                  // 18 @ base 14
            Resources["FontSizeXLarge"] = baseSize + 6;                 // 20 @ base 14
            Resources["FontSizeTitle"] = baseSize + 10;                 // 24 @ base 14

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Applied font size: {baseSize}pt (derived sizes updated)");
        }

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
                Radoub.Formats.Settings.RadoubSettings.Instance.FencePath = exePath;
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Registered Fence path in shared settings: {UnifiedLogger.SanitizePath(exePath)}");
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Could not register tool path: {ex.Message}");
        }
    }
}
