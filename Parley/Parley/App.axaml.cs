using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System;
using System.IO;
using System.Linq;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using DialogEditor.ViewModels;
using DialogEditor.Views;
using DialogEditor.Services;
using System.ComponentModel;
using Radoub.Formats.Logging;
using Radoub.UI.Services;
using ThemeManager = Radoub.UI.Services.ThemeManager;
using EasterEggService = Radoub.UI.Services.EasterEggService;

namespace DialogEditor;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Register this tool's path in shared Radoub settings for cross-tool discovery
        RegisterToolPath();

        // Clean up any leftover temp files from previous sessions (fire-and-forget)
        _ = System.Threading.Tasks.Task.Run(CleanupSoundBrowserTempFiles);

        // Check for safe mode (command line)
        var isSafeMode = Program.SafeMode?.SafeModeActive ?? false;

        if (isSafeMode)
        {
            // SafeMode: Reset visual settings to safe defaults
            ApplySafeModeDefaults();
            UnifiedLogger.LogApplication(LogLevel.INFO, "SafeMode enabled - visual settings reset to defaults");
        }

        // Initialize spell-checking (async, non-blocking)
        _ = SpellCheckService.Instance.InitializeAsync();

        // Record tool launch for easter egg tracking
        EasterEggService.Instance.RecordToolLaunch("Parley");

        // Initialize and discover themes
        ThemeManager.Initialize("Parley");
        ThemeManager.Instance.DiscoverThemes();

        string themeId;
        if (isSafeMode)
        {
            // SafeMode forces light theme
            themeId = "org.parley.theme.light";
        }
        else
        {
            themeId = SettingsService.Instance.CurrentThemeId;
            if (string.IsNullOrEmpty(themeId))
            {
                themeId = "org.parley.theme.light"; // Default if not set
            }
        }

        if (!ThemeManager.Instance.ApplyTheme(themeId))
        {
            // If preferred theme fails, try default light theme
            ThemeManager.Instance.ApplyTheme("org.parley.theme.light");
        }

        // Apply font size and family from settings (or defaults if SafeMode)
        if (isSafeMode)
        {
            ApplyFontSize(SafeModeService.DefaultFontSize);
            ApplyFontFamily(SafeModeService.DefaultFontFamily);
        }
        else
        {
            ApplyFontSize(SettingsService.Instance.FontSize);
            ApplyFontFamily(SettingsService.Instance.FontFamily);
        }

        // Apply scrollbar auto-hide preference (Issue #63)
        ApplyScrollbarAutoHide(SettingsService.Instance.AllowScrollbarAutoHide);

        // Subscribe to settings changes
        SettingsService.Instance.PropertyChanged += OnSettingsPropertyChanged;
    }

    /// <summary>
    /// Apply SafeMode defaults to settings - resets theme, fonts, and flowview.
    /// </summary>
    private void ApplySafeModeDefaults()
    {
        // Reset theme to light
        SettingsService.Instance.CurrentThemeId = "org.parley.theme.light";

        // Reset fonts to system defaults
        SettingsService.Instance.FontSize = SafeModeService.DefaultFontSize;
        SettingsService.Instance.FontFamily = SafeModeService.DefaultFontFamily;

        // Disable FlowView (can cause issues)
        SettingsService.Instance.FlowchartVisible = false;
        SettingsService.Instance.FlowchartWindowOpen = false;

        UnifiedLogger.LogApplication(LogLevel.INFO, "SafeMode: Reset theme to light, fonts to default, FlowView disabled");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            // Don't set DataContext here - MainWindow sets its own ViewModel in constructor
            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;

            // Check if SafeMode is active - show dialog after main window is set
            var isSafeMode = Program.SafeMode?.SafeModeActive ?? false;
            if (isSafeMode)
            {
                // Show SafeMode dialog once the window is loaded
                mainWindow.Opened += async (_, _) =>
                {
                    var dialog = new SafeModeDialog();
                    await dialog.ShowDialog<object?>(mainWindow);

                    if (!dialog.ShouldContinue)
                    {
                        // User chose to exit
                        desktop.Shutdown();
                        return;
                    }

                    // Apply optional cleanup choices
                    if (dialog.ClearScrap && Program.SafeMode != null)
                    {
                        Program.SafeMode.ClearScrapData();
                    }
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsService.FontSize))
        {
            ApplyFontSize(SettingsService.Instance.FontSize);
        }
        else if (e.PropertyName == nameof(SettingsService.FontFamily))
        {
            ApplyFontFamily(SettingsService.Instance.FontFamily);
        }
        else if (e.PropertyName == nameof(SettingsService.CurrentThemeId))
        {
            // Theme changed - apply new theme
            var themeId = SettingsService.Instance.CurrentThemeId;
            if (!string.IsNullOrEmpty(themeId))
            {
                ThemeManager.Instance.ApplyTheme(themeId);
            }
        }
        else if (e.PropertyName == nameof(SettingsService.AllowScrollbarAutoHide))
        {
            ApplyScrollbarAutoHide(SettingsService.Instance.AllowScrollbarAutoHide);
        }
    }

    /// <summary>
    /// Apply font size globally to all UI elements
    /// Fixes issue #58 - Font sizing across UI elements
    /// </summary>
    public static void ApplyFontSize(double fontSize)
    {
        if (Application.Current?.Resources != null)
        {
            Application.Current.Resources["GlobalFontSize"] = fontSize;
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Applied global font size: {fontSize}pt");
        }
    }

    /// <summary>
    /// Apply font family globally to all UI elements
    /// Fixes issue #59 - Font selection
    /// </summary>
    public static void ApplyFontFamily(string fontFamilyName)
    {
        if (Application.Current?.Resources != null)
        {
            try
            {
                // If empty or null, use system default
                if (string.IsNullOrWhiteSpace(fontFamilyName))
                {
                    Application.Current.Resources["GlobalFontFamily"] = FontFamily.Default;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "Applied system default font");
                }
                else
                {
                    var fontFamily = new FontFamily(fontFamilyName);
                    Application.Current.Resources["GlobalFontFamily"] = fontFamily;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Applied global font family: {fontFamilyName}");
                }
            }
            catch
            {
                // Fallback to system default if font not found
                Application.Current.Resources["GlobalFontFamily"] = FontFamily.Default;
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Font '{fontFamilyName}' not found, using system default");
            }
        }
    }

    /// <summary>
    /// Apply scrollbar auto-hide preference globally
    /// Fixes issue #63 - Scrollbar usability improvements
    /// </summary>
    public static void ApplyScrollbarAutoHide(bool allowAutoHide)
    {
        if (Application.Current?.Resources != null)
        {
            Application.Current.Resources["AllowScrollbarAutoHide"] = allowAutoHide;
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Applied scrollbar auto-hide: {allowAutoHide}");
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
    /// Register Parley's executable path in shared Radoub settings.
    /// This allows other Radoub tools (like Manifest) to find Parley.
    /// </summary>
    private static void RegisterToolPath()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                Radoub.Formats.Settings.RadoubSettings.Instance.ParleyPath = exePath;
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Registered Parley path in shared settings: {UnifiedLogger.SanitizePath(exePath)}");
            }
        }
        catch (Exception ex)
        {
            // Non-critical - log and continue
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Could not register tool path: {ex.Message}");
        }
    }

    /// <summary>
    /// Clean up leftover temp files from Sound Browser (from previous sessions or crashes).
    /// Files: pv_*.wav (validation) and ps_*.wav (playback)
    /// </summary>
    private static void CleanupSoundBrowserTempFiles()
    {
        try
        {
            var tempDir = Path.GetTempPath();
            var deletedCount = 0;

            // Clean up validation temp files (pv_*.wav)
            foreach (var file in Directory.GetFiles(tempDir, "pv_*.wav"))
            {
                try
                {
                    File.Delete(file);
                    deletedCount++;
                }
                catch
                {
                    // File may be in use, skip
                }
            }

            // Clean up playback temp files (ps_*.wav)
            foreach (var file in Directory.GetFiles(tempDir, "ps_*.wav"))
            {
                try
                {
                    File.Delete(file);
                    deletedCount++;
                }
                catch
                {
                    // File may be in use, skip
                }
            }

            if (deletedCount > 0)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Cleaned up {deletedCount} leftover Sound Browser temp file(s)");
            }
        }
        catch (Exception ex)
        {
            // Non-critical - log and continue
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Could not clean up Sound Browser temp files: {ex.Message}");
        }
    }
}