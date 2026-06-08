using System.Threading.Tasks;

namespace Radoub.UI.Controls;

/// <summary>
/// Hook for host tools to ask a file-browser panel to re-read a single row's
/// metadata (Tag / DisplayLabel) from disk. Used after a save so the browser
/// reflects the new values without a full reindex (#2199 Sprint 2).
/// </summary>
public interface IBrowserRowRefresher
{
    /// <summary>
    /// Refresh the browser row whose FilePath matches <paramref name="filePath"/>.
    /// No-op when the path is null/empty or no matching row exists.
    /// </summary>
    Task RefreshRowAsync(string filePath);
}

/// <summary>
/// Host-side helper that fires <see cref="IBrowserRowRefresher.RefreshRowAsync"/>
/// after a save. Defensive against null refresher (panel not yet wired) and
/// null/empty path (failed save / unsaved buffer). Extracted from save flows
/// so the post-save notify wiring is unit-testable without a host MainWindow.
/// </summary>
public static class BrowserSaveNotifier
{
    /// <summary>
    /// Tell the refresher to re-read the row for <paramref name="filePath"/>.
    /// </summary>
    public static Task NotifyAsync(IBrowserRowRefresher? refresher, string? filePath)
    {
        if (refresher == null) return Task.CompletedTask;
        if (string.IsNullOrEmpty(filePath)) return Task.CompletedTask;
        return refresher.RefreshRowAsync(filePath);
    }

    /// <summary>
    /// Save-flow notify that handles a brand-new file as well as an existing one (#2413):
    /// if <paramref name="filePath"/> already has a row, do the cheap in-place metadata refresh;
    /// otherwise reload the list (so the new row appears) and select the new row. A new file has
    /// no row yet, so the plain <see cref="NotifyAsync"/> would silently no-op and the user would
    /// have to refresh manually. Folding the branch here means every single-resource editor
    /// (Reliquary, Relique, Fence) gets correct Save As behavior from one place.
    /// </summary>
    public static async Task NotifyOrAddAsync(FileBrowserPanelBase? panel, string? filePath)
    {
        if (panel == null || string.IsNullOrEmpty(filePath)) return;

        if (panel.FindEntryByFilePath(filePath) != null)
        {
            // Existing row → lightweight metadata refresh (keeps scroll/selection).
            await NotifyAsync(panel as IBrowserRowRefresher, filePath);
            return;
        }

        // New path → full reload so the row is created, then select it.
        await panel.RefreshAsync();
        panel.SelectEntryByFilePath(filePath);
    }
}
