using System.Security.Cryptography;

namespace Radoub.UI.Services.Search;

/// <summary>
/// Creates backup copies of files before batch replace operations.
/// Supports individual file backup (expanded modules) and archive backup (.mod/.erf).
/// Backups stored at {backupRoot}/{ModuleName}/{Timestamp}/.
/// </summary>
public class BackupService
{
    private readonly string _backupRoot;

    /// <param name="backupRoot">Root backup directory (typically ~/Radoub/Backups/)</param>
    public BackupService(string? backupRoot = null)
    {
        _backupRoot = backupRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Radoub", "Backups");
    }

    /// <summary>
    /// Back up individual files (for expanded module directories).
    /// Each file is copied to the backup directory with its original filename.
    /// </summary>
    public async Task<BackupManifest> BackupFilesAsync(
        IReadOnlyList<string> filePaths, string moduleName)
    {
        var timestamp = DateTime.Now;
        var backupDir = GetBackupDirectory(moduleName, timestamp);
        Directory.CreateDirectory(backupDir);

        var manifest = new BackupManifest
        {
            BackupDirectory = backupDir,
            CreatedAt = timestamp,
            ModuleName = moduleName
        };

        foreach (var filePath in filePaths)
        {
            var fileName = Path.GetFileName(filePath);
            var backupPath = Path.Combine(backupDir, fileName);
            var hash = await CopyWithHashAsync(filePath, backupPath);

            manifest.Entries.Add(new BackupEntry
            {
                OriginalPath = filePath,
                BackupPath = backupPath,
                Sha256Hash = hash
            });
        }

        return manifest;
    }

    /// <summary>
    /// Back up a single archive file (.mod, .erf).
    /// </summary>
    public async Task<BackupManifest> BackupArchiveAsync(string archivePath, string moduleName)
    {
        return await BackupFilesAsync(new[] { archivePath }, moduleName);
    }

    /// <summary>
    /// Restore files from a backup manifest to their original locations.
    /// Verifies SHA256 hash integrity before restoring.
    /// Returns false if any hash verification fails (no files restored on failure).
    /// </summary>
    public async Task<bool> RestoreAsync(BackupManifest manifest)
    {
        // Verify all backup file hashes first
        foreach (var entry in manifest.Entries)
        {
            if (!File.Exists(entry.BackupPath))
                return false;

            var hash = await ComputeHashAsync(entry.BackupPath);
            if (hash != entry.Sha256Hash)
                return false;
        }

        // All verified — restore
        foreach (var entry in manifest.Entries)
        {
            File.Copy(entry.BackupPath, entry.OriginalPath, overwrite: true);
        }

        return true;
    }

    private string GetBackupDirectory(string moduleName, DateTime timestamp)
    {
        var timeFolder = timestamp.ToString("yyyyMMdd_HHmmss");
        return Path.Combine(_backupRoot, moduleName, timeFolder);
    }

    private static async Task<string> CopyWithHashAsync(string source, string destination)
    {
        using var sha256 = SHA256.Create();
        await using var sourceStream = File.OpenRead(source);
        await using var destStream = File.Create(destination);
        using var cryptoStream = new CryptoStream(destStream, sha256, CryptoStreamMode.Write);

        await sourceStream.CopyToAsync(cryptoStream);
        await cryptoStream.FlushFinalBlockAsync();

        return Convert.ToHexStringLower(sha256.Hash!);
    }

    private static async Task<string> ComputeHashAsync(string filePath)
    {
        using var sha256 = SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexStringLower(hash);
    }
}
