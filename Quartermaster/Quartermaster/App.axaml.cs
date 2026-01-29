using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Media;
using System;
using System.ComponentModel;
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

        // Check for SafeMode
        var isSafeMode = Program.SafeMode?.SafeModeActive ?? false;

        if (isSafeMode)
        {
            // SafeMode: Reset visual settings to safe defaults
            ApplySafeModeDefaults();
            UnifiedLogger.LogApplication(LogLevel.INFO, "SafeMode enabled - visual settings reset to defaults");
        }

        // Record tool launch for easter egg tracking
        EasterEggService.Instance.RecordToolLaunch("Quartermaster");

        // Initialize and discover themes
        ThemeManager.Initialize("Quartermaster");
        ThemeManager.Instance.DiscoverThemes();

        string themeId;
        if (isSafeMode)
        {
            // SafeMode forces light theme
            themeId = "org.quartermaster.theme.light";
        }
        else
        {
            themeId = SettingsService.Instance.CurrentThemeId;
        }

        // Use ApplyEffectiveTheme to check for shared Radoub-level theme first
        if (!ThemeManager.Instance.ApplyEffectiveTheme(themeId))
        {
            // Fallback to light theme
            ThemeManager.Instance.ApplyTheme("org.quartermaster.theme.light");
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

        // Initialize spell-checking (async, non-blocking)
        // No cancellation needed - singleton service that should complete during app lifetime
        _ = InitializeSpellCheckAsync();
    }

    /// <summary>
    /// Apply SafeMode defaults to settings - resets theme and fonts.
    /// </summary>
    private void ApplySafeModeDefaults()
    {
        // Reset theme to light
        SettingsService.Instance.CurrentThemeId = "org.quartermaster.theme.light";

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
            var baseSize = SafeModeService.DefaultFontSize;

            Resources["GlobalFontSize"] = baseSize;
            Resources["FontSizeXSmall"] = Math.Max(8, baseSize - 4);
            Resources["FontSizeSmall"] = Math.Max(9, baseSize - 3);
            Resources["FontSizeNormal"] = baseSize;
            Resources["FontSizeMedium"] = baseSize + 2;
            Resources["FontSizeLarge"] = baseSize + 4;
            Resources["FontSizeXLarge"] = baseSize + 6;
            Resources["FontSizeTitle"] = baseSize + 10;
            Resources["GlobalFontFamily"] = FontFamily.Default;

            // Update portrait dimensions based on font scale (base: 64x100 @ font 14)
            var scale = baseSize / 14.0;
            Resources["PortraitWidth"] = 64.0 * scale;
            Resources["PortraitHeight"] = 100.0 * scale;

            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Applied SafeMode font settings");
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
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

            // Update base font size
            Resources["GlobalFontSize"] = baseSize;

            // Update derived font sizes (must match ThemeManager.ApplyFontSettings logic)
            Resources["FontSizeXSmall"] = Math.Max(8, baseSize - 4);   // 10 @ base 14
            Resources["FontSizeSmall"] = Math.Max(9, baseSize - 3);    // 11 @ base 14
            Resources["FontSizeNormal"] = baseSize;                     // 14 @ base 14
            Resources["FontSizeMedium"] = baseSize + 2;                 // 16 @ base 14
            Resources["FontSizeLarge"] = baseSize + 4;                  // 18 @ base 14
            Resources["FontSizeXLarge"] = baseSize + 6;                 // 20 @ base 14
            Resources["FontSizeTitle"] = baseSize + 10;                 // 24 @ base 14

            // Update portrait dimensions based on font scale (base: 64x100 @ font 14)
            var scale = baseSize / 14.0;
            Resources["PortraitWidth"] = 64.0 * scale;
            Resources["PortraitHeight"] = 100.0 * scale;

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
                catch (ArgumentException ex)
                {
                    // Invalid font family - fall back to system default
                    Resources["GlobalFontFamily"] = FontFamily.Default;
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Invalid font family '{settings.FontFamily}': {ex.Message}. Using system default.");
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
