using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Manifest.Services;
using Manifest.Views;

namespace Manifest;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Apply font size from settings
        ApplyFontSize(SettingsService.Instance.FontSize);

        // Clean up old log sessions
        UnifiedLogger.CleanupOldSessions(SettingsService.Instance.LogRetentionSessions);
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

    /// <summary>
    /// Apply font size globally to all UI elements
    /// </summary>
    public static void ApplyFontSize(double fontSize)
    {
        if (Application.Current?.Resources != null)
        {
            Application.Current.Resources["GlobalFontSize"] = fontSize;
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Applied global font size: {fontSize}pt");
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
}
