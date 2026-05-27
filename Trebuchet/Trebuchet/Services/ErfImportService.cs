using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Logging;

namespace RadoubLauncher.Services;

public class ErfImportService
{
    // Aurora ResRef: 1-16 chars, alphanumeric + underscore + hyphen.
    // Rejects path separators, "..", drive letters, NUL — anything a crafted
    // ERF could use to escape targetDirectory via Path.Combine (#2245).
    private static readonly Regex ValidResRef = new(@"^[a-zA-Z0-9_\-]{1,16}$", RegexOptions.Compiled);

    /// <summary>
    /// Detect which resources already exist in the target directory.
    /// Entries with invalid ResRefs are skipped silently (cannot conflict — won't be imported).
    /// </summary>
    public HashSet<string> DetectConflicts(IReadOnlyList<ErfResourceEntry> entries, string targetDirectory)
    {
        var conflicts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targetFullPath = Path.GetFullPath(targetDirectory);

        foreach (var entry in entries)
        {
            if (!IsResRefSafe(entry.ResRef))
                continue;

            var extension = ResourceTypes.GetExtension(entry.ResourceType);
            var candidate = Path.Combine(targetDirectory, entry.ResRef + extension);

            if (!IsPathInsideTarget(candidate, targetFullPath))
                continue;

            if (File.Exists(candidate))
                conflicts.Add(entry.ResRef);
        }

        return conflicts;
    }

    /// <summary>
    /// Import selected resources from an ERF archive into the target directory.
    /// Entries with invalid ResRefs (path traversal, illegal chars) are rejected and logged (#2245).
    /// </summary>
    public async Task<ErfImportResult> ImportResourcesAsync(
        string erfPath,
        IReadOnlyList<ErfResourceEntry> entries,
        string targetDirectory,
        bool overwriteExisting,
        IProgress<ImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ErfImportResult();
        var targetFullPath = Path.GetFullPath(targetDirectory);
        int processed = 0;

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var extension = ResourceTypes.GetExtension(entry.ResourceType);
            var fileName = entry.ResRef + extension;

            progress?.Report(new ImportProgress
            {
                Current = processed + 1,
                Total = entries.Count,
                CurrentResRef = entry.ResRef
            });

            // Layer 1: strict ResRef validation.
            if (!IsResRefSafe(entry.ResRef))
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"ERF import: rejected entry with invalid ResRef '{entry.ResRef}' (must match {ValidResRef})");
                result.Errors.Add((entry.ResRef, "Invalid ResRef: must be 1-16 chars alphanumeric/underscore/hyphen"));
                result.ErrorCount++;
                processed++;
                continue;
            }

            var targetPath = Path.Combine(targetDirectory, fileName);

            // Layer 2: defense-in-depth — verify resolved path stays inside targetDirectory.
            if (!IsPathInsideTarget(targetPath, targetFullPath))
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"ERF import: rejected entry '{entry.ResRef}' — resolved path escapes target directory");
                result.Errors.Add((entry.ResRef, "Path escapes target directory"));
                result.ErrorCount++;
                processed++;
                continue;
            }

            var fileExists = File.Exists(targetPath);

            if (fileExists && !overwriteExisting)
            {
                result.SkippedCount++;
                processed++;
                continue;
            }

            try
            {
                var data = await Task.Run(() => ErfReader.ExtractResource(erfPath, entry), cancellationToken);
                await File.WriteAllBytesAsync(targetPath, data, cancellationToken);

                if (fileExists)
                {
                    result.OverwrittenCount++;
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"ERF import: overwrote {fileName}");
                }
                else
                {
                    result.ImportedCount++;
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"ERF import: imported {fileName}");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to import {fileName}: {ex.Message}");
                result.Errors.Add((entry.ResRef, ex.Message));
                result.ErrorCount++;
            }

            processed++;
        }

        return result;
    }

    private static bool IsResRefSafe(string? resRef)
        => !string.IsNullOrEmpty(resRef) && ValidResRef.IsMatch(resRef);

    private static bool IsPathInsideTarget(string candidatePath, string targetFullPath)
    {
        string resolved;
        try
        {
            resolved = Path.GetFullPath(candidatePath);
        }
        catch
        {
            return false;
        }

        var expectedPrefix = targetFullPath.EndsWith(Path.DirectorySeparatorChar)
            ? targetFullPath
            : targetFullPath + Path.DirectorySeparatorChar;

        return resolved.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase);
    }
}

public class ErfImportResult
{
    public int ImportedCount { get; set; }
    public int OverwrittenCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public List<(string ResRef, string Error)> Errors { get; set; } = new();
    public int TotalWritten => ImportedCount + OverwrittenCount;
}

public class ImportProgress
{
    public int Current { get; init; }
    public int Total { get; init; }
    public string CurrentResRef { get; init; } = string.Empty;
}
