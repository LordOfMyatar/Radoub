using System;
using System.Collections.Generic;
using System.IO;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Logging;
using Radoub.UI.Services;

namespace RadoubLauncher.Services;

/// <summary>
/// Adds asset files to an existing ERF/MOD archive, zip-style (#2268): each source file
/// becomes a resource whose ResRef is the filename stem and whose type is derived from the
/// extension. Files keep their identity, exactly like entries in a zip.
///
/// Wraps <see cref="ErfReader"/>/<see cref="ErfWriter"/> with the same backup + atomic-replace
/// robustness as <see cref="ErfWriter.UpdateResource"/>, but batches many files in one rewrite.
/// </summary>
public class ErfAssetService
{
    private readonly string _backupRoot;

    /// <param name="backupRoot">Root backup directory (defaults to ~/Radoub/Backups). Archive
    /// backups land in its <see cref="BackupCleanupService.ArchivesBucket"/> subfolder.</param>
    public ErfAssetService(string? backupRoot = null)
    {
        _backupRoot = backupRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Radoub", "Backups");
    }

    /// <summary>
    /// Add the given files to <paramref name="erfPath"/>.
    /// </summary>
    /// <param name="erfPath">Existing ERF/MOD to add into.</param>
    /// <param name="filePaths">Source files on disk to add.</param>
    /// <param name="overwriteExisting">When true, a file whose ResRef+type already exists in the
    /// archive replaces it; when false, such files are skipped.</param>
    /// <param name="createBackup">When true (default), a timestamped backup is written before modifying.</param>
    /// <returns>A summary of what was added, skipped, and rejected.</returns>
    /// <exception cref="FileNotFoundException">The ERF does not exist.</exception>
    public ErfAddResult AddFiles(string erfPath, IEnumerable<string> filePaths,
        bool overwriteExisting, bool createBackup = true)
    {
        if (!File.Exists(erfPath))
            throw new FileNotFoundException($"ERF archive not found: {erfPath}", erfPath);

        var result = new ErfAddResult();

        // Read the archive once into a buffer; extract existing resources from that buffer rather
        // than re-opening the file per resource (a large archive has many resources).
        var erfBuffer = File.ReadAllBytes(erfPath);
        var erf = ErfReader.Read(erfBuffer);

        // Materialize the existing archive contents so the rewrite preserves them.
        var resourceData = new Dictionary<(string ResRef, ushort Type), byte[]>();
        foreach (var entry in erf.Resources)
            resourceData[(entry.ResRef.ToLowerInvariant(), entry.ResourceType)] =
                ErfReader.ExtractResource(erfBuffer, entry);

        bool changed = false;

        foreach (var filePath in filePaths)
        {
            if (!File.Exists(filePath))
            {
                result.Errors.Add((Path.GetFileName(filePath), "Source file not found"));
                continue;
            }

            var stem = Path.GetFileNameWithoutExtension(filePath);
            var ext = Path.GetExtension(filePath);
            var type = ResourceTypes.FromExtension(ext);

            if (type == ResourceTypes.Invalid)
            {
                result.Errors.Add((stem, $"Unknown resource type for extension '{ext}'"));
                continue;
            }

            if (!IsResRefValid(stem))
            {
                result.Errors.Add((stem, "Invalid ResRef: must be 1-16 chars, alphanumeric/underscore, lowercase"));
                continue;
            }

            var key = (stem.ToLowerInvariant(), type);
            bool exists = resourceData.ContainsKey(key);
            if (exists && !overwriteExisting)
            {
                result.SkippedCount++;
                continue;
            }

            var bytes = File.ReadAllBytes(filePath);
            resourceData[key] = bytes;

            if (!exists)
            {
                erf.Resources.Add(new ErfResourceEntry
                {
                    ResRef = stem.ToLowerInvariant(),
                    ResourceType = type,
                    ResId = (uint)erf.Resources.Count,
                });
            }

            result.AddedCount++;
            changed = true;
        }

        if (changed)
            WriteWithBackup(erf, erfPath, resourceData, createBackup);

        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"ERF add: {result.AddedCount} added, {result.SkippedCount} skipped, {result.Errors.Count} rejected " +
            $"-> {PrivacyHelper.SanitizePath(erfPath)}");

        return result;
    }

    // Aurora ResRef: 1-16 chars, alphanumeric + underscore, lowercase. Mirrors the
    // AuroraFilenameValidator rules but operates on the already-extracted stem.
    private static bool IsResRefValid(string resRef)
    {
        if (string.IsNullOrEmpty(resRef) || resRef.Length > 16)
            return false;
        foreach (var c in resRef)
        {
            if (!(char.IsLetterOrDigit(c) && c <= 127) && c != '_')
                return false;
            if (char.IsUpper(c))
                return false;
        }
        return true;
    }

    // Backup + atomic replace, mirroring ErfWriter.UpdateResource so a failure mid-swap never
    // leaves the archive missing. The backup goes to the managed Archives bucket under
    // ~/Radoub/Backups/ so it falls under the backup retention policy — ERFs/HAKs are large and
    // should not pile up next to the working file (#2268).
    private void WriteWithBackup(ErfFile erf, string erfPath,
        Dictionary<(string ResRef, ushort Type), byte[]> resourceData, bool createBackup)
    {
        if (createBackup)
            BackupToArchivesBucket(erfPath);

        var tempPath = erfPath + ".tmp";
        try
        {
            ErfWriter.Write(erf, tempPath, resourceData);
            AtomicFile.Replace(tempPath, erfPath, backupPath: null);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    // Copy the archive to ~/Radoub/Backups/Archives/{name}_{yyyyMMdd_HHmmss}{ext}. The name keeps
    // the timestamp as the trailing two underscore-segments so BackupCleanupService's flat-file
    // retention parser recognizes it.
    private void BackupToArchivesBucket(string erfPath)
    {
        var backupRoot = Path.Combine(_backupRoot, BackupCleanupService.ArchivesBucket);
        Directory.CreateDirectory(backupRoot);

        var name = Path.GetFileNameWithoutExtension(erfPath);
        var ext = Path.GetExtension(erfPath);
        var now = DateTime.Now;

        var backupPath = Path.Combine(backupRoot, $"{name}_{now:yyyyMMdd_HHmmss}{ext}");

        // Two adds in the same second collide; bump the seconds forward until the name is free so
        // the timestamp stays the trailing segment (parser-compatible) and File.Copy won't clobber.
        var candidate = now;
        while (File.Exists(backupPath))
        {
            candidate = candidate.AddSeconds(1);
            backupPath = Path.Combine(backupRoot, $"{name}_{candidate:yyyyMMdd_HHmmss}{ext}");
        }

        File.Copy(erfPath, backupPath, overwrite: false);
    }
}

/// <summary>Summary of an <see cref="ErfAssetService.AddFiles"/> operation.</summary>
public class ErfAddResult
{
    /// <summary>Number of resources added or overwritten.</summary>
    public int AddedCount { get; set; }

    /// <summary>Number of files skipped because their ResRef+type already existed.</summary>
    public int SkippedCount { get; set; }

    /// <summary>Files rejected before adding: (name, reason).</summary>
    public List<(string Name, string Reason)> Errors { get; } = new();
}
