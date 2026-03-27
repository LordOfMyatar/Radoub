using System.Globalization;
using Radoub.Formats.Logging;

namespace Radoub.UI.Services;

/// <summary>
/// Cleans up old backup files based on a retention policy (days).
/// Handles both batch replace backups (~/Radoub/Backups/{Module}/{Timestamp}/)
/// and single-file replace backups (~/Radoub/Backups/SearchReplace/{file}_{timestamp}.ext).
/// </summary>
public static class BackupCleanupService
{
    private static readonly string DefaultBackupRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Radoub", "Backups");

    /// <summary>
    /// Delete backup directories/files older than retentionDays.
    /// </summary>
    public static void CleanupExpiredBackups(int retentionDays, string? backupRoot = null)
    {
        var root = backupRoot ?? DefaultBackupRoot;
        if (!Directory.Exists(root))
            return;

        var cutoff = DateTime.Now.AddDays(-retentionDays);
        int deletedCount = 0;

        try
        {
            foreach (var moduleDir in Directory.GetDirectories(root))
            {
                var moduleDirName = Path.GetFileName(moduleDir);

                if (moduleDirName == "SearchReplace")
                {
                    deletedCount += CleanupSearchReplaceBackups(moduleDir, cutoff);
                }
                else
                {
                    deletedCount += CleanupBatchBackups(moduleDir, cutoff);
                }
            }

            if (deletedCount > 0)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Backup cleanup: removed {deletedCount} expired backup(s) (retention: {retentionDays} days)");
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Backup cleanup error: {ex.Message}");
        }
    }

    /// <summary>
    /// Delete all contents of the backup directory.
    /// </summary>
    public static void DeleteAllBackups(string? backupRoot = null)
    {
        var root = backupRoot ?? DefaultBackupRoot;
        if (!Directory.Exists(root))
            return;

        try
        {
            foreach (var dir in Directory.GetDirectories(root))
            {
                try { Directory.Delete(dir, true); }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Failed to delete backup dir: {ex.Message}");
                }
            }

            foreach (var file in Directory.GetFiles(root))
            {
                try { File.Delete(file); }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Failed to delete backup file: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Delete all backups error: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns (fileCount, totalBytes) for all backup content.
    /// </summary>
    public static (int fileCount, long totalBytes) GetBackupSummary(string? backupRoot = null)
    {
        var root = backupRoot ?? DefaultBackupRoot;
        if (!Directory.Exists(root))
            return (0, 0);

        try
        {
            var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            long totalBytes = 0;
            foreach (var file in files)
            {
                try { totalBytes += new FileInfo(file).Length; }
                catch { /* skip inaccessible files */ }
            }
            return (files.Length, totalBytes);
        }
        catch
        {
            return (0, 0);
        }
    }

    private static int CleanupBatchBackups(string moduleDir, DateTime cutoff)
    {
        int deleted = 0;

        foreach (var timestampDir in Directory.GetDirectories(moduleDir))
        {
            var dirName = Path.GetFileName(timestampDir);
            if (TryParseTimestamp(dirName, out var timestamp) && timestamp < cutoff)
            {
                try
                {
                    Directory.Delete(timestampDir, true);
                    deleted++;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Deleted expired batch backup: {timestampDir}");
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Failed to delete batch backup: {ex.Message}");
                }
            }
        }

        // Remove empty module directory
        try
        {
            if (Directory.Exists(moduleDir) &&
                Directory.GetDirectories(moduleDir).Length == 0 &&
                Directory.GetFiles(moduleDir).Length == 0)
            {
                Directory.Delete(moduleDir);
            }
        }
        catch { /* ignore */ }

        return deleted;
    }

    private static int CleanupSearchReplaceBackups(string searchReplaceDir, DateTime cutoff)
    {
        int deleted = 0;

        foreach (var file in Directory.GetFiles(searchReplaceDir))
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (TryParseFileTimestamp(fileName, out var timestamp) && timestamp < cutoff)
            {
                try
                {
                    File.Delete(file);
                    deleted++;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Deleted expired search/replace backup: {file}");
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Failed to delete search/replace backup: {ex.Message}");
                }
            }
        }

        // Remove empty SearchReplace directory
        try
        {
            if (Directory.Exists(searchReplaceDir) &&
                Directory.GetFiles(searchReplaceDir).Length == 0)
            {
                Directory.Delete(searchReplaceDir);
            }
        }
        catch { /* ignore */ }

        return deleted;
    }

    /// <summary>
    /// Parse timestamp from batch backup directory name (yyyyMMdd_HHmmss).
    /// </summary>
    private static bool TryParseTimestamp(string dirName, out DateTime timestamp)
    {
        return DateTime.TryParseExact(dirName, "yyyyMMdd_HHmmss",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp);
    }

    /// <summary>
    /// Parse timestamp from search/replace backup filename ({name}_{yyyyMMdd_HHmmss}).
    /// The timestamp is the last 15 characters before the extension.
    /// </summary>
    private static bool TryParseFileTimestamp(string fileNameWithoutExt, out DateTime timestamp)
    {
        timestamp = default;

        // Pattern: name_yyyyMMdd_HHmmss — timestamp is last 15 chars
        if (fileNameWithoutExt.Length < 16)
            return false;

        var lastUnderscore = fileNameWithoutExt.LastIndexOf('_');
        if (lastUnderscore < 9) // Need at least name_yyyyMMdd_HHmmss
            return false;

        // The timestamp spans two segments: yyyyMMdd_HHmmss
        var secondLastUnderscore = fileNameWithoutExt.LastIndexOf('_', lastUnderscore - 1);
        if (secondLastUnderscore < 0)
            return false;

        var timestampPart = fileNameWithoutExt.Substring(secondLastUnderscore + 1);
        return DateTime.TryParseExact(timestampPart, "yyyyMMdd_HHmmss",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp);
    }
}
