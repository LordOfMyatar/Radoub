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
using RadoubLauncher.ViewModels;
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

        // Record tool launch for easter egg tracking
        EasterEggService.Instance.RecordToolLaunch("Trebuchet");

        // Copy bundled themes to shared folder so other tools can access them
        CopyBundledThemesToSharedFolder();

        // Initialize and discover themes
        ThemeManager.Initialize("Trebuchet");
        ThemeManager.Instance.DiscoverThemes();

        if (!ThemeManager.Instance.ApplySharedTheme())
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "Shared theme failed to apply, falling back to light theme");
            if (!ThemeManager.Instance.ApplyTheme("org.radoub.theme.light"))
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, "Light theme fallback also failed - UI may render with default Avalonia theme");
            }
        }

        // Apply font overrides from settings
        ApplyFontSettings();

        // Subscribe to shared settings changes (font size is the global SharedFontSize, #2152)
        Radoub.Formats.Settings.RadoubSettings.Instance.PropertyChanged += OnSharedSettingsPropertyChanged;

        // Re-apply font settings whenever theme finishes applying (theme resets font sizes
        // via Dispatcher.Post, so our inline ApplyFontSettings above runs too early)
        ThemeManager.Instance.ThemeApplied += (_, _) => ApplyFontSettings();

        // Clean up old log sessions
        UnifiedLogger.CleanupOldSessions(SettingsService.Instance.LogRetentionSessions);

        // Clean up old backups
        Radoub.UI.Services.BackupCleanupService.CleanupExpiredBackups(
            Radoub.Formats.Settings.RadoubSettings.Instance.BackupRetentionDays);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;

            // First-run / version-gate setup (#1020, #2419). The tabbed Settings window
            // doubles as setup; shown over the main window (non-blocking) once it is up,
            // when this is a first run, a newer build added settings, or a required
            // no-default setting is unfilled and unacknowledged.
            mainWindow.Opened += OnMainWindowOpenedShowSetupIfNeeded;

            // Unsubscribe from singleton events and dispose services on app exit (#1282, #1292)
            desktop.Exit += (_, _) =>
            {
                Radoub.Formats.Settings.RadoubSettings.Instance.PropertyChanged -= OnSharedSettingsPropertyChanged;
                UpdateService.Instance.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// On main-window open, decide whether the first-run / version-gate setup should
    /// appear and show the tabbed Settings window in setup mode non-modally over the
    /// main window (#1020, #2419). Fires once — the handler unsubscribes itself.
    /// </summary>
    private void OnMainWindowOpenedShowSetupIfNeeded(object? sender, EventArgs e)
    {
        if (sender is not Avalonia.Controls.Window mainWindow)
            return;

        mainWindow.Opened -= OnMainWindowOpenedShowSetupIfNeeded;

        var settings = Radoub.Formats.Settings.RadoubSettings.Instance;

        // Gap registry. On first run every step is reviewed once (setup fires regardless
        // of these flags). The IsSatisfied / HasGoodDefault flags govern the welcome-back
        // path: a required no-default setting that is unsatisfied and unacknowledged
        // re-opens setup. Today the game path is the only no-default setting;
        // appearance/logging/backup have good defaults and are first-run review only.
        var gaps = new[]
        {
            new WizardGap(SettingsWindowViewModel.GapGamePath, settings.HasGamePaths, HasGoodDefault: false),
            new WizardGap(SettingsWindowViewModel.GapAppearance, true, HasGoodDefault: true),
            new WizardGap(SettingsWindowViewModel.GapLogging, true, HasGoodDefault: true),
            new WizardGap(SettingsWindowViewModel.GapBackup, true, HasGoodDefault: true),
        };

        // Version gate (#2419): re-prompt once when a newer build raised the setup
        // review version above what the user last completed setup against.
        var decision = WizardGapService.Decide(
            gaps, settings.AcknowledgedWizardGaps, settings.WizardHasRun,
            settings.LastSetupVersion, WizardGapService.SetupReviewVersion);
        if (!decision.ShouldShow)
            return;

        var mode = decision.Mode == WizardMode.Welcome
            ? SettingsSetupMode.Welcome
            : SettingsSetupMode.WelcomeBack;

        var setupWindow = new SettingsWindow(mode);
        setupWindow.Show(mainWindow);
    }

    private void OnSharedSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Radoub.Formats.Settings.RadoubSettings.SharedFontSize) ||
            e.PropertyName == nameof(Radoub.Formats.Settings.RadoubSettings.SharedFontFamily))
        {
            ApplyFontSettings();
        }
    }

    private void ApplyFontSettings()
    {
        var radoubSettings = Radoub.Formats.Settings.RadoubSettings.Instance;

        if (Resources != null)
        {
            // SharedFontSize is the global font size in points — Trebuchet is the authority
            // and applies it directly (no scale multiplier). All tools read the same value (#2152).
            var baseSize = radoubSettings.SharedFontSize;

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

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Applied font size: {baseSize:F0}pt (SharedFontSize)");
        }

        if (Resources != null)
        {
            var fontFamily = radoubSettings.SharedFontFamily;
            if (!string.IsNullOrEmpty(fontFamily))
            {
                try
                {
                    Resources["GlobalFontFamily"] = new FontFamily(fontFamily);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Applied font family: {fontFamily}");
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
                    if (File.Exists(destFile) && File.GetLastWriteTimeUtc(destFile) >= File.GetLastWriteTimeUtc(themeFile))
                        continue; // Destination is same age or newer, skip copy

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
