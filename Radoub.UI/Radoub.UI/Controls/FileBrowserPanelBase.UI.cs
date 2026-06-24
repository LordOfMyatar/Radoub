using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Radoub.UI.Services;

namespace Radoub.UI.Controls;

/// <summary>
/// FileBrowserPanelBase partial: status/collapse UI helpers and the search/selection/sort/collapse
/// event handlers. Split from the monolithic code-behind (#2426); no behavior change.
/// </summary>
public partial class FileBrowserPanelBase
{
    #region UI Helpers

    private void UpdateCollapsedState()
    {
        CollapseButton.Content = _isCollapsed ? "▶" : "◀";
        ToolTip.SetTip(CollapseButton, _isCollapsed ? "Expand panel" : "Collapse panel");

        // The actual collapse animation should be handled by the parent container
        // This just updates the button state
    }

    protected void ShowLoading(string message)
    {
        StatusPanel.IsVisible = true;
        StatusText.Text = message;
    }

    protected void HideLoading()
    {
        StatusPanel.IsVisible = false;
    }

    protected void ShowError(string message)
    {
        CountLabel.Text = message;
        CountLabel.Foreground = BrushManager.GetErrorBrush(this);
    }

    /// <summary>
    /// Update the status text during loading operations.
    /// </summary>
    protected void UpdateLoadingStatus(string message)
    {
        StatusText.Text = message;
    }

    #endregion

    #region Event Handlers (search / selection / sort / collapse)

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void OnFileGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (FileGrid.SelectedItem is FileBrowserEntry entry)
        {
            FileSelected?.Invoke(this, new FileSelectedEventArgs(entry, isDoubleClick: false));
        }
    }

    private void OnFileGridDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (FileGrid.SelectedItem is FileBrowserEntry entry)
        {
            FileSelected?.Invoke(this, new FileSelectedEventArgs(entry, isDoubleClick: true));
        }
    }

    /// <summary>
    /// Right-click should select the row under the pointer before the context menu
    /// opens (#2106 — fixes Copy-to-Module visibility on right-click). We walk up
    /// the visual tree from the click source to the DataGridRow and select its
    /// DataContext so context-menu Opening handlers see a non-null SelectedItem.
    /// </summary>
    private void OnFileGridContextRequested(object? sender, Avalonia.Controls.ContextRequestedEventArgs e)
    {
        var source = e.Source as Avalonia.Visual;
        while (source != null && source is not DataGridRow)
        {
            source = source.GetVisualParent();
        }

        if (source is DataGridRow row && row.DataContext is FileBrowserEntry entry)
        {
            FileGrid.SelectedItem = entry;
        }
    }

    /// <summary>
    /// Intercept DataGrid column-header sorts: translate the clicked column into
    /// a <see cref="BrowserSortMode"/>, set <see cref="SortMode"/> (which re-applies
    /// the filter), and cancel the built-in sort so module-first tier is preserved.
    /// </summary>
    private void OnFileGridSorting(object? sender, DataGridColumnEventArgs e)
    {
        BrowserSortMode? requested = null;
        if (ReferenceEquals(e.Column, ResRefColumn)) requested = BrowserSortMode.ResRef;
        else if (ReferenceEquals(e.Column, NameColumn)) requested = BrowserSortMode.Name;
        else if (ReferenceEquals(e.Column, TagColumn)) requested = BrowserSortMode.Tag;

        if (requested == null) return;

        // Suppress DataGrid's built-in sort (it would override our module-first tier).
        e.Handled = true;

        // Repeat-click on the active column flips direction; switching column
        // resets to ascending (the SortMode setter handles the reset).
        if (requested.Value == _sortMode)
        {
            SortDirection = _sortDirection == BrowserSortDirection.Ascending
                ? BrowserSortDirection.Descending
                : BrowserSortDirection.Ascending;
        }
        else
        {
            SortMode = requested.Value;
        }
    }

    /// <summary>
    /// No-op on Avalonia: the Avalonia DataGrid doesn't expose a settable
    /// SortDirection on DataGridColumn (WPF-only API). The built-in arrow
    /// indicator follows whichever column was last clicked, which is good
    /// enough — the actual sort state is reflected in the visible row order.
    /// Kept as a named seam so future Avalonia versions or a custom header
    /// adornment can plug in without changing the call sites.
    /// </summary>
    private void SyncColumnSortIndicators()
    {
        // Intentionally empty. See remarks above.
    }

    private void OnCollapseClick(object? sender, RoutedEventArgs e)
    {
        IsCollapsed = !IsCollapsed;
    }

    #endregion
}
