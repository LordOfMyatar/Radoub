using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Media;
using System;
using System.IO;
using System.Linq;
using Avalonia.Markup.Xaml;
using PlaceableEditor.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using PlaceableEditor.Views;
using Radoub.UI.Services;
using ThemeManager = Radoub.UI.Services.ThemeManager;
using EasterEggService = Radoub.UI.Services.EasterEggService;

namespace PlaceableEditor;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Register this tool's path in shared Radoub settings
        RegisterToolPath();

        // Record tool launch for easter egg tracking
        EasterEggService.Instance.RecordToolLaunch("Reliquary");

        // Initialize and discover themes
        ThemeManager.Initialize("Reliquary");
        ThemeManager.Instance.DiscoverThemes();

        // Apply theme
        ThemeManager.Instance.ApplySharedTheme();

        // Apply font settings
        ApplyFontSettings();

        // Log-session and backup cleanup run after first paint (#2647) — see
        // MainWindow's Opened handler.
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
        RadoubSettings.Instance.ReloadSettings();
        ThemeManager.Instance.ApplySharedTheme();
        ApplyFontSettings();
    }

    // Fonts resolve from Trebuchet shared settings via the single shared entry point (#2404).
    private void ApplyFontSettings() => ThemeManager.ApplySharedFontSettings(Resources);

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
                RadoubSettings.Instance.ReliquaryPath = exePath;
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Registered Reliquary path in shared settings: {UnifiedLogger.SanitizePath(exePath)}");
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Could not register tool path: {ex.Message}");
        }
    }
}
