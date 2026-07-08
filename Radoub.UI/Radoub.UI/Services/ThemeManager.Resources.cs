using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.UI.Models;

namespace Radoub.UI.Services;

/// <summary>
/// Theme resource application: colors, fonts, and spacing.
/// </summary>
public partial class ThemeManager
{
    /// <summary>
    /// Apply theme resources (colors, fonts, spacing) after variant change.
    /// </summary>
    private void ApplyThemeResources(Application app, ThemeManifest theme)
    {
        try
        {
            if (theme.Colors != null)
            {
                ApplyColors(app.Resources, theme.Colors, theme.BaseTheme);
            }

            ApplyFonts(app.Resources, theme.Fonts ?? new ThemeFonts());

            if (theme.Spacing != null)
            {
                ApplySpacing(app.Resources, theme.Spacing);
            }

            _currentTheme = theme;

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"[{_toolName}] Applied theme: {theme.Plugin.Name} ({theme.Plugin.Id})");

            ThemeApplied?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"[{_toolName}] Failed to apply theme resources: {ex.Message}");
        }
    }

    /// <summary>
    /// Apply semantic colors (Success, Warning, Error, Info, Disabled) synchronously.
    /// Called before the deferred Post so BrushManager can resolve these immediately
    /// during window construction. These are custom resources that don't depend on
    /// Fluent's variant re-derivation.
    /// </summary>
    private void ApplySemanticColors(IResourceDictionary resources, ThemeColors colors)
    {
        if (!string.IsNullOrEmpty(colors.Success))
            resources["ThemeSuccess"] = new SolidColorBrush(Color.Parse(colors.Success)); // theme-ok
        if (!string.IsNullOrEmpty(colors.Warning))
            resources["ThemeWarning"] = new SolidColorBrush(Color.Parse(colors.Warning)); // theme-ok
        if (!string.IsNullOrEmpty(colors.Error))
            resources["ThemeError"] = new SolidColorBrush(Color.Parse(colors.Error)); // theme-ok
        if (!string.IsNullOrEmpty(colors.Info))
        {
            var infoBrush = new SolidColorBrush(Color.Parse(colors.Info)); // theme-ok
            resources["ThemeInfo"] = infoBrush;
            resources["ThemeInfoBrush"] = infoBrush; // Alias for Trebuchet
        }
        if (!string.IsNullOrEmpty(colors.Disabled))
            resources["ThemeDisabled"] = new SolidColorBrush(Color.Parse(colors.Disabled)); // theme-ok
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
            var bgColor = Color.Parse(colors.Background); // theme-ok
            resources["SystemChromeMediumColor"] = bgColor;
            resources["SystemChromeMediumLowColor"] = bgColor;
            resources["SystemChromeHighColor"] = bgColor;
            resources["SystemRegionColor"] = bgColor;
        }

        // Sidebar/Alt surface colors
        if (!string.IsNullOrEmpty(colors.Sidebar))
        {
            var sidebarColor = Color.Parse(colors.Sidebar); // theme-ok
            resources["SystemAltMediumColor"] = sidebarColor;
            resources["SystemAltHighColor"] = sidebarColor;
            resources["SystemChromeLowColor"] = sidebarColor;
        }

        // Text color primitives — Fluent derives all control foregrounds from these
        if (!string.IsNullOrEmpty(colors.Text))
        {
            var textColor = Color.Parse(colors.Text); // theme-ok
            var mutedTextColor = !string.IsNullOrEmpty(colors.TextMuted)
                ? Color.Parse(colors.TextMuted) // theme-ok
                : Color.FromArgb((byte)(textColor.A * 0.7), textColor.R, textColor.G, textColor.B); // theme-ok

            resources["SystemBaseHighColor"] = textColor;
            resources["SystemBaseMediumHighColor"] = textColor;
            resources["SystemBaseMediumColor"] = textColor;
            resources["SystemBaseMediumLowColor"] = mutedTextColor;
        }

        // Accent color — the ONE knob for accent-colored UI elements
        if (!string.IsNullOrEmpty(colors.Accent))
        {
            var accentColor = Color.Parse(colors.Accent); // theme-ok
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
            var selColor = Color.Parse(colors.Selection); // theme-ok
            resources["SystemListLowColor"] = selColor;
            resources["SystemListMediumColor"] = selColor;
        }

        // Border/disabled color primitives
        if (!string.IsNullOrEmpty(colors.Border))
        {
            var borderColor = Color.Parse(colors.Border); // theme-ok
            resources["SystemBaseLowColor"] = borderColor;
            resources["SystemChromeDisabledLowColor"] = borderColor;
            resources["SystemChromeDisabledHighColor"] = borderColor;
        }

        // =================================================================
        // TIER 1B: SystemControl*Brush overrides
        // Fluent derives these internally from its Light/Dark color tables,
        // NOT from our System*Color overrides above. When switching between
        // themes with the same base variant (e.g., Light → colorblind Light),
        // Fluent doesn't re-derive, so AXAML referencing these brushes gets
        // stale values. We must set them explicitly.
        // Only the brushes actually referenced in our AXAML are listed here.
        // =================================================================

        // Background brushes (used by panels, flowchart, toolbars)
        if (!string.IsNullOrEmpty(colors.Background))
        {
            var bgBrush = new SolidColorBrush(Color.Parse(colors.Background)); // theme-ok
            resources["SystemControlBackgroundChromeMediumBrush"] = bgBrush;
            resources["SystemControlBackgroundChromeMediumLowBrush"] = bgBrush;
        }
        if (!string.IsNullOrEmpty(colors.Sidebar))
        {
            var sidebarBrush = new SolidColorBrush(Color.Parse(colors.Sidebar)); // theme-ok
            resources["SystemControlBackgroundAltHighBrush"] = sidebarBrush;
        }

        // Foreground brushes (used for text, borders, separators)
        // BaseMediumBrush (172 uses): muted text, toolbar separators — use TextMuted color
        // BaseMediumLowBrush (285 uses): borders, dividers, de-emphasized text — use Border color
        if (!string.IsNullOrEmpty(colors.Text))
        {
            var textColor = Color.Parse(colors.Text); // theme-ok
            var mutedTextColor = !string.IsNullOrEmpty(colors.TextMuted)
                ? Color.Parse(colors.TextMuted) // theme-ok
                : Color.FromArgb((byte)(textColor.A * 0.7), textColor.R, textColor.G, textColor.B); // theme-ok

            resources["SystemControlForegroundBaseHighBrush"] = new SolidColorBrush(textColor);
            resources["SystemControlForegroundBaseMediumHighBrush"] = new SolidColorBrush(textColor);
            resources["SystemControlForegroundBaseMediumBrush"] = new SolidColorBrush(mutedTextColor);

            // BaseMediumLow: primarily borders/separators (285 uses). Use Border color if available,
            // otherwise fall back to muted text. This prevents borders from being invisible or
            // text-on-borders from being unreadable across themes.
            var borderColor = !string.IsNullOrEmpty(colors.Border)
                ? Color.Parse(colors.Border) // theme-ok
                : mutedTextColor;
            resources["SystemControlForegroundBaseMediumLowBrush"] = new SolidColorBrush(borderColor);
        }

        // Selection/highlight brush
        if (!string.IsNullOrEmpty(colors.Selection))
        {
            resources["SystemControlHighlightListLowBrush"] = new SolidColorBrush(Color.Parse(colors.Selection)); // theme-ok
        }

        // Border/low-emphasis brush
        if (!string.IsNullOrEmpty(colors.Border))
        {
            resources["SystemControlBackgroundBaseLowBrush"] = new SolidColorBrush(Color.Parse(colors.Border)); // theme-ok
            resources["SystemControlForegroundBaseLowBrush"] = new SolidColorBrush(Color.Parse(colors.Border)); // theme-ok
        }

        // =================================================================
        // TIER 2: Custom Theme* resources
        // Our AXAML binds to these directly via {DynamicResource Theme*}.
        // These are Radoub's own namespace — Fluent doesn't know about them.
        // =================================================================

        // Background brushes for explicit AXAML use
        if (!string.IsNullOrEmpty(colors.Background))
        {
            var bgBrush = new SolidColorBrush(Color.Parse(colors.Background)); // theme-ok
            resources["ThemeBackground"] = bgBrush;
            resources["ThemeBackgroundBrush"] = bgBrush; // Alias (used by QM/Fence SettingsWindow)
        }
        if (!string.IsNullOrEmpty(colors.Sidebar))
        {
            var sidebarBrush = new SolidColorBrush(Color.Parse(colors.Sidebar)); // theme-ok
            resources["ThemeBackgroundAlt"] = sidebarBrush;
            resources["ThemeSidebar"] = sidebarBrush; // Alias used by browser panels (#1347)
        }

        // Text brushes for explicit AXAML use
        if (!string.IsNullOrEmpty(colors.Text))
        {
            var mutedTextColor = !string.IsNullOrEmpty(colors.TextMuted)
                ? Color.Parse(colors.TextMuted) // theme-ok
                : Color.FromArgb((byte)(Color.Parse(colors.Text).A * 0.7), // theme-ok
                    Color.Parse(colors.Text).R, Color.Parse(colors.Text).G, Color.Parse(colors.Text).B); // theme-ok
            resources["ThemeTextMuted"] = new SolidColorBrush(mutedTextColor);
        }

        // Title bar colors (#1089)
        if (!string.IsNullOrEmpty(colors.TitleBar))
            resources["ThemeTitleBar"] = new SolidColorBrush(Color.Parse(colors.TitleBar)); // theme-ok
        if (!string.IsNullOrEmpty(colors.TitleBarForeground))
            resources["ThemeTitleBarForeground"] = new SolidColorBrush(Color.Parse(colors.TitleBarForeground)); // theme-ok

        // Accent brush for explicit AXAML use (Trebuchet header bar)
        if (!string.IsNullOrEmpty(colors.Accent))
            resources["ThemeAccentBrush"] = new SolidColorBrush(Color.Parse(colors.Accent)); // theme-ok

        // Border brush for explicit AXAML use
        if (!string.IsNullOrEmpty(colors.Border))
            resources["ThemeBorderBrush"] = new SolidColorBrush(Color.Parse(colors.Border)); // theme-ok

        // Semantic colors — used by BrushManager for colorblind accessibility
        if (!string.IsNullOrEmpty(colors.Success))
            resources["ThemeSuccess"] = new SolidColorBrush(Color.Parse(colors.Success)); // theme-ok
        if (!string.IsNullOrEmpty(colors.Warning))
            resources["ThemeWarning"] = new SolidColorBrush(Color.Parse(colors.Warning)); // theme-ok
        if (!string.IsNullOrEmpty(colors.Error))
            resources["ThemeError"] = new SolidColorBrush(Color.Parse(colors.Error)); // theme-ok
        if (!string.IsNullOrEmpty(colors.Info))
        {
            var infoBrush = new SolidColorBrush(Color.Parse(colors.Info)); // theme-ok
            resources["ThemeInfo"] = infoBrush;
            resources["ThemeInfoBrush"] = infoBrush; // Alias for Trebuchet
        }
        if (!string.IsNullOrEmpty(colors.Disabled))
            resources["ThemeDisabled"] = new SolidColorBrush(Color.Parse(colors.Disabled)); // theme-ok

        // Button colors — only AccentButton (Fluent handles regular Button from accent primitives)
        if (!string.IsNullOrEmpty(colors.ButtonPrimary))
        {
            var btnPrimaryBrush = new SolidColorBrush(Color.Parse(colors.ButtonPrimary)); // theme-ok
            resources["AccentButtonBackground"] = btnPrimaryBrush;
            resources["AccentButtonBackgroundPointerOver"] = btnPrimaryBrush;
            resources["AccentButtonBackgroundPressed"] = btnPrimaryBrush;
            resources["ThemeButtonPrimary"] = btnPrimaryBrush;
        }
        if (!string.IsNullOrEmpty(colors.ButtonText))
        {
            var btnTextBrush = new SolidColorBrush(Color.Parse(colors.ButtonText)); // theme-ok
            resources["AccentButtonForeground"] = btnTextBrush;
            resources["AccentButtonForegroundPointerOver"] = btnTextBrush;
            resources["AccentButtonForegroundPressed"] = btnTextBrush;
            resources["ThemeAccentForeground"] = btnTextBrush;
        }
        if (!string.IsNullOrEmpty(colors.ButtonSecondary))
            resources["ThemeButtonSecondary"] = new SolidColorBrush(Color.Parse(colors.ButtonSecondary)); // theme-ok

        // Parley-specific: tree node colors, edit mode borders
        if (!string.IsNullOrEmpty(colors.TreeReply))
            resources["ThemePCColor"] = colors.TreeReply;
        if (!string.IsNullOrEmpty(colors.TreeEntry))
            resources["ThemeOwnerColor"] = colors.TreeEntry;
        if (!string.IsNullOrEmpty(colors.EditModeBorder))
            resources["ThemeEditModeBorder"] = new SolidColorBrush(Color.Parse(colors.EditModeBorder)); // theme-ok
        if (!string.IsNullOrEmpty(colors.EditModeUnsaved))
            resources["ThemeEditModeUnsaved"] = new SolidColorBrush(Color.Parse(colors.EditModeUnsaved)); // theme-ok
        if (!string.IsNullOrEmpty(colors.EditModeSaved))
            resources["ThemeEditModeSaved"] = new SolidColorBrush(Color.Parse(colors.EditModeSaved)); // theme-ok
        if (!string.IsNullOrEmpty(colors.AutoTrimBorder))
            resources["ThemeAutoTrimBorder"] = new SolidColorBrush(Color.Parse(colors.AutoTrimBorder)); // theme-ok

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
                    resources[$"Theme{prop.Name}"] = new SolidColorBrush(Color.Parse(colorValue)); // theme-ok
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
    /// <summary>
    /// Resolves the effective global font size (#2152). The Trebuchet slider
    /// (<paramref name="sharedFontSize"/>) is the single authority; a theme's own
    /// <c>fonts.size</c> is only a fallback used when no shared value is available
    /// (sharedFontSize &lt;= 0). Pure logic for unit testing.
    /// </summary>
    public static double ResolveEffectiveFontSize(ThemeFonts? fonts, double sharedFontSize)
    {
        if (sharedFontSize > 0)
            return sharedFontSize;

        // No shared value — fall back to the theme's size, then the 14 default.
        return (fonts?.Size is int s && s > 0) ? s : 14.0;
    }

    /// <summary>
    /// Resolves the effective global font family (#2404). The Trebuchet shared
    /// setting (<paramref name="sharedFontFamily"/>) is the single authority; a
    /// theme's own <c>fonts.Primary</c> is only a fallback used when no shared
    /// family is configured. Returns null when neither yields a concrete family
    /// (caller applies the system default). "$Default" in a theme is a sentinel
    /// for "system default", not a literal family name. Pure logic for unit testing.
    /// </summary>
    public static string? ResolveEffectiveFontFamily(ThemeFonts? fonts, string? sharedFontFamily)
    {
        if (!string.IsNullOrWhiteSpace(sharedFontFamily))
            return sharedFontFamily;

        var themeFamily = fonts?.Primary;
        if (!string.IsNullOrWhiteSpace(themeFamily) && themeFamily != "$Default")
            return themeFamily;

        return null;
    }

    private void ApplyFonts(IResourceDictionary resources, ThemeFonts fonts)
    {
        // Font family + size: RadoubSettings (Trebuchet shared settings) is the single
        // authority (#2404 family, #2152 size). The theme's own fonts.Primary/fonts.Size
        // are only fallbacks used when no shared value is configured — otherwise every
        // theme apply would clobber the user's global font choices back to theme defaults.
        var sharedSettings = RadoubSettings.Instance;
        ApplyResolvedFonts(resources, ResolveEffectiveFontFamily(fonts, sharedSettings.SharedFontFamily),
            ResolveEffectiveFontSize(fonts, sharedSettings.SharedFontSize));

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
    /// Re-applies global font family + size from the Trebuchet shared settings
    /// (<see cref="RadoubSettings"/>) to the given resource dictionary (#2404).
    /// This is the single shared entry point every tool calls on startup and on
    /// window activation, replacing the six duplicated per-tool ApplyFontSettings()
    /// copies. Only family + the seven derived sizes are shared; tool-specific
    /// derived resources (e.g. Quartermaster portrait dimensions) are applied by
    /// the tool after this call.
    /// </summary>
    public static void ApplySharedFontSettings(IResourceDictionary? resources)
    {
        if (resources == null)
            return;

        var sharedSettings = RadoubSettings.Instance;
        // Shared settings are authoritative, but when SharedFontFamily/Size are unset fall back to
        // the ACTIVE theme's own fonts (matching ApplyFonts on the theme path) rather than jumping
        // straight to system default — otherwise this call, which runs right after ApplySharedTheme
        // on window activation, would clobber a theme's concrete primary font back to default every
        // time (a regression for community themes; shipped themes use "$Default" → no effect).
        var themeFonts = Instance.CurrentTheme?.Fonts;
        ApplyResolvedFonts(resources,
            ResolveEffectiveFontFamily(themeFonts, sharedSettings.SharedFontFamily),
            ResolveEffectiveFontSize(themeFonts, sharedSettings.SharedFontSize));
    }

    /// <summary>
    /// Writes GlobalFontFamily + GlobalFontSize + the seven derived UI sizes to a
    /// resource dictionary from an already-resolved family/size pair. Null family
    /// means system default. Shared by theme apply (<see cref="ApplyFonts"/>) and
    /// the per-tool shared entry point (<see cref="ApplySharedFontSettings"/>) so
    /// both paths produce identical results.
    /// </summary>
    private static void ApplyResolvedFonts(IResourceDictionary resources, string? family, double baseSize)
    {
        if (family == null)
        {
            resources["GlobalFontFamily"] = FontFamily.Default;
        }
        else
        {
            try
            {
                resources["GlobalFontFamily"] = new FontFamily(family);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Invalid font family: {family} - {ex.Message}. Using system default.");
                resources["GlobalFontFamily"] = FontFamily.Default;
            }
        }

        resources["GlobalFontSize"] = baseSize;

        // Derived font sizes for UI hierarchy (all scale with base size)
        resources["FontSizeXSmall"] = Math.Max(10, baseSize - 2);  // 12 @ base 14
        resources["FontSizeSmall"] = Math.Max(11, baseSize - 1);   // 13 @ base 14
        resources["FontSizeNormal"] = baseSize;                     // 14 @ base 14
        resources["FontSizeMedium"] = baseSize + 2;                 // 16 @ base 14
        resources["FontSizeLarge"] = baseSize + 4;                  // 18 @ base 14
        resources["FontSizeXLarge"] = baseSize + 6;                 // 20 @ base 14
        resources["FontSizeTitle"] = baseSize + 10;                 // 24 @ base 14
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
}
