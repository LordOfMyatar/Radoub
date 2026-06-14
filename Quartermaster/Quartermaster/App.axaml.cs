using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Media;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;
using Quartermaster.Services;
using Radoub.Formats.Logging;
using Quartermaster.Views;
using Radoub.UI.Services;
using ThemeManager = Radoub.UI.Services.ThemeManager;
using EasterEggService = Radoub.UI.Services.EasterEggService;

namespace Quartermaster;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Register this tool's path in shared Radoub settings
        RegisterToolPath();

        // Record tool launch for easter egg tracking
        EasterEggService.Instance.RecordToolLaunch("Quartermaster");

        // Initialize and discover themes
        ThemeManager.Initialize("Quartermaster");
        ThemeManager.Instance.DiscoverThemes();

        // Apply theme
        ThemeManager.Instance.ApplySharedTheme();

        // Apply font settings
        ApplyFontSettings();

        // Clean up old log sessions
        UnifiedLogger.CleanupOldSessions(SettingsService.Instance.LogRetentionSessions);

        // Clean up old backups
        Radoub.UI.Services.BackupCleanupService.CleanupExpiredBackups(
            Radoub.Formats.Settings.RadoubSettings.Instance.BackupRetentionDays);

        // Initialize spell-checking (async, non-blocking)
        // No cancellation needed - singleton service that should complete during app lifetime
        _ = InitializeSpellCheckAsync();
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

        if (Resources != null)
        {
            var baseSize = sharedSettings.SharedFontSize;

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

            // Update portrait dimensions based on font scale (base: 64x100 @ font 14)
            var scale = baseSize / 14.0;
            Resources["PortraitWidth"] = 64.0 * scale;
            Resources["PortraitHeight"] = 100.0 * scale;

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Applied font size: {baseSize}pt (derived sizes updated)");
        }

        if (Resources != null)
        {
            if (!string.IsNullOrEmpty(sharedSettings.SharedFontFamily))
            {
                try
                {
                    Resources["GlobalFontFamily"] = new FontFamily(sharedSettings.SharedFontFamily);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Applied font family: {sharedSettings.SharedFontFamily}");
                }
                catch (ArgumentException ex)
                {
                    // Invalid font family - fall back to system default
                    Resources["GlobalFontFamily"] = FontFamily.Default;
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Invalid font family '{sharedSettings.SharedFontFamily}': {ex.Message}. Using system default.");
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

    private static async Task InitializeSpellCheckAsync()
    {
        try
        {
            await SpellCheckService.Instance.InitializeAsync();
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Spell-check initialization failed: {ex.Message}");
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
                Radoub.Formats.Settings.RadoubSettings.Instance.QuartermasterPath = exePath;
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Registered Quartermaster path in shared settings: {UnifiedLogger.SanitizePath(exePath)}");
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Could not register tool path: {ex.Message}");
        }
    }
}
