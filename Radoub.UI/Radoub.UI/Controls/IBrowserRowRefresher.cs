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
}
