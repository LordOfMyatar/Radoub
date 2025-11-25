using System;
using DialogEditor.Services;

namespace DialogEditor.Utils
{
    /// <summary>
    /// Helper class for rainbow bracket depth indicators.
    /// Provides soft, eye-friendly colors that cycle through nesting levels.
    /// Theme-aware with separate palettes for light and dark modes.
    /// </summary>
    public static class RainbowBracketHelper
    {
        /// <summary>
        /// Soft rainbow palette for light themes.
        /// Muted colors that provide visual distinction without being harsh.
        /// </summary>
        private static readonly string[] LightThemePalette = new[]
        {
            "#7986CB", // Soft indigo
            "#4DB6AC", // Soft teal
            "#FFB74D", // Soft amber
            "#F06292", // Soft pink
            "#81C784", // Soft green
            "#BA68C8", // Soft purple
        };

        /// <summary>
        /// Soft rainbow palette for dark themes.
        /// Slightly brighter for visibility on dark backgrounds.
        /// </summary>
        private static readonly string[] DarkThemePalette = new[]
        {
            "#9FA8DA", // Light indigo
            "#80CBC4", // Light teal
            "#FFD54F", // Light amber
            "#F48FB1", // Light pink
            "#A5D6A7", // Light green
            "#CE93D8", // Light purple
        };

        /// <summary>
        /// Gets the rainbow color for a given depth level.
        /// Colors cycle through the palette as depth increases.
        /// </summary>
        /// <param name="depth">The nesting depth (0-based)</param>
        /// <param name="isDarkTheme">True if using dark theme</param>
        /// <returns>Hex color string for the depth indicator</returns>
        public static string GetDepthColor(int depth, bool isDarkTheme)
        {
            // Skip depth 0 (ROOT) - it doesn't need a rainbow indicator
            if (depth <= 0) return "Transparent";

            var palette = isDarkTheme ? DarkThemePalette : LightThemePalette;
            // Subtract 1 so depth 1 gets first color
            int index = (depth - 1) % palette.Length;
            return palette[index];
        }

        /// <summary>
        /// Gets the rainbow color matching an NPC's speaker color when multi-NPC mode is enabled.
        /// Falls back to depth-based rainbow when speaker has no custom color or multi-NPC is disabled.
        /// </summary>
        /// <param name="depth">The nesting depth (0-based)</param>
        /// <param name="speaker">The speaker tag/name</param>
        /// <param name="isPC">True if this is a PC speaker</param>
        /// <param name="isDarkTheme">True if using dark theme</param>
        /// <returns>Hex color string for the depth indicator</returns>
        public static string GetDepthColorWithSpeaker(int depth, string speaker, bool isPC, bool isDarkTheme)
        {
            // Skip depth 0 (ROOT)
            if (depth <= 0) return "Transparent";

            // If multi-NPC coloring is enabled, use the speaker's color
            if (SettingsService.Instance.EnableNpcTagColoring)
            {
                // Get the speaker's assigned color (same logic as node coloring)
                var speakerColor = SpeakerVisualHelper.GetSpeakerColor(speaker, isPC);

                // Make the color slightly more muted for the bracket indicator
                // by applying some transparency or using it directly
                return speakerColor;
            }

            // Fall back to rainbow palette when multi-NPC coloring is disabled
            return GetDepthColor(depth, isDarkTheme);
        }

        /// <summary>
        /// Determines if the current theme is dark.
        /// Checks the ThemeManager for active theme type.
        /// </summary>
        public static bool IsDarkTheme()
        {
            try
            {
                var themeId = SettingsService.Instance.CurrentThemeId;
                // Check theme ID for "dark" indicator or check theme manager
                return themeId.Contains("dark", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false; // Default to light theme on error
            }
        }
    }
}
