using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Media;
using System.ComponentModel;
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

        // Discover and apply theme
        ThemeManager.Instance.DiscoverThemes();
        var themeId = SettingsService.Instance.CurrentThemeId;
        if (!ThemeManager.Instance.ApplyTheme(themeId))
        {
            // Fallback to light theme
            ThemeManager.Instance.ApplyTheme("org.manifest.theme.light");
        }

        // Apply font overrides from settings (after theme)
        ApplyFontSettings();

        // Subscribe to settings changes for dynamic theme/font updates
        SettingsService.Instance.PropertyChanged += OnSettingsPropertyChanged;

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
}
