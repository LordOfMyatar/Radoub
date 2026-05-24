namespace Radoub.UI.Controls;

/// <summary>
/// Sort/search mode for file browser panels. The search field queries the
/// active field — e.g. SortMode=Name → search matches DisplayLabel.
/// </summary>
public enum BrowserSortMode
{
    /// <summary>Sort and search by ResRef (filename). Default; always available.</summary>
    ResRef,

    /// <summary>Sort and search by localized display name (FileBrowserEntry.DisplayLabel).</summary>
    Name,

    /// <summary>Sort and search by script tag (FileBrowserEntry.Tag).</summary>
    Tag
}
