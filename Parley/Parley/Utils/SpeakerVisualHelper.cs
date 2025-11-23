using System;
using System.Collections.Generic;
using DialogEditor.Services;

namespace DialogEditor.Utils
{
    /// <summary>
    /// Helper class for assigning consistent colors and shapes to dialog speakers
    /// Uses hash-based assignment for visual consistency across sessions
    /// </summary>
    public static class SpeakerVisualHelper
    {
        /// <summary>
        /// Available shapes for speaker icons. PC and Owner have fixed shapes, while other NPCs use hash-based assignment.
        /// Provides visual redundancy alongside color for accessibility.
        /// </summary>
        public enum SpeakerShape
        {
            /// <summary>Reserved for PC (Player Character) speakers</summary>
            Circle,
            /// <summary>Reserved for Owner (default NPC) speakers</summary>
            Square,
            /// <summary>Available for named NPC speakers (hash-assigned)</summary>
            Triangle,
            /// <summary>Available for named NPC speakers (hash-assigned)</summary>
            Diamond,
            /// <summary>Available for named NPC speakers (hash-assigned)</summary>
            Pentagon,
            /// <summary>Available for named NPC speakers (hash-assigned)</summary>
            Star
        }

        /// <summary>
        /// Color-blind friendly palette based on research and WCAG guidelines.
        /// Colors are distinguishable by protanopia, deuteranopia, and tritanopia users.
        /// </summary>
        public static class ColorPalette
        {
            /// <summary>Light blue - Reserved for PC speakers</summary>
            public const string Blue = "#4FC3F7";
            /// <summary>Orange - Reserved for Owner (default NPC) speakers</summary>
            public const string Orange = "#FF8A65";
            /// <summary>Purple - Available for named NPC speakers</summary>
            public const string Purple = "#BA68C8";
            /// <summary>Teal - Available for named NPC speakers</summary>
            public const string Teal = "#26A69A";
            /// <summary>Amber - Available for named NPC speakers</summary>
            public const string Amber = "#FFD54F";
            /// <summary>Pink - Available for named NPC speakers</summary>
            public const string Pink = "#F48FB1";

            private static readonly string[] NpcColors = { Orange, Purple, Teal, Amber, Pink };

            /// <summary>
            /// Gets a consistent color for an NPC speaker based on hash of the speaker name.
            /// </summary>
            /// <param name="speakerName">The name of the NPC speaker (tag)</param>
            /// <returns>Hex color string (e.g., "#BA68C8")</returns>
            public static string GetNpcColor(string speakerName)
            {
                if (string.IsNullOrEmpty(speakerName))
                    return Orange;

                int hash = Math.Abs(speakerName.GetHashCode());
                return NpcColors[hash % NpcColors.Length];
            }
        }

        /// <summary>
        /// Gets the shape icon for a dialog speaker based on their identity.
        /// PC always gets Circle, Owner always gets Square, other NPCs check preferences first, then use hash-assigned shapes (unless disabled).
        /// </summary>
        /// <param name="speaker">The speaker tag/name (empty for Owner)</param>
        /// <param name="isPC">True if this is a PC (Player Character) reply node</param>
        /// <returns>A shape from the SpeakerShape enum</returns>
        public static SpeakerShape GetSpeakerShape(string speaker, bool isPC)
        {
            // PC always gets circle
            if (isPC)
                return SpeakerShape.Circle;

            // Owner (empty speaker on Entry) always gets square
            if (string.IsNullOrEmpty(speaker))
                return SpeakerShape.Square;

            // Check for user preference (Issue #16, #36)
            var (_, prefShape) = SettingsService.Instance.GetSpeakerPreference(speaker);
            if (prefShape.HasValue)
                return prefShape.Value;

            // If NPC tag coloring disabled, use default Owner shape (Issue #134)
            if (!SettingsService.Instance.EnableNpcTagColoring)
                return SpeakerShape.Square;

            // Other NPCs get shapes based on hash
            var availableShapes = new[] { SpeakerShape.Triangle, SpeakerShape.Diamond, SpeakerShape.Pentagon, SpeakerShape.Star };
            int hash = Math.Abs(speaker.GetHashCode());
            return availableShapes[hash % availableShapes.Length];
        }

        /// <summary>
        /// Gets the color for a dialog speaker based on their identity.
        /// PC gets blue (or theme override), Owner gets orange (or theme override),
        /// other NPCs check preferences first, then get hash-assigned colors from the palette (unless disabled).
        /// </summary>
        /// <param name="speaker">The speaker tag/name (empty for Owner)</param>
        /// <param name="isPC">True if this is a PC (Player Character) reply node</param>
        /// <returns>Hex color string (e.g., "#4FC3F7")</returns>
        public static string GetSpeakerColor(string speaker, bool isPC)
        {
            // Check for theme overrides from Application resources
            var app = Avalonia.Application.Current;

            // PC gets blue (or theme PC color override)
            if (isPC)
            {
                if (app?.Resources.TryGetResource("ThemePCColor", Avalonia.Styling.ThemeVariant.Default, out var pcColorObj) == true
                    && pcColorObj is string pcColor)
                {
                    return pcColor;
                }
                return ColorPalette.Blue;
            }

            // Owner (empty speaker on Entry) gets orange (or theme Owner color override)
            if (string.IsNullOrEmpty(speaker))
            {
                if (app?.Resources.TryGetResource("ThemeOwnerColor", Avalonia.Styling.ThemeVariant.Default, out var ownerColorObj) == true
                    && ownerColorObj is string ownerColor)
                {
                    return ownerColor;
                }
                return ColorPalette.Orange;
            }

            // Check for user preference (Issue #16, #36)
            var (prefColor, _) = SettingsService.Instance.GetSpeakerPreference(speaker);
            if (!string.IsNullOrEmpty(prefColor))
                return prefColor;

            // If NPC tag coloring disabled, use default Owner color from theme (Issue #134)
            if (!SettingsService.Instance.EnableNpcTagColoring)
            {
                if (app?.Resources.TryGetResource("ThemeOwnerColor", Avalonia.Styling.ThemeVariant.Default, out var ownerColorObj2) == true
                    && ownerColorObj2 is string ownerColor2)
                {
                    return ownerColor2;
                }
                return ColorPalette.Orange;
            }

            // Other NPCs get colors based on hash
            return ColorPalette.GetNpcColor(speaker);
        }

        /// <summary>
        /// Gets both shape and color for a speaker in a single call (convenience method).
        /// Equivalent to calling GetSpeakerShape and GetSpeakerColor separately.
        /// </summary>
        /// <param name="speaker">The speaker tag/name (empty for Owner)</param>
        /// <param name="isPC">True if this is a PC (Player Character) reply node</param>
        /// <returns>Tuple of (shape, color) for the speaker</returns>
        public static (SpeakerShape shape, string color) GetSpeakerVisuals(string speaker, bool isPC)
        {
            return (GetSpeakerShape(speaker, isPC), GetSpeakerColor(speaker, isPC));
        }

        /// <summary>
        /// Gets the SVG path geometry data for rendering a shape icon in Avalonia.
        /// All shapes are normalized to a 20x20 viewport and centered.
        /// </summary>
        /// <param name="shape">The shape to get geometry for</param>
        /// <returns>SVG path data string suitable for Avalonia Path.Data binding</returns>
        public static string GetShapeGeometry(SpeakerShape shape)
        {
            return shape switch
            {
                SpeakerShape.Circle => "M 10,2 A 8,8 0 1,1 10,18 A 8,8 0 1,1 10,2 Z",
                SpeakerShape.Square => "M 3,3 L 17,3 L 17,17 L 3,17 Z",
                SpeakerShape.Triangle => "M 10,2 L 18,17 L 2,17 Z",
                SpeakerShape.Diamond => "M 10,2 L 18,10 L 10,18 L 2,10 Z",
                SpeakerShape.Pentagon => "M 10,2 L 18,8 L 15,17 L 5,17 L 2,8 Z",
                SpeakerShape.Star => "M 10,2 L 12,8 L 18,8 L 13,12 L 15,18 L 10,14 L 5,18 L 7,12 L 2,8 L 8,8 Z",
                _ => "M 10,2 A 8,8 0 1,1 10,18 A 8,8 0 1,1 10,2 Z" // Default to circle
            };
        }
    }
}
