namespace Radoub.UI.Services.Search;

/// <summary>
/// Outcome of a ResRefRenameOrchestrator.ExecuteAsync call.
/// </summary>
public class ResRefRenameResult
{
    public required bool Success { get; init; }

    /// <summary>Number of files renamed on disk.</summary>
    public int RenamedFiles { get; init; }

    /// <summary>Number of references rewritten (across all touched files).</summary>
    public int ReferencesUpdated { get; init; }

    /// <summary>Backup manifest from BackupService — non-null when backup phase succeeded.</summary>
    public BackupManifest? BackupManifest { get; init; }

    /// <summary>Error message when Success is false. Null when Success is true.</summary>
    public string? Error { get; init; }

    /// <summary>True when an automatic rollback was attempted after a failure.</summary>
    public bool RollbackAttempted { get; init; }

    /// <summary>True when rollback completed cleanly (all files restored, all renames reversed).
    /// False when rollback itself failed — caller should surface the BackupManifest path
    /// for manual recovery.</summary>
    public bool RollbackSucceeded { get; init; }

    public static ResRefRenameResult Ok(int renamed, int refsUpdated, BackupManifest manifest) => new()
    {
        Success = true,
        RenamedFiles = renamed,
        ReferencesUpdated = refsUpdated,
        BackupManifest = manifest
    };

    public static ResRefRenameResult Fail(string error, BackupManifest? manifest = null,
        bool rollbackAttempted = false, bool rollbackSucceeded = false) => new()
    {
        Success = false,
        Error = error,
        BackupManifest = manifest,
        RollbackAttempted = rollbackAttempted,
        RollbackSucceeded = rollbackSucceeded
    };
}
