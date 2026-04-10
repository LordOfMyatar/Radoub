namespace Radoub.Formats.Tokens;

/// <summary>
/// Runtime model for user-defined custom (non-color) tokens.
/// Loaded from ~/Radoub/custom-tokens.json
/// </summary>
public class CustomTokenConfig
{
    public List<CustomTokenDefinition> Tokens { get; set; } = new();
}

/// <summary>
/// A single custom token definition — standalone or paired.
/// </summary>
public class CustomTokenDefinition
{
    /// <summary>
    /// Display name shown in the token chooser UI.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Token type: "standalone" or "paired".
    /// </summary>
    public string Type { get; set; } = "standalone";

    /// <summary>
    /// The CUSTOM tag for standalone tokens (e.g., "&lt;CUSTOM500&gt;").
    /// </summary>
    public string? Tag { get; set; }

    /// <summary>
    /// The opening CUSTOM tag for paired tokens (e.g., "&lt;CUSTOM2001&gt;").
    /// </summary>
    public string? OpenTag { get; set; }

    /// <summary>
    /// The closing CUSTOM tag for paired tokens (e.g., "&lt;CUSTOM2000&gt;").
    /// </summary>
    public string? CloseTag { get; set; }

    /// <summary>
    /// Whether this is a standalone token (single tag insert).
    /// </summary>
    public bool IsStandalone => Type?.Equals("standalone", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Whether this is a paired token (open/close wrapping).
    /// </summary>
    public bool IsPaired => Type?.Equals("paired", StringComparison.OrdinalIgnoreCase) == true;
}
