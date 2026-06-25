using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Radoub.Formats.Logging;

namespace Radoub.UI.Controls;

/// <summary>
/// FileBrowserPanelBase partial: sort-column setup and background metadata indexing (including the
/// detach-time CTS cleanup, #2262). Split from the monolithic code-behind (#2426); no behavior change.
/// </summary>
public partial class FileBrowserPanelBase
{
    #region Sort Mode + Indexing

    /// <summary>
    /// Apply the panel's <see cref="SupportedSortModes"/> to the DataGrid columns:
    /// hide unsupported columns (e.g., Parley DLG hides Name/Tag), and update
    /// the search-box watermark to match the current SortMode.
    /// </summary>
    private void InitializeSortColumns()
    {
        var modes = SupportedSortModes ?? new[] { BrowserSortMode.ResRef };
        var modeSet = new HashSet<BrowserSortMode>(modes);

        NameColumn.IsVisible = modeSet.Contains(BrowserSortMode.Name);
        TagColumn.IsVisible = modeSet.Contains(BrowserSortMode.Tag);

        UpdateSearchWatermark();
        SyncColumnSortIndicators();
    }

    private void UpdateSearchWatermark()
    {
        SearchBox.Watermark = _sortMode switch
        {
            BrowserSortMode.Name => "Search by name...",
            BrowserSortMode.Tag => "Search by tag...",
            _ => "Search by resref..."
        };
    }

    /// <summary>
    /// Cancel any in-flight indexing task. Called on module change and on
    /// detach (#2262) so a host disposing the panel mid-index doesn't orphan
    /// the CancellationTokenSource and its background Task.
    /// </summary>
    private void CancelIndexing()
    {
        if (_indexingCts != null)
        {
            _indexingCts.Cancel();
            _indexingCts.Dispose();
            _indexingCts = null;
        }
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        // Free the in-flight indexing CTS so detaching the panel mid-index
        // doesn't leak it until the next ModulePath setter (#2262).
        CancelIndexing();
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    /// Start a background indexing pass for any entries that don't yet have
    /// metadata. Cancels and replaces any in-flight indexing. On completion,
    /// re-applies the current filter so searches that ran during indexing
    /// see fresh DisplayLabel/Tag data.
    /// </summary>
    private void KickoffIndexing()
    {
        CancelIndexing();

        var pending = _allEntries.Where(e => !e.MetadataLoaded).ToList();
        if (pending.Count == 0) return;

        _indexingCts = new CancellationTokenSource();
        var token = _indexingCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await IndexMetadataAsync(pending, token);

                if (token.IsCancellationRequested) return;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested) return;
                    ApplyFilter();
                });
            }
            catch (OperationCanceledException)
            {
                // Expected on module change — no logging needed.
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"FileBrowserPanel: Indexing error: {ex.Message}");
            }
        }, token);
    }

    #endregion
}
