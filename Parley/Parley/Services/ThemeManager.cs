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

                // Apply Avalonia base theme variant (Light/Dark)
                app.RequestedThemeVariant = theme.BaseTheme.ToLower() switch
                {
                    "dark" => ThemeVariant.Dark,
                    "light" => ThemeVariant.Light,
                    _ => ThemeVariant.Light
                };

                // Apply custom colors to resource dictionary
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
        /// </summary>
        private void ApplyColors(IResourceDictionary resources, ThemeColors colors)
        {
            // Use reflection to iterate all color properties
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
