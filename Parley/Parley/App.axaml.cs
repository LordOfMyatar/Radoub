using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using DialogEditor.ViewModels;
using DialogEditor.Views;
using DialogEditor.Services;
using DialogEditor.Plugins;
using Parley.Services;
using System.ComponentModel;

namespace DialogEditor;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Check for safe mode (command line)
        // Note: Safe mode already backed up config folder in Program.cs,
        // so SettingsService will use factory defaults (no config file exists)
        var isSafeMode = CommandLineService.Options.SafeMode;

        if (isSafeMode)
        {
            PluginSettingsService.Instance.SafeMode = true;
            UnifiedLogger.LogApplication(LogLevel.INFO, "Safe mode enabled - using factory defaults");
        }

        // Discover and apply themes
        ThemeManager.Instance.DiscoverThemes();

        var themeId = SettingsService.Instance.CurrentThemeId;
        if (string.IsNullOrEmpty(themeId))
        {
            themeId = "org.parley.theme.light"; // Default if not set
        }

        if (!ThemeManager.Instance.ApplyTheme(themeId))
        {
            // If preferred theme fails, try default light theme
            ThemeManager.Instance.ApplyTheme("org.parley.theme.light");
        }

        // Apply font size and family from settings
        ApplyFontSize(SettingsService.Instance.FontSize);
        ApplyFontFamily(SettingsService.Instance.FontFamily);

        // Apply scrollbar auto-hide preference (Issue #63)
        ApplyScrollbarAutoHide(SettingsService.Instance.AllowScrollbarAutoHide);

        // Subscribe to settings changes
        SettingsService.Instance.PropertyChanged += OnSettingsPropertyChanged;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            // Don't set DataContext here - MainWindow sets its own ViewModel in constructor
            desktop.MainWindow = new MainWindow();
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
}