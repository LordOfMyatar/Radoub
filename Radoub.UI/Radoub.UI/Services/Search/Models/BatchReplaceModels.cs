using Radoub.Formats.Search;

namespace Radoub.UI.Services.Search;

/// <summary>
/// A single pending change with selection state for the UI.
/// </summary>
public class PendingChange
{
    /// <summary>The original search match</summary>
    public required SearchMatch Match { get; init; }

    /// <summary>What to replace with</summary>
    public required string ReplacementText { get; init; }

    /// <summary>File path containing this match</summary>
    public required string FilePath { get; init; }

    /// <summary>User can toggle individual changes on/off</summary>
    public bool IsSelected { get; set; } = true;
}

/// <summary>
/// Groups pending changes by file for preview display.
/// </summary>
public class FileChangeGroup
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public List<PendingChange> Changes { get; init; } = new();
}

/// <summary>
/// Preview of all changes before execution, with per-match selectability.
/// </summary>
public class BatchReplacePreview
{
    /// <summary>All individual pending changes (flat list)</summary>
    public List<PendingChange> Changes { get; init; } = new();

    /// <summary>Changes grouped by file for tree display</summary>
    public List<FileChangeGroup> FileGroups { get; init; } = new();

    /// <summary>Total number of changes</summary>
    public int TotalChanges => Changes.Count;

    /// <summary>Number of selected changes</summary>
    public int SelectedChanges => Changes.Count(c => c.IsSelected);
}

/// <summary>
/// Result of a batch replace execution.
/// </summary>
public class BatchReplaceResult
{
    /// <summary>Whether the entire operation succeeded</summary>
    public bool Success { get; init; }

    /// <summary>Number of files that were modified</summary>
    public int FilesModified { get; init; }

    /// <summary>Total replacements made</summary>
    public int ReplacementsMade { get; init; }

    /// <summary>Backup manifest for undo</summary>
    public BackupManifest? BackupManifest { get; init; }

    /// <summary>Per-replacement results</summary>
    public List<ReplaceResult> ChangeLog { get; init; } = new();

    /// <summary>Error message if operation failed</summary>
    public string? Error { get; init; }
}
