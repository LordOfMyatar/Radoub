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

        // Point the browser at the current module so it lists its .utp files.
        var modulePath = RadoubSettings.Instance.CurrentModulePath;
        if (!string.IsNullOrEmpty(modulePath))
        {
            var moduleDir = Path.GetDirectoryName(modulePath);
            if (!string.IsNullOrEmpty(moduleDir) && Directory.Exists(moduleDir))
                browser.ModulePath = moduleDir;
        }
    }

    private async void OnBrowserFileSelected(object? sender, FileSelectedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Entry.FilePath)) return;

        // Already open — nothing to discard or reload.
        if (string.Equals(_currentFilePath, e.Entry.FilePath, StringComparison.OrdinalIgnoreCase))
            return;

        // Prompt before discarding unsaved edits on the current placeable.
        if (!await ConfirmDiscardAsync()) return;

        var browser = this.FindControl<PlaceableBrowserPanel>("PlaceableBrowserPanel");
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
