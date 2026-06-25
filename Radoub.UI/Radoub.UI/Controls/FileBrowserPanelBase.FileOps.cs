using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Radoub.Formats.Logging;
using Radoub.UI.Services;
using Radoub.UI.Views;

namespace Radoub.UI.Controls;

/// <summary>
/// FileBrowserPanelBase partial: context-menu enablement and the on-disk file operations
/// (delete-with-backup, copy, rename) plus the entry lookup/merge helpers. Split from the
/// monolithic code-behind (#2426); no behavior change.
/// </summary>
public partial class FileBrowserPanelBase
{
    #region Context Menu + File Operations

    private void OnContextMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Copy/Rename/Delete operate on on-disk module files only (not HAK/BIF
        // archive resources, which have no FilePath).
        var isModuleFile = FileGrid.SelectedItem is FileBrowserEntry entry
            && !entry.IsFromHak
            && !string.IsNullOrEmpty(entry.FilePath);
        DeleteMenuItem.IsEnabled = isModuleFile;
        CopyMenuItem.IsEnabled = isModuleFile;
        RenameMenuItem.IsEnabled = isModuleFile;
    }

    /// <summary>
    /// Confirm, back up, then delete a module file on disk (#2350). The confirm
    /// dialog, backup (to ~/Radoub/Backups/{module}/{timestamp}/), delete, and
    /// browser refresh are all handled here so every tool gets identical
    /// data-safe behavior — no per-tool hand-rolled File.Delete that loses data
    /// on a misclick. The host only reacts to <see cref="FileDeleted"/> to close
    /// the open file and update the status bar.
    /// </summary>
    private async void OnDeleteMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (FileGrid.SelectedItem is not FileBrowserEntry entry
            || entry.IsFromHak || string.IsNullOrEmpty(entry.FilePath))
            return;

        var filePath = entry.FilePath!;
        if (!File.Exists(filePath))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"FileBrowserPanel: Delete source missing: {UnifiedLogger.SanitizePath(filePath)}");
            return;
        }

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null) return;

        var fileName = Path.GetFileName(filePath);
        var confirmed = await DialogHelper.ShowConfirmAsync(
            owner, "Confirm Delete",
            $"Delete \"{fileName}\" from disk?\n\nA backup is saved to ~/Radoub/Backups first, so this can be restored.");
        if (!confirmed) return;

        var wasCurrentFile = !string.IsNullOrEmpty(_currentFilePath)
            && filePath.Equals(_currentFilePath, StringComparison.OrdinalIgnoreCase);

        try
        {
            // Release the file session lock before deleting the open file — the lock
            // sidecar lives next to the file and would otherwise survive the delete,
            // blocking other tools from editing a recreated file (#2257). No-op if
            // unlocked.
            if (wasCurrentFile)
                FileSessionLockService.ReleaseLock(filePath);

            // Back up before deleting so a misclick is recoverable (#2347/#2350).
            var modulePath = Radoub.Formats.Settings.RadoubSettings.Instance.CurrentModulePath;
            var moduleName = !string.IsNullOrEmpty(modulePath)
                ? Path.GetFileNameWithoutExtension(modulePath)
                : "unknown";
            await Services.Search.FileDeletionService.DeleteWithBackupAsync(
                filePath, moduleName, new Services.Search.BackupService());
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Deleted file (backed up): {UnifiedLogger.SanitizePath(filePath)}");

            await RefreshAsync();
            FileDeleted?.Invoke(this, new FileDeletedEventArgs(filePath, wasCurrentFile));
        }
        catch (IOException ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"FileBrowserPanel: Delete failed: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"FileBrowserPanel: Delete failed (access): {ex.Message}");
        }
    }

    /// <summary>
    /// Copy a module file to a new ResRef in the same directory (#2320). The
    /// disk copy, browser refresh, and post-event are all handled here so every
    /// tool gets identical behavior — no per-tool refresh drift. The host only
    /// reacts to <see cref="FileCopied"/> for status-bar feedback.
    /// </summary>
    private async void OnCopyMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (FileGrid.SelectedItem is not FileBrowserEntry entry
            || entry.IsFromHak || string.IsNullOrEmpty(entry.FilePath))
            return;

        var sourcePath = entry.FilePath!;
        if (!File.Exists(sourcePath))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"FileBrowserPanel: Copy source missing: {UnifiedLogger.SanitizePath(sourcePath)}");
            return;
        }

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null) return;

        var directory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var currentName = Path.GetFileNameWithoutExtension(sourcePath);
        var extension = Path.GetExtension(sourcePath);

        var newName = await RenameDialog.ShowAsync(
            owner, currentName, directory, extension, actionLabel: "Copy", allowUnchanged: true);
        if (string.IsNullOrEmpty(newName)) return; // cancelled

        var resolved = FileBrowserOperations.ResolveCopyDestination(sourcePath, newName, extension);
        if (!resolved.IsValid || resolved.DestinationPath == null)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"FileBrowserPanel: Copy rejected: {resolved.ErrorMessage}");
            return;
        }

        try
        {
            // overwrite:false — the dialog already rejected an existing target.
            File.Copy(sourcePath, resolved.DestinationPath, overwrite: false);
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Copied file: {UnifiedLogger.SanitizePath(sourcePath)} -> {UnifiedLogger.SanitizePath(resolved.DestinationPath)}");

            await RefreshAsync();
            FileCopied?.Invoke(this, new FileCopiedEventArgs(sourcePath, resolved.DestinationPath));
        }
        catch (IOException ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"FileBrowserPanel: Copy failed: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"FileBrowserPanel: Copy failed (access): {ex.Message}");
        }
    }

    /// <summary>
    /// Rename a module file on disk to a new ResRef in the same directory
    /// (#2320). The disk move, browser refresh (drop stale row + re-scan), and
    /// post-event are handled here. The host reacts to <see cref="FileRenamed"/>
    /// to fix editor state — e.g. update the open-file path if the renamed file
    /// was the one loaded — and the status bar.
    /// </summary>
    private async void OnRenameMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (FileGrid.SelectedItem is not FileBrowserEntry entry
            || entry.IsFromHak || string.IsNullOrEmpty(entry.FilePath))
            return;

        var sourcePath = entry.FilePath!;
        if (!File.Exists(sourcePath))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"FileBrowserPanel: Rename source missing: {UnifiedLogger.SanitizePath(sourcePath)}");
            return;
        }

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null) return;

        var directory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var currentName = Path.GetFileNameWithoutExtension(sourcePath);
        var extension = Path.GetExtension(sourcePath);

        // Prompt + validate up front so both paths (open / not-open) share the
        // exact same name validation. Clicking a row to reach this menu usually
        // also loads it, so the open-file case is the common one (#2320).
        var newName = await RenameDialog.ShowAsync(
            owner, currentName, directory, extension, actionLabel: "Rename", allowUnchanged: false);
        if (string.IsNullOrEmpty(newName)) return; // cancelled

        var resolved = FileBrowserOperations.ResolveRenameDestination(sourcePath, newName, extension);
        if (!resolved.IsValid || resolved.DestinationPath == null)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"FileBrowserPanel: Rename rejected: {resolved.ErrorMessage}");
            return;
        }

        // Renaming the currently-open file is the host's job — it holds the
        // session lock and the in-memory ResRef the panel can't see. Hand it the
        // already-validated destination and let it save → move → reload.
        if (!string.IsNullOrEmpty(_currentFilePath)
            && sourcePath.Equals(_currentFilePath, StringComparison.OrdinalIgnoreCase))
        {
            FileRenameRequested?.Invoke(this,
                new FileRenameRequestedEventArgs(entry, sourcePath, resolved.DestinationPath));
            return;
        }

        try
        {
            File.Move(sourcePath, resolved.DestinationPath);
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Renamed file: {UnifiedLogger.SanitizePath(sourcePath)} -> {UnifiedLogger.SanitizePath(resolved.DestinationPath)}");

            // Drop the stale pre-rename row before re-scanning so it doesn't
            // linger pointing at a path that no longer exists (#2285 pattern).
            RemoveEntryByFilePath(sourcePath);
            await RefreshAsync();
            FileRenamed?.Invoke(this, new FileRenamedEventArgs(sourcePath, resolved.DestinationPath));
        }
        catch (IOException ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"FileBrowserPanel: Rename failed: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"FileBrowserPanel: Rename failed (access): {ex.Message}");
        }
    }

    #endregion

    #region Entry Lookup + Merge

    /// <summary>
    /// Call this from derived classes when custom filters change (e.g., "Show HAK" checkbox).
    /// </summary>
    protected void OnFilterOptionsChanged()
    {
        ApplyFilter();
    }

    /// <summary>
    /// Merge additional entries into the master list (with name-based dedup).
    /// Call after lazy-loading entries (e.g., on checkbox toggle) so they
    /// become visible to ApplyFilter/ApplyCustomFilters. Also triggers a
    /// background indexing pass over any newly added entries.
    /// </summary>
    protected void MergeAdditionalEntries(IEnumerable<FileBrowserEntry> entries)
    {
        var materialized = entries.ToList();
        MergeEntries(_allEntries, materialized);
        KickoffIndexing();
    }

    /// <summary>
    /// Merge source entries into target list, skipping entries whose Name
    /// already exists (case-insensitive). Extracted for testability.
    /// </summary>
    internal static void MergeEntries(List<FileBrowserEntry> target, IEnumerable<FileBrowserEntry> source)
    {
        foreach (var entry in source)
        {
            if (!target.Any(e => e.Name.Equals(entry.Name, StringComparison.OrdinalIgnoreCase)))
            {
                target.Add(entry);
            }
        }
    }

    /// <summary>
    /// Locate a browser entry by full file path (case-insensitive). Host tools
    /// call this after saving a file so they can hand the entry to
    /// <see cref="RefreshEntryMetadataAsync"/> for a targeted re-read (#2199).
    /// Returns null when the path is empty/null, no entry matches, or the
    /// matching entry has no FilePath (HAK/BIF rows).
    /// </summary>
    public FileBrowserEntry? FindEntryByFilePath(string filePath)
        => FindEntryByFilePath(_allEntries, filePath);

    /// <summary>
    /// Select the grid row whose FilePath matches <paramref name="filePath"/>, if present.
    /// Used after a new-file save reloads the list so the new row is highlighted (#2413).
    /// No-op when no matching row exists.
    /// </summary>
    public void SelectEntryByFilePath(string filePath)
    {
        var entry = FindEntryByFilePath(filePath);
        if (entry != null) FileGrid.SelectedItem = entry;
    }

    /// <summary>
    /// Pure-logic overload for testing. Same semantics as the instance method
    /// but operates on a caller-supplied entry list.
    /// </summary>
    internal static FileBrowserEntry? FindEntryByFilePath(
        IEnumerable<FileBrowserEntry> entries,
        string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return null;
        foreach (var entry in entries)
        {
            if (!string.IsNullOrEmpty(entry.FilePath)
                && entry.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }
        return null;
    }

    /// <summary>
    /// Remove the entry whose FilePath matches <paramref name="filePath"/> and
    /// rebind the DataGrid. Host tools call this after a rename so the stale
    /// pre-rename row doesn't linger pointing at a path that no longer exists
    /// (#2285). Returns true when an entry was found and removed; false on
    /// null/empty path or unknown path.
    /// </summary>
    public bool RemoveEntryByFilePath(string filePath)
    {
        var removed = RemoveEntryByFilePath(_allEntries, filePath);
        if (removed) ApplyFilter();
        return removed;
    }

    /// <summary>
    /// Pure-logic overload for testing. Same semantics as the instance method
    /// but operates on a caller-supplied entry list. Returns true when an
    /// entry was removed.
    /// </summary>
    internal static bool RemoveEntryByFilePath(List<FileBrowserEntry> entries, string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return false;
        var index = entries.FindIndex(e =>
            !string.IsNullOrEmpty(e.FilePath)
            && e.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return false;
        entries.RemoveAt(index);
        return true;
    }

    #endregion
}
