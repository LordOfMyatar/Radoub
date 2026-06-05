using System.Collections.Generic;
using System.Threading.Tasks;
using Radoub.UI.Services.Search;

namespace ItemEditor.Services;

/// <summary>
/// Deletes item files with a backup-first guarantee (#2347). Relique previously
/// deleted with a bare File.Delete and no backup, making the action
/// unrecoverable. This snapshots the file to ~/Radoub/Backups/{module}/{timestamp}/
/// (the shared backup root used by every other destructive Radoub operation)
/// before removing it, so a misclick can be restored.
/// </summary>
public static class FileDeletionService
{
    /// <summary>
    /// Back up <paramref name="filePath"/> then delete it. Returns the backup
    /// manifest so callers can surface the restore location.
    /// </summary>
    public static async Task<BackupManifest> DeleteWithBackupAsync(
        string filePath, string moduleName, BackupService backupService)
    {
        var manifest = await backupService.BackupFilesAsync(new List<string> { filePath }, moduleName);
        System.IO.File.Delete(filePath);
        return manifest;
    }
}
