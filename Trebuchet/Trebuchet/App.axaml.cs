using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Media;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia.Markup.Xaml;
using RadoubLauncher.Services;
using Radoub.Formats.Logging;
using RadoubLauncher.Views;
using Radoub.UI.Services;
using ThemeManager = Radoub.UI.Services.ThemeManager;
using EasterEggService = Radoub.UI.Services.EasterEggService;

namespace RadoubLauncher;

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
            // SafeMode: Reset visual settings to safe defaults
            ApplySafeModeDefaults();
            UnifiedLogger.LogApplication(LogLevel.INFO, "SafeMode enabled - visual settings reset to defaults");
        }

        // Record tool launch for easter egg tracking
        EasterEggService.Instance.RecordToolLaunch("Trebuchet");

        // Copy bundled themes to shared folder so other tools can access them
        CopyBundledThemesToSharedFolder();

        // Initialize and discover themes
        ThemeManager.Initialize("Trebuchet");
        ThemeManager.Instance.DiscoverThemes();

        string themeId;
        if (isSafeMode)
        {
            // SafeMode forces light theme
            themeId = "org.radoub.theme.light";
        }
        else
        {
            themeId = SettingsService.Instance.CurrentThemeId;
        }

        if (!ThemeManager.Instance.ApplyEffectiveTheme(themeId, SettingsService.Instance.UseSharedTheme))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Theme '{themeId}' failed to apply, falling back to light theme");
            if (!ThemeManager.Instance.ApplyTheme("org.radoub.theme.light"))
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, "Light theme fallback also failed - UI may render with default Avalonia theme");
            }
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

        // Re-apply font settings whenever theme finishes applying (theme resets font sizes
        // via Dispatcher.Post, so our inline ApplyFontSettings above runs too early)
        ThemeManager.Instance.ThemeApplied += (_, _) => ApplyFontSettings();

        // Clean up old log sessions
        UnifiedLogger.CleanupOldSessions(SettingsService.Instance.LogRetentionSessions);

        // Clean up old backups
        Radoub.UI.Services.BackupCleanupService.CleanupExpiredBackups(
            Radoub.Formats.Settings.RadoubSettings.Instance.BackupRetentionDays);
    }

    /// <summary>
    /// Apply SafeMode defaults to settings - resets theme and fonts.
    /// </summary>
    private void ApplySafeModeDefaults()
    {
        // Reset theme to light
        SettingsService.Instance.CurrentThemeId = "org.radoub.theme.light";

        // Reset fonts to system defaults
        SettingsService.Instance.FontSize = SafeModeService.DefaultFontSize;
        SettingsService.Instance.FontFamily = SafeModeService.DefaultFontFamily;

        UnifiedLogger.LogApplication(LogLevel.INFO, "SafeMode: Reset theme to light, fonts to default");
    }

    // ApplySafeModeDefaults() already resets settings values, so just apply from settings
    private void ApplySafeModeFontSettings() => ApplyFontSettings();

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow();

            // Unsubscribe from singleton events and dispose services on app exit (#1282, #1292)
            desktop.Exit += (_, _) =>
            {
                SettingsService.Instance.PropertyChanged -= OnSettingsPropertyChanged;
                UpdateService.Instance.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SettingsService.CurrentThemeId):
                if (!ThemeManager.Instance.ApplyEffectiveTheme(
                    SettingsService.Instance.CurrentThemeId,
                    SettingsService.Instance.UseSharedTheme))
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"Failed to apply theme '{SettingsService.Instance.CurrentThemeId}' on settings change");
                }
                // Font reapplication handled by ThemeApplied event (theme applies fonts async via Dispatcher.Post)
                break;
            case nameof(SettingsService.FontSize):
            case nameof(SettingsService.FontSizeScale):
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
            var baseSize = (double)settings.FontSize * settings.FontSizeScale;

            // Update base font size
            Resources["GlobalFontSize"] = baseSize;

            // Update derived font sizes (must match ThemeManager.ApplyFontSettings logic)
            Resources["FontSizeXSmall"] = Math.Max(10, baseSize - 2);
            Resources["FontSizeSmall"] = Math.Max(11, baseSize - 1);
            Resources["FontSizeNormal"] = baseSize;
            Resources["FontSizeMedium"] = baseSize + 2;
            Resources["FontSizeLarge"] = baseSize + 4;
            Resources["FontSizeXLarge"] = baseSize + 6;
            Resources["FontSizeTitle"] = baseSize + 10;

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Applied font size: {baseSize:F0}pt (base {settings.FontSize} × {settings.FontSizeScale:P0})");
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
                catch (Exception ex)
                {
                    // Invalid font family - fall back to system default
                    Resources["GlobalFontFamily"] = FontFamily.Default;
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Font family fallback to System Default: {ex.Message}");
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
                Radoub.Formats.Settings.RadoubSettings.Instance.TrebuchetPath = exePath;
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Registered Trebuchet path in shared settings: {UnifiedLogger.SanitizePath(exePath)}");
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Could not register tool path: {ex.Message}");
        }
    }

    /// <summary>
    /// Copy bundled themes to shared themes folder so other Radoub tools can access them.
    /// Only copies themes with org.radoub.theme.* IDs (universal themes).
    /// Existing files are overwritten to ensure users get latest theme updates.
    /// </summary>
    private static void CopyBundledThemesToSharedFolder()
    {
        try
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var bundledThemesDir = Path.Combine(appDir, "Themes");
            var sharedThemesDir = Radoub.Formats.Settings.RadoubSettings.Instance.GetSharedThemesPath();

            if (!Directory.Exists(bundledThemesDir))
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "No bundled themes directory found");
                return;
            }

            Directory.CreateDirectory(sharedThemesDir);

            var copiedCount = 0;
            foreach (var themeFile in Directory.GetFiles(bundledThemesDir, "*.json"))
            {
                try
                {
                    // Only copy universal themes (org.radoub.theme.*)
                    var content = File.ReadAllText(themeFile);
                    if (!content.Contains("\"org.radoub.theme."))
                        continue;

                    var destFile = Path.Combine(sharedThemesDir, Path.GetFileName(themeFile));
                    File.Copy(themeFile, destFile, overwrite: true);
                    copiedCount++;
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Could not copy theme {Path.GetFileName(themeFile)}: {ex.Message}");
                }
            }

            if (copiedCount > 0)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Copied {copiedCount} shared themes to {UnifiedLogger.SanitizePath(sharedThemesDir)}");
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Could not copy bundled themes: {ex.Message}");
        }
    }
}
