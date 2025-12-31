using System.Collections.Generic;
using Radoub.Formats.Erf;

namespace Radoub.UI.Services;

/// <summary>
/// Represents a script entry in the browser with source information.
/// </summary>
public class ScriptEntry
{
    public string Name { get; set; } = "";
    public bool IsBuiltIn { get; set; }
    public string Source { get; set; } = ""; // "Module", "Override", "BIF: filename", "HAK: filename"

    /// <summary>
    /// If from HAK, the path to the HAK file.
    /// </summary>
    public string? HakPath { get; set; }

    /// <summary>
    /// If from HAK, the ERF resource entry for extraction.
    /// </summary>
    public ErfResourceEntry? ErfEntry { get; set; }

    /// <summary>
    /// True if this script comes from a HAK file (requires extraction for preview).
    /// </summary>
    public bool IsFromHak => HakPath != null && ErfEntry != null;

    /// <summary>
    /// Full file path for filesystem scripts.
    /// </summary>
    public string? FilePath { get; set; }

    public string DisplayName => IsBuiltIn ? $"🎮 {Name}" : IsFromHak ? $"📦 {Name}" : Name;

    public override string ToString() => DisplayName;
}

/// <summary>
/// Interface for providing context to the script browser.
/// Implementations provide tool-specific paths and services.
/// </summary>
public interface IScriptBrowserContext
{
    /// <summary>
    /// The current file's directory (e.g., dialog file or creature file directory).
    /// Used as the primary script search location.
    /// </summary>
    string? CurrentFileDirectory { get; }

    /// <summary>
    /// The Neverwinter Nights installation/user path.
    /// Used to find HAKs in the user hak folder.
    /// </summary>
    string? NeverwinterNightsPath { get; }

    /// <summary>
    /// Path to external script editor (optional).
    /// </summary>
    string? ExternalEditorPath { get; }

    /// <summary>
    /// Whether game resources (BIF files) are available for built-in script lookup.
    /// </summary>
    bool GameResourcesAvailable { get; }

    /// <summary>
    /// Lists all built-in scripts from game BIF files.
    /// </summary>
    IEnumerable<(string ResRef, string SourcePath)> ListBuiltInScripts();

    /// <summary>
    /// Finds a resource from game BIF files.
    /// </summary>
    /// <param name="resRef">Resource reference name</param>
    /// <param name="resourceType">Resource type (use ResourceTypes.Nss for scripts)</param>
    byte[]? FindBuiltInResource(string resRef, ushort resourceType);
}
