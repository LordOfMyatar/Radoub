using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Logging;

namespace RadoubLauncher.Services;

public class ErfImportService
{
    /// <summary>
    /// Detect which resources already exist in the target directory.
    /// </summary>
    public HashSet<string> DetectConflicts(IReadOnlyList<ErfResourceEntry> entries, string targetDirectory)
    {
        var conflicts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            var extension = ResourceTypes.GetExtension(entry.ResourceType);
            var filePath = Path.Combine(targetDirectory, entry.ResRef + extension);
            if (File.Exists(filePath))
                conflicts.Add(entry.ResRef);
        }

        return conflicts;
    }

    /// <summary>
    /// Import selected resources from an ERF archive into the target directory.
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
        int processed = 0;

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var extension = ResourceTypes.GetExtension(entry.ResourceType);
            var fileName = entry.ResRef + extension;
            var targetPath = Path.Combine(targetDirectory, fileName);

            progress?.Report(new ImportProgress
            {
                Current = processed + 1,
                Total = entries.Count,
                CurrentResRef = entry.ResRef
            });

            if (File.Exists(targetPath) && !overwriteExisting)
            {
                result.SkippedCount++;
                processed++;
                continue;
            }

            try
            {
                var data = await Task.Run(() => ErfReader.ExtractResource(erfPath, entry), cancellationToken);
                await File.WriteAllBytesAsync(targetPath, data, cancellationToken);
                result.ImportedCount++;
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
}

public class ErfImportResult
{
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public List<(string ResRef, string Error)> Errors { get; set; } = new();
}

public class ImportProgress
{
    public int Current { get; init; }
    public int Total { get; init; }
    public string CurrentResRef { get; init; } = string.Empty;
}
