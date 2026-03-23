namespace Radoub.UI.Services.Search;

/// <summary>
/// Tracks all files backed up during a batch replace operation.
/// </summary>
public class BackupManifest
{
    /// <summary>Directory containing all backup copies</summary>
    public required string BackupDirectory { get; init; }

    /// <summary>When the backup was created</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>Module name for organizational context</summary>
    public required string ModuleName { get; init; }

    /// <summary>Individual file backup entries</summary>
    public List<BackupEntry> Entries { get; init; } = new();
}

/// <summary>
/// A single file's backup record with hash for integrity verification.
/// </summary>
public class BackupEntry
{
    /// <summary>Original file path that was backed up</summary>
    public required string OriginalPath { get; init; }

    /// <summary>Path to the backup copy</summary>
    public required string BackupPath { get; init; }

    /// <summary>SHA256 hash of the original file at backup time</summary>
    public required string Sha256Hash { get; init; }
}
