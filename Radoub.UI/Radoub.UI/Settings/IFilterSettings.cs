using Radoub.UI.Models;

namespace Radoub.UI.Settings;

/// <summary>
/// Interface for filter state persistence.
/// Implementing classes should store filter state in application settings.
/// </summary>
public interface IFilterSettings
{
    /// <summary>
    /// Get saved filter state for a context.
    /// </summary>
    /// <param name="contextKey">Context identifier (e.g., "Backpack", "Palette").</param>
    /// <returns>Saved filter state, or null if none saved.</returns>
    FilterState? GetFilterState(string contextKey);

    /// <summary>
    /// Save filter state for a context.
    /// </summary>
    /// <param name="contextKey">Context identifier.</param>
    /// <param name="state">Filter state to save.</param>
    void SetFilterState(string contextKey, FilterState state);

    /// <summary>
    /// Persist changes to storage.
    /// </summary>
    void Save();
}
