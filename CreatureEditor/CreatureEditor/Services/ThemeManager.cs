using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using CreatureEditor.Models;

namespace CreatureEditor.Services;

/// <summary>
/// Manages theme loading, application, and switching for CreatureEditor.
/// </summary>
public class ThemeManager
{
    private static ThemeManager? _instance;
    public static ThemeManager Instance => _instance ??= new ThemeManager();

    private readonly Dictionary<string, ThemeManifest> _themes = new();
    private ThemeManifest? _currentTheme;

    public event EventHandler? ThemeApplied;

    private readonly List<string> _themeDirectories = new();

    public IReadOnlyList<ThemeManifest> AvailableThemes => _themes.Values.ToList();
    public ThemeManifest? CurrentTheme => _currentTheme;

    private ThemeManager()
    {
        InitializeThemeDirectories();
    }

    private void InitializeThemeDirectories()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var officialThemes = Path.Combine(appDir, "Themes");
        if (!Directory.Exists(officialThemes))
        {
            Directory.CreateDirectory(officialThemes);
        }
        _themeDirectories.Add(officialThemes);

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var userThemes = Path.Combine(userProfile, "Radoub", "CreatureEditor", "Themes");
        if (!Directory.Exists(userThemes))
        {
            Directory.CreateDirectory(userThemes);
        }
        _themeDirectories.Add(userThemes);

        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"Theme directories initialized: {_themeDirectories.Count} locations");
    }

    public void DiscoverThemes()
    {
        _themes.Clear();

        foreach (var directory in _themeDirectories)
        {
            if (!Directory.Exists(directory))
                continue;

            foreach (var themeFile in Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    var manifest = LoadThemeManifest(themeFile);
                    if (manifest != null)
                    {
                        _themes[manifest.Plugin.Id] = manifest;
                        UnifiedLogger.LogApplication(LogLevel.DEBUG,
                            $"Discovered theme: {manifest.Plugin.Name} ({manifest.Plugin.Id})");
                    }
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"Failed to load theme: {ex.Message}");
                }
            }
        }

        UnifiedLogger.LogApplication(LogLevel.INFO, $"Discovered {_themes.Count} themes");
    }

    private ThemeManifest? LoadThemeManifest(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        var manifest = JsonSerializer.Deserialize<ThemeManifest>(json, options);

        if (manifest != null)
        {
            manifest.SourcePath = filePath;
        }

        return manifest;
    }

    public bool ApplyTheme(string themeId)
    {
        if (!_themes.TryGetValue(themeId, out var theme))
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Theme not found: {themeId}");
            return false;
        }

        return ApplyTheme(theme);
    }

    public bool ApplyTheme(ThemeManifest theme)
    {
        try
        {
            var app = Application.Current;
            if (app == null)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, "Application instance not available");
                return false;
            }

            var targetVariant = theme.BaseTheme.ToLower() switch
            {
                "dark" => ThemeVariant.Dark,
                "light" => ThemeVariant.Light,
                _ => ThemeVariant.Light
            };

            if (app.RequestedThemeVariant != targetVariant)
            {
                app.RequestedThemeVariant = targetVariant;
            }

            if (theme.Colors != null)
            {
                ApplyColors(app.Resources, theme.Colors);
            }

            if (theme.Fonts != null)
            {
                ApplyFonts(app.Resources, theme.Fonts);
            }

            if (theme.Spacing != null)
            {
                ApplySpacing(app.Resources, theme.Spacing);
            }

            _currentTheme = theme;

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Applied theme: {theme.Plugin.Name} ({theme.Plugin.Id})");

            ThemeApplied?.Invoke(this, EventArgs.Empty);

            return true;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"Failed to apply theme {theme.Plugin.Name}: {ex.Message}");
            return false;
        }
    }

    private void ApplyColors(IResourceDictionary resources, ThemeColors colors)
    {
        if (!string.IsNullOrEmpty(colors.Background))
        {
            var bgColor = Color.Parse(colors.Background);
            var bgBrush = new SolidColorBrush(bgColor);

            resources["SystemChromeMediumColor"] = bgColor;
            resources["SystemChromeMediumLowColor"] = bgColor;
            resources["SystemChromeHighColor"] = bgColor;
            resources["SystemRegionColor"] = bgColor;

            resources["SolidBackgroundFillColorBase"] = bgBrush;
            resources["SolidBackgroundFillColorBaseBrush"] = bgBrush;
            resources["LayerFillColorDefault"] = bgBrush;
            resources["LayerFillColorDefaultBrush"] = bgBrush;
            resources["ThemeBackground"] = bgBrush;
        }

        if (!string.IsNullOrEmpty(colors.Text))
        {
            var textColor = Color.Parse(colors.Text);
            var textBrush = new SolidColorBrush(textColor);

            resources["SystemBaseHighColor"] = textColor;
            resources["SystemBaseMediumHighColor"] = textColor;
            resources["SystemBaseMediumColor"] = textColor;

            resources["TextControlForeground"] = textBrush;
            resources["SystemControlForegroundBaseHighBrush"] = textBrush;
        }

        if (!string.IsNullOrEmpty(colors.Accent))
        {
            var accentColor = Color.Parse(colors.Accent);
            var accentBrush = new SolidColorBrush(accentColor);

            resources["SystemAccentColor"] = accentColor;
            resources["SystemControlHighlightAccentBrush"] = accentBrush;
        }

        if (!string.IsNullOrEmpty(colors.Selection))
        {
            var selColor = Color.Parse(colors.Selection);
            var selBrush = new SolidColorBrush(selColor);

            resources["SystemListLowColor"] = selColor;
            resources["SystemListMediumColor"] = selColor;
            resources["SystemControlHighlightListLowBrush"] = selBrush;
        }

        if (!string.IsNullOrEmpty(colors.Border))
        {
            var borderColor = Color.Parse(colors.Border);
            var borderBrush = new SolidColorBrush(borderColor);

            resources["SystemBaseMediumLowColor"] = borderColor;
            resources["SystemBaseLowColor"] = borderColor;
            resources["SystemControlForegroundBaseMediumLowBrush"] = borderBrush;
        }

        // Create Theme-prefixed resources
        var colorProperties = typeof(ThemeColors).GetProperties();
        foreach (var prop in colorProperties)
        {
            var colorValue = prop.GetValue(colors) as string;
            if (!string.IsNullOrEmpty(colorValue))
            {
                try
                {
                    var brush = new SolidColorBrush(Color.Parse(colorValue));
                    resources[$"Theme{prop.Name}"] = brush;
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"Invalid color value for {prop.Name}: {colorValue} - {ex.Message}");
                }
            }
        }
    }

    private void ApplyFonts(IResourceDictionary resources, ThemeFonts fonts)
    {
        if (!string.IsNullOrEmpty(fonts.Primary) && fonts.Primary != "$Default")
        {
            try
            {
                resources["GlobalFontFamily"] = new FontFamily(fonts.Primary);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Invalid font family: {fonts.Primary} - {ex.Message}");
            }
        }

        if (fonts.Size.HasValue && fonts.Size.Value > 0)
        {
            resources["GlobalFontSize"] = (double)fonts.Size.Value;
        }

        if (!string.IsNullOrEmpty(fonts.Monospace))
        {
            try
            {
                resources["MonospaceFontFamily"] = new FontFamily(fonts.Monospace);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Invalid monospace font: {fonts.Monospace} - {ex.Message}");
            }
        }
    }

    private void ApplySpacing(IResourceDictionary resources, ThemeSpacing spacing)
    {
        if (spacing.ControlPadding.HasValue)
        {
            resources["ControlPadding"] = (double)spacing.ControlPadding.Value;
        }

        if (spacing.ControlMargin.HasValue)
        {
            resources["ControlMargin"] = (double)spacing.ControlMargin.Value;
        }

        if (spacing.PanelSpacing.HasValue)
        {
            resources["PanelSpacing"] = (double)spacing.PanelSpacing.Value;
        }

        if (spacing.MinControlHeight.HasValue)
        {
            resources["MinControlHeight"] = (double)spacing.MinControlHeight.Value;
        }
    }

    public ThemeManifest? GetTheme(string themeId)
    {
        return _themes.TryGetValue(themeId, out var theme) ? theme : null;
    }

    public void RefreshThemes()
    {
        DiscoverThemes();
    }
}
