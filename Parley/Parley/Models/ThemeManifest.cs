using System.Text.Json.Serialization;

namespace DialogEditor.Models
{
    /// <summary>
    /// Theme plugin manifest - data-only theme configuration
    /// </summary>
    public class ThemeManifest
    {
        [JsonPropertyName("manifest_version")]
        public string ManifestVersion { get; set; } = "1.0";

        [JsonPropertyName("plugin")]
        public ThemePluginInfo Plugin { get; set; } = new();

        [JsonPropertyName("base_theme")]
        public string BaseTheme { get; set; } = "Light"; // "Light" or "Dark"

        [JsonPropertyName("accessibility")]
        public ThemeAccessibility? Accessibility { get; set; }

        [JsonPropertyName("colors")]
        public ThemeColors? Colors { get; set; }

        [JsonPropertyName("fonts")]
        public ThemeFonts? Fonts { get; set; }

        [JsonPropertyName("spacing")]
        public ThemeSpacing? Spacing { get; set; }

        /// <summary>
        /// Path to the theme file (set at runtime, not in JSON)
        /// </summary>
        [JsonIgnore]
        public string? SourcePath { get; set; }
    }

    /// <summary>
    /// Theme plugin metadata
    /// </summary>
    public class ThemePluginInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0.0";

        [JsonPropertyName("author")]
        public string Author { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "theme";

        [JsonPropertyName("tags")]
        public string[] Tags { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Theme accessibility metadata
    /// </summary>
    public class ThemeAccessibility
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "standard"; // "standard", "colorblind", "nightmare"

        [JsonPropertyName("condition")]
        public string? Condition { get; set; } // "deuteranopia", "protanopia", "tritanopia"

        [JsonPropertyName("contrast_level")]
        public string ContrastLevel { get; set; } = "AA"; // "AA", "AAA", "LOL"

        [JsonPropertyName("warning")]
        public string? Warning { get; set; }
    }

    /// <summary>
    /// Theme color palette
    /// </summary>
    public class ThemeColors
    {
        // Core UI colors
        [JsonPropertyName("background")]
        public string? Background { get; set; }

        [JsonPropertyName("sidebar")]
        public string? Sidebar { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("selection")]
        public string? Selection { get; set; }

        [JsonPropertyName("border")]
        public string? Border { get; set; }

        [JsonPropertyName("accent")]
        public string? Accent { get; set; }

        // Status colors
        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("warning")]
        public string? Warning { get; set; }

        [JsonPropertyName("success")]
        public string? Success { get; set; }

        [JsonPropertyName("info")]
        public string? Info { get; set; }

        // Tree view colors
        [JsonPropertyName("tree_entry")]
        public string? TreeEntry { get; set; }

        [JsonPropertyName("tree_reply")]
        public string? TreeReply { get; set; }

        [JsonPropertyName("tree_link")]
        public string? TreeLink { get; set; }

        // Button colors
        [JsonPropertyName("button_primary")]
        public string? ButtonPrimary { get; set; }

        [JsonPropertyName("button_secondary")]
        public string? ButtonSecondary { get; set; }

        [JsonPropertyName("button_hover")]
        public string? ButtonHover { get; set; }
    }

    /// <summary>
    /// Theme font configuration
    /// </summary>
    public class ThemeFonts
    {
        [JsonPropertyName("primary")]
        public string? Primary { get; set; } // Main UI font

        [JsonPropertyName("monospace")]
        public string? Monospace { get; set; } // Code/script font

        [JsonPropertyName("size")]
        public int? Size { get; set; } // Default font size

        [JsonPropertyName("weight")]
        public string? Weight { get; set; } // "Normal", "Bold", etc.
    }

    /// <summary>
    /// Theme spacing/layout configuration
    /// </summary>
    public class ThemeSpacing
    {
        [JsonPropertyName("control_padding")]
        public int? ControlPadding { get; set; }

        [JsonPropertyName("control_margin")]
        public int? ControlMargin { get; set; }

        [JsonPropertyName("panel_spacing")]
        public int? PanelSpacing { get; set; }

        [JsonPropertyName("min_control_height")]
        public int? MinControlHeight { get; set; }

        [JsonPropertyName("tree_indent")]
        public int? TreeIndent { get; set; }
    }
}
