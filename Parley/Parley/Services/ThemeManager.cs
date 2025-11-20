using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using DialogEditor.Models;

namespace DialogEditor.Services
{
    /// <summary>
    /// Manages theme loading, application, and switching
    /// Supports data-only theme plugins (JSON format)
    /// </summary>
    public class ThemeManager
    {
        private static ThemeManager? _instance;
        public static ThemeManager Instance => _instance ??= new ThemeManager();

        private readonly Dictionary<string, ThemeManifest> _themes = new();
        private ThemeManifest? _currentTheme;

        /// <summary>
        /// Theme directories (official and community)
        /// </summary>
        private readonly List<string> _themeDirectories = new();

        /// <summary>
        /// Available themes discovered from plugin directories
        /// </summary>
        public IReadOnlyList<ThemeManifest> AvailableThemes => _themes.Values.ToList();

        /// <summary>
        /// Currently active theme
        /// </summary>
        public ThemeManifest? CurrentTheme => _currentTheme;

        private ThemeManager()
        {
            InitializeThemeDirectories();
        }

        /// <summary>
        /// Initialize theme search directories
        /// </summary>
        private void InitializeThemeDirectories()
        {
            // Official themes (shipped with app)
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var officialThemes = Path.Combine(appDir, "Themes");
            if (!Directory.Exists(officialThemes))
            {
                Directory.CreateDirectory(officialThemes);
            }
            _themeDirectories.Add(officialThemes);

            // Community themes (user data folder)
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var userThemes = Path.Combine(appData, "Parley", "Themes");
            if (!Directory.Exists(userThemes))
            {
                Directory.CreateDirectory(userThemes);
            }
            _themeDirectories.Add(userThemes);

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Theme directories initialized: {_themeDirectories.Count} locations");
        }

        /// <summary>
        /// Discover all available themes from theme directories
        /// </summary>
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

        /// <summary>
        /// Load theme manifest from JSON file
        /// </summary>
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

        /// <summary>
        /// Apply a theme by ID
        /// </summary>
        public bool ApplyTheme(string themeId)
        {
            if (!_themes.TryGetValue(themeId, out var theme))
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Theme not found: {themeId}");
                return false;
            }

            return ApplyTheme(theme);
        }

        /// <summary>
        /// Apply a theme manifest
        /// </summary>
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

                // Apply Avalonia base theme variant (Light/Dark) first
                var targetVariant = theme.BaseTheme.ToLower() switch
                {
                    "dark" => ThemeVariant.Dark,
                    "light" => ThemeVariant.Light,
                    _ => ThemeVariant.Light
                };

                // Only change if different to avoid flickering
                if (app.RequestedThemeVariant != targetVariant)
                {
                    app.RequestedThemeVariant = targetVariant;
                }

                // CRITICAL: Apply custom colors AFTER theme variant loads
                // Must override BOTH Color and Brush resources
                if (theme.Colors != null)
                {
                    ApplyColors(app.Resources, theme.Colors);
                }

                // Apply custom fonts to resource dictionary
                if (theme.Fonts != null)
                {
                    ApplyFonts(app.Resources, theme.Fonts);
                }

                // Apply custom spacing to resource dictionary
                if (theme.Spacing != null)
                {
                    ApplySpacing(app.Resources, theme.Spacing);
                }

                _currentTheme = theme;

                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Applied theme: {theme.Plugin.Name} ({theme.Plugin.Id})");

                return true;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"Failed to apply theme {theme.Plugin.Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Apply color values to resource dictionary
        /// Maps theme colors to Avalonia system resources
        /// </summary>
        private void ApplyColors(IResourceDictionary resources, ThemeColors colors)
        {
            // Comprehensive mapping to Avalonia system colors AND brushes
            // Background colors - main window background AND control backgrounds
            if (!string.IsNullOrEmpty(colors.Background))
            {
                var bgColor = Color.Parse(colors.Background);
                var bgBrush = new SolidColorBrush(bgColor);

                // Set both color and brush resources
                resources["SystemChromeMediumColor"] = bgColor;
                resources["SystemChromeMediumLowColor"] = bgColor;
                resources["SystemChromeHighColor"] = bgColor;
                resources["SystemRegionColor"] = bgColor;

                // CRITICAL: Window and panel backgrounds
                resources["SolidBackgroundFillColorBase"] = bgBrush;
                resources["SolidBackgroundFillColorBaseBrush"] = bgBrush;
                resources["LayerFillColorDefault"] = bgBrush;
                resources["LayerFillColorDefaultBrush"] = bgBrush;
                resources["CardBackgroundFillColorDefault"] = bgBrush;
                resources["CardBackgroundFillColorDefaultBrush"] = bgBrush;
                resources["SubtleFillColorSecondary"] = bgBrush;
                resources["SubtleFillColorSecondaryBrush"] = bgBrush;

                // CRITICAL: These are what TextBox, CheckBox, etc. actually use
                resources["TextControlBackground"] = bgBrush;
                resources["TextControlBackgroundPointerOver"] = bgBrush;
                resources["TextControlBackgroundFocused"] = bgBrush;
                resources["TextControlBackgroundDisabled"] = bgBrush;
                resources["ControlFillColorDefaultBrush"] = bgBrush;
                resources["ControlFillColorSecondaryBrush"] = bgBrush;
                resources["ControlFillColorTertiaryBrush"] = bgBrush;
                resources["SystemControlBackgroundAltHighBrush"] = bgBrush;
                resources["SystemControlBackgroundAltMediumBrush"] = bgBrush;
                resources["SystemControlBackgroundAltMediumHighBrush"] = bgBrush;
            }

            // Sidebar/Alt colors - panels, toolbars
            if (!string.IsNullOrEmpty(colors.Sidebar))
            {
                var sidebarColor = Color.Parse(colors.Sidebar);
                var sidebarBrush = new SolidColorBrush(sidebarColor);

                resources["SystemAltMediumColor"] = sidebarColor;
                resources["SystemAltHighColor"] = sidebarColor;
                resources["SystemChromeLowColor"] = sidebarColor;
                resources["SystemChromeWhiteColor"] = sidebarColor;

                // Panel and container backgrounds
                resources["CardBackgroundFillColorSecondary"] = sidebarBrush;
                resources["CardBackgroundFillColorSecondaryBrush"] = sidebarBrush;
                resources["LayerFillColorAlt"] = sidebarBrush;
                resources["LayerFillColorAltBrush"] = sidebarBrush;
                resources["SystemControlBackgroundAltHighBrush"] = sidebarBrush;
                resources["SystemControlBackgroundChromeMediumBrush"] = sidebarBrush;
            }

            // Text colors - controls need TextControlForeground
            if (!string.IsNullOrEmpty(colors.Text))
            {
                var textColor = Color.Parse(colors.Text);
                var textBrush = new SolidColorBrush(textColor);

                resources["SystemBaseHighColor"] = textColor;
                resources["SystemBaseMediumHighColor"] = textColor;
                resources["SystemBaseMediumColor"] = textColor;

                // CRITICAL: TextBox and other control foregrounds
                resources["TextControlForeground"] = textBrush;
                resources["TextControlForegroundPointerOver"] = textBrush;
                resources["TextControlForegroundFocused"] = textBrush;
                resources["TextControlForegroundDisabled"] = textBrush;
                resources["TextControlPlaceholderForeground"] = textBrush;
                resources["SystemControlForegroundBaseHighBrush"] = textBrush;
                resources["SystemControlForegroundBaseMediumBrush"] = textBrush;
                resources["SystemControlForegroundBaseMediumHighBrush"] = textBrush;
            }

            // Accent color - buttons, highlights
            if (!string.IsNullOrEmpty(colors.Accent))
            {
                var accentColor = Color.Parse(colors.Accent);
                var accentBrush = new SolidColorBrush(accentColor);

                resources["SystemAccentColor"] = accentColor;
                resources["SystemAccentColorLight1"] = accentColor;
                resources["SystemAccentColorLight2"] = accentColor;
                resources["SystemAccentColorLight3"] = accentColor;
                resources["SystemAccentColorDark1"] = accentColor;
                resources["SystemAccentColorDark2"] = accentColor;
                resources["SystemAccentColorDark3"] = accentColor;

                resources["SystemControlHighlightAccentBrush"] = accentBrush;
            }

            // Selection color
            if (!string.IsNullOrEmpty(colors.Selection))
            {
                var selColor = Color.Parse(colors.Selection);
                var selBrush = new SolidColorBrush(selColor);

                resources["SystemListLowColor"] = selColor;
                resources["SystemListMediumColor"] = selColor;

                resources["SystemControlHighlightListLowBrush"] = selBrush;
                resources["SystemControlHighlightListMediumBrush"] = selBrush;
            }

            // Border colors
            if (!string.IsNullOrEmpty(colors.Border))
            {
                var borderColor = Color.Parse(colors.Border);
                var borderBrush = new SolidColorBrush(borderColor);

                resources["SystemBaseMediumLowColor"] = borderColor;
                resources["SystemBaseLowColor"] = borderColor;
                resources["SystemChromeDisabledLowColor"] = borderColor;
                resources["SystemChromeDisabledHighColor"] = borderColor;

                resources["SystemControlForegroundBaseMediumLowBrush"] = borderBrush;
                resources["SystemControlForegroundBaseLowBrush"] = borderBrush;
            }

            // Also create Theme-prefixed resources for direct use
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

        /// <summary>
        /// Apply font values to resource dictionary
        /// </summary>
        private void ApplyFonts(IResourceDictionary resources, ThemeFonts fonts)
        {
            if (!string.IsNullOrEmpty(fonts.Primary))
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

        /// <summary>
        /// Apply spacing values to resource dictionary
        /// </summary>
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

        /// <summary>
        /// Get theme by ID
        /// </summary>
        public ThemeManifest? GetTheme(string themeId)
        {
            return _themes.TryGetValue(themeId, out var theme) ? theme : null;
        }

        /// <summary>
        /// Reload themes from disk
        /// </summary>
        public void RefreshThemes()
        {
            DiscoverThemes();
        }
    }
}
