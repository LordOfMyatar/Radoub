using System.Collections.Generic;
using System.Threading.Tasks;

namespace Radoub.UI.Services.Search;

/// <summary>
/// Deletes user files with a backup-first guarantee (#2350, originally Relique #2347).
/// Before #2350, three tools (Quartermaster, Fence, Parley) deleted browser files
/// with a bare File.Delete and no backup, making a misclick unrecoverable. This is
/// the single shared implementation that snapshots the file to
/// ~/Radoub/Backups/{module}/{timestamp}/ (the shared backup root used by every
/// destructive Radoub operation) before removing it. Used by
/// <see cref="Radoub.UI.Controls.FileBrowserPanelBase"/> so every tool inherits it.
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
