using System;
using System.Text.Json.Serialization;

namespace Quartermaster.Models;

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
    public string BaseTheme { get; set; } = "Light";

    [JsonPropertyName("colors")]
    public ThemeColors? Colors { get; set; }

    [JsonPropertyName("fonts")]
    public ThemeFonts? Fonts { get; set; }

    [JsonPropertyName("spacing")]
    public ThemeSpacing? Spacing { get; set; }

    [JsonIgnore]
    public string? SourcePath { get; set; }
}

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

public class ThemeColors
{
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

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("warning")]
    public string? Warning { get; set; }

    [JsonPropertyName("success")]
    public string? Success { get; set; }

    [JsonPropertyName("info")]
    public string? Info { get; set; }
}

public class ThemeFonts
{
    [JsonPropertyName("primary")]
    public string? Primary { get; set; }

    [JsonPropertyName("monospace")]
    public string? Monospace { get; set; }

    [JsonPropertyName("size")]
    public int? Size { get; set; }

    [JsonPropertyName("weight")]
    public string? Weight { get; set; }
}

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
}
