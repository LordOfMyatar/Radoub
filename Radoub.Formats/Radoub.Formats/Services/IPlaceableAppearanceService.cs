using System.Collections.Generic;

namespace Radoub.Formats.Services;

/// <summary>
/// Resolves placeable appearance IDs to model and display names from
/// placeables.2da (StrRef → TLK with LABEL fallback). Cascade-aware via
/// <see cref="IGameDataService"/> so custom content (CEP, PRC) is honored.
/// </summary>
public interface IPlaceableAppearanceService
{
    /// <summary>
    /// Gets the appearance entry for an ID, or null if the row is padding/missing.
    /// </summary>
    PlaceableAppearance? GetById(uint id);

    /// <summary>
    /// Gets all real placeable appearances (padding rows skipped). Cached.
    /// </summary>
    IReadOnlyList<PlaceableAppearance> GetAll();

    /// <summary>
    /// Gets the MDL model name (ModelName column) for an appearance ID, or null.
    /// </summary>
    string? GetModelName(uint id);

    /// <summary>
    /// Gets the display name (TLK via StrRef, falling back to LABEL, then a synthetic name).
    /// </summary>
    string GetDisplayName(uint id);

    /// <summary>
    /// Clears the cached <see cref="GetAll"/> snapshot. Call after the 2DA chain changes.
    /// </summary>
    void InvalidateCache();
}

/// <summary>
/// A single placeable appearance from placeables.2da.
/// </summary>
public record PlaceableAppearance(uint Id, string Label, string ModelName, string DisplayName);
