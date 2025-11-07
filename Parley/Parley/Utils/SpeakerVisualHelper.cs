using System;
using System.Collections.Generic;

namespace DialogEditor.Utils
{
    /// <summary>
    /// Helper class for assigning consistent colors and shapes to dialog speakers
    /// Uses hash-based assignment for visual consistency across sessions
    /// </summary>
    public static class SpeakerVisualHelper
    {
        /// <summary>
        /// Available shapes for speaker icons (PC and Owner have fixed shapes)
        /// </summary>
        public enum SpeakerShape
        {
            Circle,    // Reserved for PC
            Square,    // Reserved for Owner
            Triangle,
            Diamond,
            Pentagon,
            Star
        }

        /// <summary>
        /// Color-blind friendly palette based on research and WCAG guidelines
        /// </summary>
        public static class ColorPalette
        {
            public const string Blue = "#4FC3F7";      // PC only
            public const string Orange = "#FF8A65";    // Owner default
            public const string Purple = "#BA68C8";    // Available for NPCs
            public const string Teal = "#26A69A";      // Available for NPCs
            public const string Amber = "#FFD54F";     // Available for NPCs
            public const string Pink = "#F48FB1";      // Available for NPCs

            private static readonly string[] NpcColors = { Orange, Purple, Teal, Amber, Pink };

            /// <summary>
            /// Get color for NPC speaker by hash
            /// </summary>
            public static string GetNpcColor(string speakerName)
            {
                if (string.IsNullOrEmpty(speakerName))
                    return Orange;

                int hash = Math.Abs(speakerName.GetHashCode());
                return NpcColors[hash % NpcColors.Length];
            }
        }

        /// <summary>
        /// Gets the shape for a speaker
        /// </summary>
        public static SpeakerShape GetSpeakerShape(string speaker, bool isPC)
        {
            // PC always gets circle
            if (isPC)
                return SpeakerShape.Circle;

            // Owner (empty speaker on Entry) always gets square
            if (string.IsNullOrEmpty(speaker))
                return SpeakerShape.Square;

            // Other NPCs get shapes based on hash
            var availableShapes = new[] { SpeakerShape.Triangle, SpeakerShape.Diamond, SpeakerShape.Pentagon, SpeakerShape.Star };
            int hash = Math.Abs(speaker.GetHashCode());
            return availableShapes[hash % availableShapes.Length];
        }

        /// <summary>
        /// Gets the color for a speaker
        /// </summary>
        public static string GetSpeakerColor(string speaker, bool isPC)
        {
            // PC always gets blue
            if (isPC)
                return ColorPalette.Blue;

            // Owner (empty speaker on Entry) always gets orange
            if (string.IsNullOrEmpty(speaker))
                return ColorPalette.Orange;

            // Other NPCs get colors based on hash
            return ColorPalette.GetNpcColor(speaker);
        }

        /// <summary>
        /// Gets both shape and color for a speaker (convenience method)
        /// </summary>
        public static (SpeakerShape shape, string color) GetSpeakerVisuals(string speaker, bool isPC)
        {
            return (GetSpeakerShape(speaker, isPC), GetSpeakerColor(speaker, isPC));
        }

        /// <summary>
        /// Gets the geometry path data for a shape (for use in Avalonia Path control)
        /// All shapes are normalized to a 20x20 viewport
        /// </summary>
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
