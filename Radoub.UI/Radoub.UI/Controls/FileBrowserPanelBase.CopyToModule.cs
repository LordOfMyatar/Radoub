using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Radoub.Formats.Erf;
using Radoub.Formats.Logging;
using Radoub.UI.Services;
using Radoub.UI.Views;

namespace Radoub.UI.Controls;

/// <summary>
/// FileBrowserPanelBase partial: the shared Copy-to-Module flow for archive-sourced (HAK/BIF)
/// entries — extension points, the lazy context-menu item, and the extract/customize/write
/// pipeline. Split from the monolithic code-behind (#2426); no behavior change.
/// </summary>
public partial class FileBrowserPanelBase
{
    #region Copy-to-Module (shared across all derived panels)

    /// <summary>
    /// Whether this panel exposes a "Copy to Module" context menu item for
    /// archive-sourced entries. Override to return true in panels that wrap
    /// a GFF file format (UTM/UTC/UTI/DLG).
    /// </summary>
    protected virtual bool SupportsCopyToModule() => false;

    /// <summary>
    /// Whether the Copy-to-Module dialog should show editable Tag/Name fields.
    /// UTM/UTC/UTI: true. DLG (Parley): false — ResRef only.
    /// </summary>
    protected virtual bool SupportsTagNameRename() => true;

    /// <summary>
    /// Extract the raw bytes of an archive-sourced entry (BIF or HAK).
    /// Returning null aborts the copy. Default implementation returns null.
    /// </summary>
    protected virtual Task<byte[]?> ExtractArchiveBytesAsync(FileBrowserEntry entry)
        => Task.FromResult<byte[]?>(null);

    /// <summary>
    /// Read the source resource's Tag and default-language Name so the dialog
    /// can pre-fill those fields. Default returns empty strings.
    /// Only called when <see cref="SupportsTagNameRename"/> returns true.
    /// </summary>
    protected virtual Task<(string tag, string name)> ReadSourceMetadataAsync(byte[] bytes)
        => Task.FromResult((string.Empty, string.Empty));

    /// <summary>
    /// Apply the user's Tag/Name/ResRef choices to the raw source bytes, returning
    /// the bytes that should be written to the module file. Default implementation
    /// returns input bytes unchanged (Parley path — ResRef-only rename).
    /// </summary>
    protected virtual Task<byte[]> ApplyCopyCustomizationsAsync(byte[] sourceBytes, CopyToModuleResult result)
        => Task.FromResult(sourceBytes);

    /// <summary>
    /// Shared HAK extraction helper — looks up a resource in a HAK/ERF by ResRef +
    /// resource type, returns the decompressed bytes, or null on any failure.
    /// </summary>
    protected static byte[]? ExtractFromHak(string hakPath, string resRef, ushort resourceType)
    {
        try
        {
            var erf = ErfReader.ReadMetadataOnly(hakPath);
            var entry = erf.Resources.FirstOrDefault(r =>
                r.ResRef.Equals(resRef, StringComparison.OrdinalIgnoreCase)
                && r.ResourceType == resourceType);

            return entry == null ? null : ErfReader.ExtractResource(hakPath, entry);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Failed to extract {resRef} from {Path.GetFileName(hakPath)}: {ex.Message}");
            return null;
        }
    }

    private MenuItem? _copyToModuleMenuItem;

    private void TryAddCopyToModuleMenuItem()
    {
        if (_copyToModuleMenuItem != null) return;
        if (!SupportsCopyToModule()) return;

        _copyToModuleMenuItem = new MenuItem { Header = "Copy to Module" };
        _copyToModuleMenuItem.Click += async (_, _) =>
        {
            if (FileGrid.SelectedItem is FileBrowserEntry entry)
                await CopyArchiveEntryToModuleAsync(entry);
        };

        // Insert before Delete so it appears at the top of the menu.
        FileListContextMenu.Items.Insert(0, _copyToModuleMenuItem);
        FileListContextMenu.Items.Insert(1, new Separator());

        FileListContextMenu.Opening += OnCopyToModuleContextMenuOpening;
    }

    private void OnCopyToModuleContextMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_copyToModuleMenuItem == null) return;

        var entry = FileGrid.SelectedItem as FileBrowserEntry;
        var isArchive = entry != null && IsArchiveEntry(entry);
        _copyToModuleMenuItem.IsVisible = isArchive && !string.IsNullOrEmpty(ModulePath);
    }

    /// <summary>
    /// Check whether an entry came from an archive (HAK or BIF) — i.e. it's
    /// a candidate for Copy-to-Module. Derived panels with their own "IsFromBif"
    /// flag override this to also accept BIF entries.
    /// </summary>
    protected virtual bool IsArchiveEntry(FileBrowserEntry entry) => entry.IsFromHak;

    private async Task CopyArchiveEntryToModuleAsync(FileBrowserEntry entry)
    {
        if (string.IsNullOrEmpty(ModulePath)) return;
        if (!SupportsCopyToModule())
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                "CopyArchiveEntryToModuleAsync called on a panel that does not support copy-to-module");
            return;
        }

        try
        {
            var bytes = await ExtractArchiveBytesAsync(entry);
            if (bytes == null)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Could not extract {entry.Name} from archive");
                return;
            }

            var (tag, name) = SupportsTagNameRename()
                ? await ReadSourceMetadataAsync(bytes)
                : (string.Empty, string.Empty);

            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    "Cannot show CopyToModuleDialog — no owner window");
                return;
            }

            var dialogResult = await CopyToModuleDialog.ShowAsync(
                owner,
                currentResRef: entry.Name,
                currentTag: tag,
                currentName: name,
                moduleDirectory: ModulePath,
                extension: FileExtension,
                showTagAndName: SupportsTagNameRename());

            if (dialogResult == null) return; // user cancelled

            var modifiedBytes = await ApplyCopyCustomizationsAsync(bytes, dialogResult);

            var destPath = Path.Combine(ModulePath, dialogResult.NewResRef + FileExtension);
            if (File.Exists(destPath))
            {
                // Dialog already validated this, but re-check defensively.
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Destination already exists: {Path.GetFileName(destPath)}");
                return;
            }

            await File.WriteAllBytesAsync(destPath, modifiedBytes);
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Copied archive resource to module: {UnifiedLogger.SanitizePath(destPath)}");

            FileCopiedToModule?.Invoke(this, destPath);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"Failed to copy {entry.Name} to module: {ex.Message}");
        }
    }

    #endregion
}
