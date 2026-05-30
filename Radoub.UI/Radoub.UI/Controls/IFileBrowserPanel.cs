using System;
using System.Threading.Tasks;

namespace Radoub.UI.Controls;

/// <summary>
/// Event args for when a file is selected in the browser panel.
/// </summary>
public class FileSelectedEventArgs : EventArgs
{
    /// <summary>
    /// The selected file entry.
    /// </summary>
    public FileBrowserEntry Entry { get; }

    /// <summary>
    /// Whether this was a double-click (or Enter key) indicating immediate action.
    /// Single-click sets selection; double-click triggers load.
    /// </summary>
    public bool IsDoubleClick { get; }

    public FileSelectedEventArgs(FileBrowserEntry entry, bool isDoubleClick = false)
    {
        Entry = entry;
        IsDoubleClick = isDoubleClick;
    }
}

/// <summary>
/// Base entry class for file browser panels.
/// Tool-specific panels can extend this with additional properties.
/// </summary>
public class FileBrowserEntry
{
    /// <summary>
    /// Resource name without extension (e.g., "merchant_01").
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Full file path for filesystem files. Null for HAK/BIF resources.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Source description: "Module", "HAK: filename", "LocalVault", etc.
    /// </summary>
    public string Source { get; set; } = "Module";

    /// <summary>
    /// True if this resource is from a HAK file (not directly on filesystem).
    /// </summary>
    public bool IsFromHak { get; set; }

    /// <summary>
    /// Path to the HAK file containing this resource. Only set if IsFromHak is true.
    /// </summary>
    public string? HakPath { get; set; }

    /// <summary>
    /// Localized in-game name (e.g., UTI.LocalizedName, UTM.LocName, UTC.FirstName+LastName).
    /// Null until the panel's IndexMetadataAsync hook populates it. Used by
    /// <see cref="BrowserSortMode.Name"/> sort/search.
    /// </summary>
    public string? DisplayLabel { get; set; }

    /// <summary>
    /// Script tag (e.g., UTI.Tag). Null until IndexMetadataAsync populates.
    /// Used by <see cref="BrowserSortMode.Tag"/> sort/search.
    /// </summary>
    public string? Tag { get; set; }

    /// <summary>
    /// True once DisplayLabel/Tag have been populated (or attempted) for this entry.
    /// Lets the panel skip re-indexing entries that already have metadata.
    /// </summary>
    public bool MetadataLoaded { get; set; }

    /// <summary>
    /// Display name shown in the list. Override for custom formatting.
    /// </summary>
    public virtual string DisplayName => IsFromHak ? $"{Name} ({Source})" : Name;

    public override string ToString() => DisplayName;
}

/// <summary>
/// Event args for when a file delete is requested from the browser panel.
/// </summary>
public class FileDeleteRequestedEventArgs : EventArgs
{
    /// <summary>
    /// The file entry to delete.
    /// </summary>
    public FileBrowserEntry Entry { get; }

    public FileDeleteRequestedEventArgs(FileBrowserEntry entry)
    {
        Entry = entry;
    }
}

/// <summary>
/// Event args raised when the user asks to rename the file that is CURRENTLY
/// OPEN in the editor (#2320). The panel does NOT touch the file in this case —
/// renaming an open file is entangled with the host's session lock and the
/// editor's in-memory ResRef, which the shared panel can't see. The host runs
/// its own lock-aware save-rename-reload, then should refresh the browser.
/// </summary>
public class FileRenameRequestedEventArgs : EventArgs
{
    /// <summary>The entry the user asked to rename (the open file).</summary>
    public FileBrowserEntry Entry { get; }

    public FileRenameRequestedEventArgs(FileBrowserEntry entry)
    {
        Entry = entry;
    }
}

/// <summary>
/// Event args raised AFTER the panel has renamed a file on disk and refreshed
/// the browser (#2320). The host uses this to fix editor state — if the renamed
/// file was the one open in the editor, update its current-file path — and to
/// update the status bar. The panel has already done the disk move and the
/// browser refresh; the host must NOT repeat those.
/// </summary>
public class FileRenamedEventArgs : EventArgs
{
    /// <summary>Full path of the file before the rename.</summary>
    public string OldPath { get; }

    /// <summary>Full path of the file after the rename.</summary>
    public string NewPath { get; }

    public FileRenamedEventArgs(string oldPath, string newPath)
    {
        OldPath = oldPath;
        NewPath = newPath;
    }
}

/// <summary>
/// Event args raised AFTER the panel has copied a file on disk and refreshed
/// the browser (#2320). The host uses this for status-bar feedback. The panel
/// has already done the disk copy and the browser refresh.
/// </summary>
public class FileCopiedEventArgs : EventArgs
{
    /// <summary>Full path of the source file that was copied.</summary>
    public string SourcePath { get; }

    /// <summary>Full path of the newly created copy.</summary>
    public string NewPath { get; }

    public FileCopiedEventArgs(string sourcePath, string newPath)
    {
        SourcePath = sourcePath;
        NewPath = newPath;
    }
}

/// <summary>
/// Interface for embeddable file browser panels.
/// Implemented by tool-specific panels (DialogBrowserPanel, StoreBrowserPanel, etc.)
/// </summary>
public interface IFileBrowserPanel
{
    /// <summary>
    /// Raised when a file is selected (single-click) or activated (double-click).
    /// </summary>
    event EventHandler<FileSelectedEventArgs>? FileSelected;

    /// <summary>
    /// Raised when the user requests deletion of a file from the browser panel.
    /// The parent window should handle confirmation and actual deletion.
    /// </summary>
    event EventHandler<FileDeleteRequestedEventArgs>? FileDeleteRequested;

    /// <summary>
    /// Raised when the panel's collapsed state changes.
    /// </summary>
    event EventHandler<bool>? CollapsedChanged;

    /// <summary>
    /// Refresh the file list from current sources.
    /// </summary>
    Task RefreshAsync();

    /// <summary>
    /// Set the search filter text.
    /// </summary>
    void SetFilter(string searchText);

    /// <summary>
    /// Get or set whether the panel is collapsed.
    /// </summary>
    bool IsCollapsed { get; set; }

    /// <summary>
    /// Get or set the currently highlighted file path.
    /// Used to highlight the currently loaded file in the list.
    /// </summary>
    string? CurrentFilePath { get; set; }

    /// <summary>
    /// Get or set the module directory to scan for files.
    /// </summary>
    string? ModulePath { get; set; }

    /// <summary>
    /// Get the total count of files currently displayed.
    /// </summary>
    int FileCount { get; }
}
