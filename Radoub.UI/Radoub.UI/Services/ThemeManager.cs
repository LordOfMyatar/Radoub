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
                ApplyColors(app.Resources, theme.Colors);
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
    private void ApplyColors(IResourceDictionary resources, ThemeColors colors)
    {
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

            // Window and panel backgrounds
            resources["SolidBackgroundFillColorBase"] = bgBrush;
            resources["SolidBackgroundFillColorBaseBrush"] = bgBrush;
            resources["LayerFillColorDefault"] = bgBrush;
            resources["LayerFillColorDefaultBrush"] = bgBrush;
            resources["CardBackgroundFillColorDefault"] = bgBrush;
            resources["CardBackgroundFillColorDefaultBrush"] = bgBrush;
            resources["SubtleFillColorSecondary"] = bgBrush;
            resources["SubtleFillColorSecondaryBrush"] = bgBrush;

            // TextBox, CheckBox, and other control backgrounds
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

            // DataGrid and ListBox backgrounds
            resources["DataGridBackground"] = bgBrush;
            resources["DataGridRowBackground"] = bgBrush;
            resources["DataGridCellBackground"] = bgBrush;
            resources["ListBoxBackground"] = bgBrush;
            resources["ListViewBackground"] = bgBrush;
            resources["TreeViewBackground"] = bgBrush;

            // ScrollViewer background
            resources["ScrollViewerBackground"] = bgBrush;

            // App-level theme background for windows and dialogs
            resources["ThemeBackground"] = bgBrush;
        }

        // Sidebar/Alt colors - panels, toolbars
        if (!string.IsNullOrEmpty(colors.Sidebar))
        {
            var sidebarColor = Color.Parse(colors.Sidebar);
            var sidebarBrush = new SolidColorBrush(sidebarColor);

            resources["SystemAltMediumColor"] = sidebarColor;
            resources["SystemAltHighColor"] = sidebarColor;
            resources["SystemChromeLowColor"] = sidebarColor;
            // Note: Do NOT set SystemChromeWhiteColor to sidebar - FluentTheme expects
            // this to be white/light for contrast purposes in certain controls

            // Panel and container backgrounds
            resources["CardBackgroundFillColorSecondary"] = sidebarBrush;
            resources["CardBackgroundFillColorSecondaryBrush"] = sidebarBrush;
            resources["LayerFillColorAlt"] = sidebarBrush;
            resources["LayerFillColorAltBrush"] = sidebarBrush;
            resources["SystemControlBackgroundAltHighBrush"] = sidebarBrush;
            resources["SystemControlBackgroundChromeMediumBrush"] = sidebarBrush;

            // Table/list backgrounds - use sidebar color for consistency
            // This ensures contrast calculations in brush-contrast-test.html match actual rendering
            resources["SystemControlBackgroundBaseLowBrush"] = sidebarBrush;
        }

        // Text colors - controls need TextControlForeground
        if (!string.IsNullOrEmpty(colors.Text))
        {
            var textColor = Color.Parse(colors.Text);
            var textBrush = new SolidColorBrush(textColor);

            resources["SystemBaseHighColor"] = textColor;
            resources["SystemBaseMediumHighColor"] = textColor;
            resources["SystemBaseMediumColor"] = textColor;

            // TextBox and other control foregrounds
            resources["TextControlForeground"] = textBrush;
            resources["TextControlForegroundPointerOver"] = textBrush;
            resources["TextControlForegroundFocused"] = textBrush;
            resources["TextControlForegroundDisabled"] = textBrush;
            resources["TextControlPlaceholderForeground"] = textBrush;
            resources["SystemControlForegroundBaseHighBrush"] = textBrush;
            resources["SystemControlForegroundBaseMediumBrush"] = textBrush;
            resources["SystemControlForegroundBaseMediumHighBrush"] = textBrush;

            // Menu text colors - top-level menu items in menu bar
            resources["MenuFlyoutItemForeground"] = textBrush;
            resources["MenuFlyoutItemForegroundPointerOver"] = textBrush;
            resources["MenuFlyoutItemForegroundPressed"] = textBrush;
            resources["MenuFlyoutItemForegroundDisabled"] = textBrush;
            resources["MenuBarItemForeground"] = textBrush;
            resources["TopLevelItemForeground"] = textBrush;

            // Menu keyboard shortcut (InputGesture/accelerator) text colors
            resources["MenuFlyoutItemKeyboardAcceleratorTextForeground"] = textBrush;
            resources["MenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOver"] = textBrush;
            resources["MenuFlyoutItemKeyboardAcceleratorTextForegroundPressed"] = textBrush;
            resources["MenuFlyoutItemKeyboardAcceleratorTextForegroundDisabled"] = textBrush;

            // Submenu chevron/arrow colors
            resources["MenuFlyoutSubItemChevron"] = textBrush;
            resources["MenuFlyoutSubItemChevronPointerOver"] = textBrush;
            resources["MenuFlyoutSubItemChevronPressed"] = textBrush;
            resources["MenuFlyoutSubItemChevronDisabled"] = textBrush;
            resources["MenuFlyoutSubItemChevronSubMenuOpened"] = textBrush;

            // CheckBox text (label) colors - all states
            resources["CheckBoxForegroundUnchecked"] = textBrush;
            resources["CheckBoxForegroundUncheckedPointerOver"] = textBrush;
            resources["CheckBoxForegroundUncheckedPressed"] = textBrush;
            resources["CheckBoxForegroundUncheckedDisabled"] = textBrush;
            resources["CheckBoxForegroundChecked"] = textBrush;
            resources["CheckBoxForegroundCheckedPointerOver"] = textBrush;
            resources["CheckBoxForegroundCheckedPressed"] = textBrush;
            resources["CheckBoxForegroundCheckedDisabled"] = textBrush;
            resources["CheckBoxForegroundIndeterminate"] = textBrush;
            resources["CheckBoxForegroundIndeterminatePointerOver"] = textBrush;
            resources["CheckBoxForegroundIndeterminatePressed"] = textBrush;
            resources["CheckBoxForegroundIndeterminateDisabled"] = textBrush;

            // RadioButton text colors (similar pattern to CheckBox)
            resources["RadioButtonForeground"] = textBrush;
            resources["RadioButtonForegroundPointerOver"] = textBrush;
            resources["RadioButtonForegroundPressed"] = textBrush;
            resources["RadioButtonForegroundDisabled"] = textBrush;
        }

        // Menu flyout (dropdown) background - needs to match theme
        if (!string.IsNullOrEmpty(colors.Sidebar))
        {
            var sidebarBrush = new SolidColorBrush(Color.Parse(colors.Sidebar));

            // Menu dropdown background
            resources["MenuFlyoutPresenterBackground"] = sidebarBrush;
            resources["MenuFlyoutPresenterBorderBrush"] = sidebarBrush;
            resources["ContextMenuBackground"] = sidebarBrush;
            resources["MenuBarBackground"] = sidebarBrush;

            // Generic flyout/popup backgrounds (ComboBox dropdowns, AutoComplete, etc.)
            resources["FlyoutPresenterBackground"] = sidebarBrush;
            resources["FlyoutBorderThemeBrush"] = sidebarBrush;

            // ComboBox dropdown background
            resources["ComboBoxDropDownBackground"] = sidebarBrush;
            resources["ComboBoxDropDownBackgroundPointerOver"] = sidebarBrush;
            resources["ComboBoxDropDownBackgroundPressed"] = sidebarBrush;

            // ToolTip background
            resources["ToolTipBackground"] = sidebarBrush;

            // AutoComplete/suggestion popup background
            resources["AutoCompleteBoxBackground"] = sidebarBrush;
            resources["AutoCompleteBoxBackgroundPointerOver"] = sidebarBrush;
            resources["AutoCompleteBoxBackgroundFocused"] = sidebarBrush;
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

            // Expander border styling
            resources["ExpanderHeaderBorderBrush"] = borderBrush;
            resources["ExpanderContentBorderBrush"] = borderBrush;
        }

        // Expander styling - use sidebar for header background, background for content
        if (!string.IsNullOrEmpty(colors.Sidebar))
        {
            var sidebarBrush = new SolidColorBrush(Color.Parse(colors.Sidebar));
            resources["ExpanderHeaderBackground"] = sidebarBrush;
            resources["ExpanderHeaderBackgroundPointerOver"] = sidebarBrush;
            resources["ExpanderHeaderBackgroundPressed"] = sidebarBrush;
        }
        if (!string.IsNullOrEmpty(colors.Background))
        {
            var bgBrush = new SolidColorBrush(Color.Parse(colors.Background));
            resources["ExpanderContentBackground"] = bgBrush;
        }
        if (!string.IsNullOrEmpty(colors.Text))
        {
            var textBrush = new SolidColorBrush(Color.Parse(colors.Text));
            resources["ExpanderHeaderForeground"] = textBrush;
            resources["ExpanderHeaderForegroundPointerOver"] = textBrush;
            resources["ExpanderHeaderForegroundPressed"] = textBrush;
            resources["ExpanderChevronForeground"] = textBrush;
        }

        // Create Theme-prefixed resources for direct use in XAML
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
                        $"[{_toolName}] Invalid color value for {prop.Name}: {colorValue} - {ex.Message}");
                }
            }
        }

        // Map tree colors to PC/Owner overrides for SpeakerVisualHelper (Parley)
        if (!string.IsNullOrEmpty(colors.TreeReply))
        {
            resources["ThemePCColor"] = colors.TreeReply;
        }
        if (!string.IsNullOrEmpty(colors.TreeEntry))
        {
            resources["ThemeOwnerColor"] = colors.TreeEntry;
        }

        // Edit mode border colors (Parley)
        if (!string.IsNullOrEmpty(colors.EditModeBorder))
        {
            var editBorderBrush = new SolidColorBrush(Color.Parse(colors.EditModeBorder));
            resources["ThemeEditModeBorder"] = editBorderBrush;
        }
        if (!string.IsNullOrEmpty(colors.EditModeUnsaved))
        {
            var unsavedBrush = new SolidColorBrush(Color.Parse(colors.EditModeUnsaved));
            resources["ThemeEditModeUnsaved"] = unsavedBrush;
        }
        if (!string.IsNullOrEmpty(colors.EditModeSaved))
        {
            var savedBrush = new SolidColorBrush(Color.Parse(colors.EditModeSaved));
            resources["ThemeEditModeSaved"] = savedBrush;
        }
        if (!string.IsNullOrEmpty(colors.AutoTrimBorder))
        {
            var autoTrimBrush = new SolidColorBrush(Color.Parse(colors.AutoTrimBorder));
            resources["ThemeAutoTrimBorder"] = autoTrimBrush;
        }

        // Button colors - apply theme-defined button colors to Fluent theme resources
        // This ensures proper contrast in dark themes
        if (!string.IsNullOrEmpty(colors.ButtonPrimary))
        {
            var btnPrimaryColor = Color.Parse(colors.ButtonPrimary);
            var btnPrimaryBrush = new SolidColorBrush(btnPrimaryColor);

            // Primary button background (accent buttons)
            resources["AccentButtonBackground"] = btnPrimaryBrush;
            resources["AccentButtonBackgroundPointerOver"] = btnPrimaryBrush;
            resources["AccentButtonBackgroundPressed"] = btnPrimaryBrush;
        }

        if (!string.IsNullOrEmpty(colors.ButtonSecondary))
        {
            var btnSecondaryColor = Color.Parse(colors.ButtonSecondary);
            var btnSecondaryBrush = new SolidColorBrush(btnSecondaryColor);

            // Standard button backgrounds - use sidebar-based colors for better visibility
            resources["ButtonBackground"] = btnSecondaryBrush;
            resources["ButtonBackgroundDisabled"] = btnSecondaryBrush;
        }

        if (!string.IsNullOrEmpty(colors.ButtonHover))
        {
            var btnHoverColor = Color.Parse(colors.ButtonHover);
            var btnHoverBrush = new SolidColorBrush(btnHoverColor);

            resources["ButtonBackgroundPointerOver"] = btnHoverBrush;
            resources["ButtonBackgroundPressed"] = btnHoverBrush;
        }

        // TabItem text color - ensure readability against tab background
        // Use theme text color for tab foregrounds
        if (!string.IsNullOrEmpty(colors.Text))
        {
            var textBrush = new SolidColorBrush(Color.Parse(colors.Text));

            // TabItem text (header) colors - Avalonia FluentTheme resource keys
            // Unselected tab states
            resources["TabItemHeaderForegroundUnselected"] = textBrush;
            resources["TabItemHeaderForegroundUnselectedPointerOver"] = textBrush;
            resources["TabItemHeaderForegroundUnselectedPressed"] = textBrush;
            // Selected tab states
            resources["TabItemHeaderForegroundSelected"] = textBrush;
            resources["TabItemHeaderForegroundSelectedPointerOver"] = textBrush;
            resources["TabItemHeaderForegroundSelectedPressed"] = textBrush;
            // Disabled state
            resources["TabItemHeaderForegroundDisabled"] = textBrush;
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
