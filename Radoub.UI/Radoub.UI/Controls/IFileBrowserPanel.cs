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
    /// Display name shown in the list. Override for custom formatting.
    /// </summary>
    public virtual string DisplayName => IsFromHak ? $"{Name} ({Source})" : Name;

    public override string ToString() => DisplayName;
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
