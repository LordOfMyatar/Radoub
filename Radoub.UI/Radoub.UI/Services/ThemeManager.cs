using System;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Radoub.UI.Models;

namespace Radoub.UI.Services;

/// <summary>
/// Manages theme loading, application, and switching for Radoub tools.
/// Supports JSON-based theme plugins with tool-specific user directories.
/// </summary>
public class ThemeManager
{
    private static ThemeManager? _instance;
    private static readonly object _lock = new();

    private readonly Dictionary<string, ThemeManifest> _themes = new();
    private ThemeManifest? _currentTheme;
    private readonly string _toolName;
    private readonly bool _useSharedTheme;

    /// <summary>
    /// Event raised when a theme is successfully applied.
    /// UI components can subscribe to refresh their visuals.
    /// </summary>
    public event EventHandler? ThemeApplied;

    /// <summary>
    /// Theme directories (official and user)
    /// </summary>
    private readonly List<string> _themeDirectories = new();

    /// <summary>
    /// Available themes discovered from theme directories
    /// </summary>
    public IReadOnlyList<ThemeManifest> AvailableThemes => _themes.Values.ToList();

    /// <summary>
    /// Currently active theme
    /// </summary>
    public ThemeManifest? CurrentTheme => _currentTheme;

    /// <summary>
    /// Gets the singleton instance. Must call Initialize() first.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if Initialize() has not been called.</exception>
    public static ThemeManager Instance
    {
        get
        {
            if (_instance == null)
            {
                throw new InvalidOperationException(
                    "ThemeManager not initialized. Call ThemeManager.Initialize(toolName) first.");
            }
            return _instance;
        }
    }

    /// <summary>
    /// Initializes the ThemeManager singleton for the specified tool.
    /// Call once at application startup before accessing Instance.
    /// </summary>
    /// <param name="toolName">Tool name for user theme directory (e.g., "Parley", "Manifest", "Quartermaster")</param>
    /// <param name="useSharedTheme">If true, prefer shared Radoub-level theme over tool-specific. Default: true</param>
    /// <returns>The initialized ThemeManager instance</returns>
    public static ThemeManager Initialize(string toolName, bool useSharedTheme = true)
    {
        lock (_lock)
        {
            if (_instance != null)
            {
                // Already initialized - return existing instance
                // (supports safe re-initialization with same tool name)
                return _instance;
            }
            _instance = new ThemeManager(toolName, useSharedTheme);
            return _instance;
        }
    }

    /// <summary>
    /// Creates a new ThemeManager for the specified tool.
    /// Use Initialize() for singleton access or construct directly for testing.
    /// </summary>
    /// <param name="toolName">Tool name for user theme directory (e.g., "Parley", "Manifest", "Quartermaster")</param>
    /// <param name="useSharedTheme">If true, prefer shared Radoub-level theme over tool-specific. Default: true</param>
    public ThemeManager(string toolName, bool useSharedTheme = true)
    {
        _toolName = toolName;
        _useSharedTheme = useSharedTheme;
        InitializeThemeDirectories();
    }

    /// <summary>
    /// Initialize theme search directories.
    /// Order of precedence (last wins for same theme ID):
    /// 1. Official themes (shipped with app)
    /// 2. Radoub-level shared themes (~/Radoub/Themes/)
    /// 3. Tool-specific user themes (~/Radoub/{ToolName}/Themes/)
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

        // Radoub-level shared themes (~/Radoub/Themes/)
        // Available to all tools
        var sharedThemes = RadoubSettings.Instance.GetSharedThemesPath();
        if (!_themeDirectories.Contains(sharedThemes))
        {
            _themeDirectories.Add(sharedThemes);
        }

        // User themes (user home folder - consistent with SettingsService)
        // Location: ~/Radoub/{ToolName}/Themes
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var userThemes = Path.Combine(userProfile, "Radoub", _toolName, "Themes");
        if (!Directory.Exists(userThemes))
        {
            Directory.CreateDirectory(userThemes);
        }
        _themeDirectories.Add(userThemes);

        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"[{_toolName}] Theme directories initialized: {_themeDirectories.Count} locations (shared themes: {_useSharedTheme})");
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
                            $"[{_toolName}] Discovered theme: {manifest.Plugin.Name} ({manifest.Plugin.Id})");
                    }
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"[{_toolName}] Failed to load theme: {ex.Message}");
                }
            }
        }

        UnifiedLogger.LogApplication(LogLevel.INFO, $"[{_toolName}] Discovered {_themes.Count} themes");
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
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"[{_toolName}] Theme not found: {themeId}");
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
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"[{_toolName}] Application instance not available");
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

            // Apply custom colors AFTER theme variant loads
            if (theme.Colors != null)
            {
                ApplyColors(app.Resources, theme.Colors, theme.BaseTheme);
            }

            // Apply font sizes (with defaults if not specified in theme)
            ApplyFonts(app.Resources, theme.Fonts ?? new ThemeFonts());

            // Apply custom spacing to resource dictionary
            if (theme.Spacing != null)
            {
                ApplySpacing(app.Resources, theme.Spacing);
            }

            _currentTheme = theme;

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"[{_toolName}] Applied theme: {theme.Plugin.Name} ({theme.Plugin.Id})");

            // Notify subscribers that theme has changed
            ThemeApplied?.Invoke(this, EventArgs.Empty);

            return true;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"[{_toolName}] Failed to apply theme {theme.Plugin.Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Apply color values to resource dictionary.
    /// Maps theme colors to Avalonia system resources.
    /// </summary>
    /// <param name="resources">Resource dictionary to update</param>
    /// <param name="colors">Theme color definitions</param>
    /// <param name="baseTheme">Base theme variant ("Light" or "Dark")</param>
    private void ApplyColors(IResourceDictionary resources, ThemeColors colors, string baseTheme)
    {
        // =================================================================
        // TIER 1: System color primitives
        // These are the "knobs" that FluentTheme reads to derive ALL
        // per-control-state resources (Button, CheckBox, ComboBox, Menu,
        // RadioButton, TabItem, etc.). Set these and Fluent handles the rest.
        // Do NOT set per-control-state resources directly — that breaks
        // Fluent's internal consistency and causes cross-theme visual bugs.
        // =================================================================

        // Background surface colors
        if (!string.IsNullOrEmpty(colors.Background))
        {
            var bgColor = Color.Parse(colors.Background);
            resources["SystemChromeMediumColor"] = bgColor;
            resources["SystemChromeMediumLowColor"] = bgColor;
            resources["SystemChromeHighColor"] = bgColor;
            resources["SystemRegionColor"] = bgColor;
        }

        // Sidebar/Alt surface colors
        if (!string.IsNullOrEmpty(colors.Sidebar))
        {
            var sidebarColor = Color.Parse(colors.Sidebar);
            resources["SystemAltMediumColor"] = sidebarColor;
            resources["SystemAltHighColor"] = sidebarColor;
            resources["SystemChromeLowColor"] = sidebarColor;
        }

        // Text color primitives — Fluent derives all control foregrounds from these
        if (!string.IsNullOrEmpty(colors.Text))
        {
            var textColor = Color.Parse(colors.Text);
            var mutedTextColor = !string.IsNullOrEmpty(colors.TextMuted)
                ? Color.Parse(colors.TextMuted)
                : Color.FromArgb((byte)(textColor.A * 0.7), textColor.R, textColor.G, textColor.B);

            resources["SystemBaseHighColor"] = textColor;
            resources["SystemBaseMediumHighColor"] = textColor;
            resources["SystemBaseMediumColor"] = textColor;
            resources["SystemBaseMediumLowColor"] = mutedTextColor;
        }

        // Accent color — the ONE knob for accent-colored UI elements
        if (!string.IsNullOrEmpty(colors.Accent))
        {
            var accentColor = Color.Parse(colors.Accent);
            resources["SystemAccentColor"] = accentColor;
            resources["SystemAccentColorLight1"] = accentColor;
            resources["SystemAccentColorLight2"] = accentColor;
            resources["SystemAccentColorLight3"] = accentColor;
            resources["SystemAccentColorDark1"] = accentColor;
            resources["SystemAccentColorDark2"] = accentColor;
            resources["SystemAccentColorDark3"] = accentColor;
        }

        // Selection color primitives
        if (!string.IsNullOrEmpty(colors.Selection))
        {
            var selColor = Color.Parse(colors.Selection);
            resources["SystemListLowColor"] = selColor;
            resources["SystemListMediumColor"] = selColor;
        }

        // Border/disabled color primitives
        if (!string.IsNullOrEmpty(colors.Border))
        {
            var borderColor = Color.Parse(colors.Border);
            resources["SystemBaseLowColor"] = borderColor;
            resources["SystemChromeDisabledLowColor"] = borderColor;
            resources["SystemChromeDisabledHighColor"] = borderColor;
        }

        // =================================================================
        // TIER 2: Custom Theme* resources
        // Our AXAML binds to these directly via {DynamicResource Theme*}.
        // These are Radoub's own namespace — Fluent doesn't know about them.
        // =================================================================

        // Background brushes for explicit AXAML use
        if (!string.IsNullOrEmpty(colors.Background))
        {
            var bgBrush = new SolidColorBrush(Color.Parse(colors.Background));
            resources["ThemeBackground"] = bgBrush;
        }
        if (!string.IsNullOrEmpty(colors.Sidebar))
        {
            var sidebarBrush = new SolidColorBrush(Color.Parse(colors.Sidebar));
            resources["ThemeBackgroundAlt"] = sidebarBrush;
        }

        // Text brushes for explicit AXAML use
        if (!string.IsNullOrEmpty(colors.Text))
        {
            var mutedTextColor = !string.IsNullOrEmpty(colors.TextMuted)
                ? Color.Parse(colors.TextMuted)
                : Color.FromArgb((byte)(Color.Parse(colors.Text).A * 0.7),
                    Color.Parse(colors.Text).R, Color.Parse(colors.Text).G, Color.Parse(colors.Text).B);
            resources["ThemeTextMuted"] = new SolidColorBrush(mutedTextColor);
        }

        // Title bar colors (#1089)
        if (!string.IsNullOrEmpty(colors.TitleBar))
            resources["ThemeTitleBar"] = new SolidColorBrush(Color.Parse(colors.TitleBar));
        if (!string.IsNullOrEmpty(colors.TitleBarForeground))
            resources["ThemeTitleBarForeground"] = new SolidColorBrush(Color.Parse(colors.TitleBarForeground));

        // Accent brush for explicit AXAML use (Trebuchet header bar)
        if (!string.IsNullOrEmpty(colors.Accent))
            resources["ThemeAccentBrush"] = new SolidColorBrush(Color.Parse(colors.Accent));

        // Border brush for explicit AXAML use
        if (!string.IsNullOrEmpty(colors.Border))
            resources["ThemeBorderBrush"] = new SolidColorBrush(Color.Parse(colors.Border));

        // Semantic colors — used by BrushManager for colorblind accessibility
        if (!string.IsNullOrEmpty(colors.Success))
            resources["ThemeSuccess"] = new SolidColorBrush(Color.Parse(colors.Success));
        if (!string.IsNullOrEmpty(colors.Warning))
            resources["ThemeWarning"] = new SolidColorBrush(Color.Parse(colors.Warning));
        if (!string.IsNullOrEmpty(colors.Error))
            resources["ThemeError"] = new SolidColorBrush(Color.Parse(colors.Error));
        if (!string.IsNullOrEmpty(colors.Info))
        {
            var infoBrush = new SolidColorBrush(Color.Parse(colors.Info));
            resources["ThemeInfo"] = infoBrush;
            resources["ThemeInfoBrush"] = infoBrush; // Alias for Trebuchet
        }
        if (!string.IsNullOrEmpty(colors.Disabled))
            resources["ThemeDisabled"] = new SolidColorBrush(Color.Parse(colors.Disabled));

        // Button colors — only AccentButton (Fluent handles regular Button from accent primitives)
        if (!string.IsNullOrEmpty(colors.ButtonPrimary))
        {
            var btnPrimaryBrush = new SolidColorBrush(Color.Parse(colors.ButtonPrimary));
            resources["AccentButtonBackground"] = btnPrimaryBrush;
            resources["AccentButtonBackgroundPointerOver"] = btnPrimaryBrush;
            resources["AccentButtonBackgroundPressed"] = btnPrimaryBrush;
            resources["ThemeButtonPrimary"] = btnPrimaryBrush;
        }
        if (!string.IsNullOrEmpty(colors.ButtonText))
        {
            var btnTextBrush = new SolidColorBrush(Color.Parse(colors.ButtonText));
            resources["AccentButtonForeground"] = btnTextBrush;
            resources["AccentButtonForegroundPointerOver"] = btnTextBrush;
            resources["AccentButtonForegroundPressed"] = btnTextBrush;
            resources["ThemeAccentForeground"] = btnTextBrush;
        }
        if (!string.IsNullOrEmpty(colors.ButtonSecondary))
            resources["ThemeButtonSecondary"] = new SolidColorBrush(Color.Parse(colors.ButtonSecondary));

        // Parley-specific: tree node colors, edit mode borders
        if (!string.IsNullOrEmpty(colors.TreeReply))
            resources["ThemePCColor"] = colors.TreeReply;
        if (!string.IsNullOrEmpty(colors.TreeEntry))
            resources["ThemeOwnerColor"] = colors.TreeEntry;
        if (!string.IsNullOrEmpty(colors.EditModeBorder))
            resources["ThemeEditModeBorder"] = new SolidColorBrush(Color.Parse(colors.EditModeBorder));
        if (!string.IsNullOrEmpty(colors.EditModeUnsaved))
            resources["ThemeEditModeUnsaved"] = new SolidColorBrush(Color.Parse(colors.EditModeUnsaved));
        if (!string.IsNullOrEmpty(colors.EditModeSaved))
            resources["ThemeEditModeSaved"] = new SolidColorBrush(Color.Parse(colors.EditModeSaved));
        if (!string.IsNullOrEmpty(colors.AutoTrimBorder))
            resources["ThemeAutoTrimBorder"] = new SolidColorBrush(Color.Parse(colors.AutoTrimBorder));

        // Bulk-generate Theme{PropertyName} brushes from all ThemeColors properties
        // This lets AXAML use {DynamicResource ThemeBackground}, {DynamicResource ThemeAccent}, etc.
        var colorProperties = typeof(ThemeColors).GetProperties();
        foreach (var prop in colorProperties)
        {
            var colorValue = prop.GetValue(colors) as string;
            if (!string.IsNullOrEmpty(colorValue))
            {
                try
                {
                    resources[$"Theme{prop.Name}"] = new SolidColorBrush(Color.Parse(colorValue));
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"[{_toolName}] Invalid color value for {prop.Name}: {colorValue} - {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Apply font values to resource dictionary
    /// </summary>
    private void ApplyFonts(IResourceDictionary resources, ThemeFonts fonts)
    {
        // Apply font family - empty or "$Default" means system default
        if (!string.IsNullOrEmpty(fonts.Primary) && fonts.Primary != "$Default")
        {
            try
            {
                resources["GlobalFontFamily"] = new FontFamily(fonts.Primary);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"[{_toolName}] Invalid font family: {fonts.Primary} - {ex.Message}");
                resources["GlobalFontFamily"] = FontFamily.Default;
            }
        }
        else
        {
            // Explicitly set system default font
            resources["GlobalFontFamily"] = FontFamily.Default;
        }

        // Use theme's font size or default to 14
        var baseSize = (fonts.Size.HasValue && fonts.Size.Value > 0) ? (double)fonts.Size.Value : 14.0;
        resources["GlobalFontSize"] = baseSize;

        // Derived font sizes for UI hierarchy (all scale with base size)
        resources["FontSizeXSmall"] = Math.Max(8, baseSize - 4);   // 10 @ base 14
        resources["FontSizeSmall"] = Math.Max(9, baseSize - 3);    // 11 @ base 14
        resources["FontSizeNormal"] = baseSize;                     // 14 @ base 14
        resources["FontSizeMedium"] = baseSize + 2;                 // 16 @ base 14
        resources["FontSizeLarge"] = baseSize + 4;                  // 18 @ base 14
        resources["FontSizeXLarge"] = baseSize + 6;                 // 20 @ base 14
        resources["FontSizeTitle"] = baseSize + 10;                 // 24 @ base 14

        if (!string.IsNullOrEmpty(fonts.Monospace))
        {
            try
            {
                resources["MonospaceFontFamily"] = new FontFamily(fonts.Monospace);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"[{_toolName}] Invalid monospace font: {fonts.Monospace} - {ex.Message}");
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

    /// <summary>
    /// Get the effective theme ID to apply.
    /// If useSharedTheme is enabled and a shared theme is configured in RadoubSettings,
    /// returns the shared theme ID. Otherwise returns the provided tool-specific theme ID.
    /// </summary>
    /// <param name="toolThemeId">The tool's configured theme ID</param>
    /// <returns>The effective theme ID to apply</returns>
    public string GetEffectiveThemeId(string toolThemeId)
    {
        if (_useSharedTheme && RadoubSettings.Instance.HasSharedTheme)
        {
            var sharedThemeId = RadoubSettings.Instance.SharedThemeId;
            if (_themes.ContainsKey(sharedThemeId))
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"[{_toolName}] Using shared theme: {sharedThemeId}");
                return sharedThemeId;
            }
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"[{_toolName}] Shared theme '{sharedThemeId}' not found, falling back to tool theme");
        }

        return toolThemeId;
    }

    /// <summary>
    /// Apply the effective theme (shared or tool-specific).
    /// Checks shared settings first if useSharedTheme is enabled.
    /// </summary>
    /// <param name="toolThemeId">The tool's configured theme ID as fallback</param>
    /// <returns>True if a theme was applied successfully</returns>
    public bool ApplyEffectiveTheme(string toolThemeId)
    {
        var effectiveThemeId = GetEffectiveThemeId(toolThemeId);
        return ApplyTheme(effectiveThemeId);
    }

    /// <summary>
    /// Check if shared theme is being used.
    /// </summary>
    public bool IsUsingSharedTheme => _useSharedTheme && RadoubSettings.Instance.HasSharedTheme;
}
