using System;
using System.Collections.Generic;
using System.IO;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Logging;

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

        var erf = ErfReader.Read(erfPath);

        // Materialize the existing archive contents so the rewrite preserves them.
        var resourceData = new Dictionary<(string ResRef, ushort Type), byte[]>();
        foreach (var entry in erf.Resources)
            resourceData[(entry.ResRef.ToLowerInvariant(), entry.ResourceType)] =
                ErfReader.ExtractResource(erfPath, entry);

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
    // leaves the archive missing.
    private static void WriteWithBackup(ErfFile erf, string erfPath,
        Dictionary<(string ResRef, ushort Type), byte[]> resourceData, bool createBackup)
    {
        string? backupPath = null;
        if (createBackup)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var directory = Path.GetDirectoryName(erfPath) ?? ".";
            var fileName = Path.GetFileNameWithoutExtension(erfPath);
            var extension = Path.GetExtension(erfPath);
            backupPath = Path.Combine(directory, $"{fileName}_backup_{timestamp}{extension}");
            // The HHmmss timestamp collides when two adds land in the same second; disambiguate
            // so a rapid second add doesn't fail on File.Copy (overwrite: false).
            for (int n = 2; File.Exists(backupPath); n++)
                backupPath = Path.Combine(directory, $"{fileName}_backup_{timestamp}_{n}{extension}");
            File.Copy(erfPath, backupPath, overwrite: false);
        }

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
