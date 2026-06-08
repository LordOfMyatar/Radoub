using System;
using System.IO;
using Avalonia.Controls;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.UI.Controls;
using PlaceableEditor.Views.Panels;

namespace PlaceableEditor.Views;

/// <summary>
/// Window/browser lifecycle for Reliquary's MainWindow (#2294). Wires the
/// PlaceableBrowserPanel events. Delete-with-backup is inherited from
/// FileBrowserPanelBase (#2350) — the host only reacts to FileDeleted.
/// </summary>
public partial class MainWindow
{
    private void WireBrowserPanel()
    {
        var browser = this.FindControl<PlaceableBrowserPanel>("PlaceableBrowserPanel");
        if (browser == null) return;

        browser.FileSelected += OnBrowserFileSelected;
        browser.FileDeleted += OnBrowserFileDeleted;

        // The base panel only flips its ◀/▶ button + raises CollapsedChanged; the host owns the
        // actual collapse by zeroing the browser's width and hiding the splitter (QM/Relique pattern).
        browser.CollapsedChanged += (_, collapsed) => SetBrowserCollapsed(collapsed);

        // Point the browser at the current module's working directory so it lists its .utp files.
        // CurrentModulePath is already the module dir (or a .mod file) — resolve, don't take the
        // parent (that pointed at the modules/ folder, which has no loose .utp).
        var moduleDir = GetModuleWorkingDirectory();
        if (!string.IsNullOrEmpty(moduleDir))
            browser.ModulePath = moduleDir;
    }

    /// <summary>
    /// Collapse/expand the browser sidebar. Zeroes the browser + splitter *columns* (not just the
    /// panel width) and hides both controls, so the editor reclaims the full row — mirroring Relique's
    /// SetItemBrowserPanelVisible. Zeroing only the panel width left the Auto column occupying a blank
    /// gap (#2363 follow-up).
    /// </summary>
    private void SetBrowserCollapsed(bool collapsed)
    {
        var browser = this.FindControl<PlaceableBrowserPanel>("PlaceableBrowserPanel");
        var splitter = this.FindControl<GridSplitter>("BrowserSplitter");
        var grid = this.FindControl<Grid>("OuterContentGrid");
        if (grid != null)
        {
            grid.ColumnDefinitions[0].Width = new Avalonia.Controls.GridLength(collapsed ? 0 : 260, Avalonia.Controls.GridUnitType.Pixel);
            grid.ColumnDefinitions[1].Width = new Avalonia.Controls.GridLength(collapsed ? 0 : 4, Avalonia.Controls.GridUnitType.Pixel);
        }
        if (browser != null) browser.IsVisible = !collapsed;
        if (splitter != null) splitter.IsVisible = !collapsed;
    }

    // --- Window size/position persistence (BaseToolSettingsService already stores the values). ---

    private void RestoreWindowPosition()
    {
        var settings = PlaceableEditor.Services.SettingsService.Instance;
        if (settings.WindowWidth > 0 && settings.WindowHeight > 0)
        {
            Width = settings.WindowWidth;
            Height = settings.WindowHeight;
        }
        if (settings.WindowLeft >= 0 && settings.WindowTop >= 0)
            Position = new Avalonia.PixelPoint((int)settings.WindowLeft, (int)settings.WindowTop);
    }

    private void SaveWindowPosition()
    {
        // Only persist a normal (non-maximized/minimized) window so we don't store maximized bounds.
        if (WindowState != Avalonia.Controls.WindowState.Normal) return;
        var settings = PlaceableEditor.Services.SettingsService.Instance;
        settings.WindowWidth = Width;
        settings.WindowHeight = Height;
        settings.WindowLeft = Position.X;
        settings.WindowTop = Position.Y;
    }

    private async void OnBrowserFileSelected(object? sender, FileSelectedEventArgs e)
    {
        var browser = this.FindControl<PlaceableBrowserPanel>("PlaceableBrowserPanel");
        var isArchive = e.Entry is PlaceableBrowserEntry { IsFromBif: true } || e.Entry.IsFromHak;

        // Archive (HAK/BIF) entries have no file path — load a read-only preview.
        if (isArchive)
        {
            if (!await ConfirmDiscardAsync()) return;
            var bytes = browser?.ExtractArchiveBytes(e.Entry);
            if (bytes == null)
            {
                UpdateStatus($"Could not extract {e.Entry.Name} from archives.");
                return;
            }
            LoadPlaceableFromBytes(bytes, e.Entry.Name);
            if (browser != null) browser.CurrentFilePath = null;
            return;
        }

        if (string.IsNullOrEmpty(e.Entry.FilePath)) return;

        // Already open — nothing to discard or reload.
        if (string.Equals(_currentFilePath, e.Entry.FilePath, StringComparison.OrdinalIgnoreCase))
            return;

        // Prompt before discarding unsaved edits on the current placeable.
        if (!await ConfirmDiscardAsync()) return;

        if (browser != null)
            browser.CurrentFilePath = e.Entry.FilePath;

        // Load the selected placeable into the editor (#2295).
        LoadPlaceable(e.Entry.FilePath);
    }

    // The browser panel owns confirm + backup + delete + refresh (#2350). The host
    // only clears its current-file tracking when the deleted file was the open one.
    private void OnBrowserFileDeleted(object? sender, FileDeletedEventArgs e)
    {
        if (e.WasCurrentFile)
            _currentFilePath = null;

        UpdateStatus($"Deleted {Path.GetFileName(e.FilePath)}");
        UnifiedLogger.LogApplication(LogLevel.INFO, $"Reliquary: deleted {UnifiedLogger.SanitizePath(e.FilePath)}");
    }
}
