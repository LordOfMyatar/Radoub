namespace Radoub.UI.Settings;

/// <summary>
/// Interface for persisting column width settings.
/// Implementers should store widths by column key and context.
/// </summary>
public interface IColumnSettings
{
    /// <summary>
    /// Get the stored width for a column.
    /// </summary>
    /// <param name="contextKey">Context identifier (e.g., "Backpack", "Palette")</param>
    /// <param name="columnKey">Column identifier (e.g., "Name", "ResRef")</param>
    /// <returns>Stored width, or null if not set</returns>
    double? GetColumnWidth(string contextKey, string columnKey);

    /// <summary>
    /// Store the width for a column.
    /// </summary>
    /// <param name="contextKey">Context identifier</param>
    /// <param name="columnKey">Column identifier</param>
    /// <param name="width">Width to store</param>
    void SetColumnWidth(string contextKey, string columnKey, double width);

    /// <summary>
    /// Save all pending changes.
    /// </summary>
    void Save();
}
