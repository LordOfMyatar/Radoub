using Avalonia.Media.Imaging;

namespace Radoub.UI.Services;

/// <summary>
/// Represents a portrait entry in the browser with metadata.
/// </summary>
public class PortraitEntry
{
    /// <summary>
    /// Portrait ID from portraits.2da.
    /// </summary>
    public ushort Id { get; set; }

    /// <summary>
    /// Base resource reference (e.g., "po_elf_m_").
    /// </summary>
    public string ResRef { get; set; } = "";

    /// <summary>
    /// Race ID from portraits.2da (or -1 if unknown).
    /// </summary>
    public int Race { get; set; } = -1;

    /// <summary>
    /// Sex/Gender ID from portraits.2da (0=Male, 1=Female, -1=unknown).
    /// </summary>
    public int Sex { get; set; } = -1;

    /// <summary>
    /// Display-friendly name (resolved from TLK or fallback).
    /// </summary>
    public string DisplayName => string.IsNullOrEmpty(ResRef) ? $"Portrait {Id}" : ResRef;
}

/// <summary>
/// Interface for providing context to the portrait browser.
/// Implementations provide tool-specific paths and services.
/// Issue #970 - Part of Epic #959 (UI Uniformity).
/// </summary>
public interface IPortraitBrowserContext
{
    /// <summary>
    /// The current file's directory (for local portrait lookup).
    /// </summary>
    string? CurrentFileDirectory { get; }

    /// <summary>
    /// The Neverwinter Nights user documents path.
    /// Used to find override portraits.
    /// </summary>
    string? NeverwinterNightsPath { get; }

    /// <summary>
    /// Whether game resources (BIF files) are available for portrait lookup.
    /// </summary>
    bool GameResourcesAvailable { get; }

    /// <summary>
    /// Lists all portraits from portraits.2da.
    /// </summary>
    /// <returns>List of portrait entries with metadata</returns>
    IEnumerable<PortraitEntry> ListPortraits();

    /// <summary>
    /// Gets a portrait bitmap by ResRef.
    /// </summary>
    /// <param name="resRef">Portrait resource reference</param>
    /// <returns>Portrait bitmap, or null if not found</returns>
    Bitmap? GetPortraitBitmap(string resRef);

    /// <summary>
    /// Gets the display name for a race ID.
    /// </summary>
    /// <param name="raceId">Race ID from racialtypes.2da</param>
    /// <returns>Localized race name</returns>
    string GetRaceName(int raceId);
}
