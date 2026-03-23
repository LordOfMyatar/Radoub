using Radoub.Formats.Gff;
using Radoub.Formats.Search;

namespace Radoub.UI.Services.Search;

/// <summary>
/// Orchestrates batch replace: preview, backup, execute, changelog.
/// Groups changes by file, calls provider Replace() per file,
/// writes modified GFF back to disk.
/// </summary>
public class BatchReplaceService
{
    private readonly BackupService _backupService;
    private readonly SearchProviderFactory _providerFactory;

    public BatchReplaceService(BackupService backupService)
        : this(backupService, SearchProviderFactory.CreateDefault())
    {
    }

    public BatchReplaceService(BackupService backupService, SearchProviderFactory providerFactory)
    {
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
    }

    /// <summary>
    /// Generate a preview of all changes from search results.
    /// All changes are selected by default.
    /// </summary>
    public BatchReplacePreview PreviewReplace(
        IReadOnlyList<FileSearchResult> fileResults,
        string replacementText)
    {
        var preview = new BatchReplacePreview();

        foreach (var fileResult in fileResults)
        {
            var fileGroup = new FileChangeGroup
            {
                FilePath = fileResult.FilePath,
                FileName = fileResult.FileName
            };

            foreach (var match in fileResult.Matches)
            {
                if (!match.Field.IsReplaceable) continue;

                var change = new PendingChange
                {
                    Match = match,
                    ReplacementText = replacementText,
                    FilePath = fileResult.FilePath
                };

                preview.Changes.Add(change);
                fileGroup.Changes.Add(change);
            }

            if (fileGroup.Changes.Count > 0)
                preview.FileGroups.Add(fileGroup);
        }

        return preview;
    }

    /// <summary>
    /// Execute the replace operation on disk.
    /// Creates backup, applies changes, writes files, returns changelog.
    /// </summary>
    public async Task<BatchReplaceResult> ExecuteReplaceAsync(
        BatchReplacePreview preview, string moduleName)
    {
        var selectedChanges = preview.Changes.Where(c => c.IsSelected).ToList();
        if (selectedChanges.Count == 0)
        {
            return new BatchReplaceResult { Success = true, FilesModified = 0 };
        }

        // Group selected changes by file
        var changesByFile = selectedChanges
            .GroupBy(c => c.FilePath)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Backup all affected files
        var filePaths = changesByFile.Keys.ToList();
        var manifest = await _backupService.BackupFilesAsync(filePaths, moduleName);

        var allResults = new List<ReplaceResult>();
        var filesModified = 0;

        try
        {
            foreach (var (filePath, changes) in changesByFile)
            {
                var fileBytes = await File.ReadAllBytesAsync(filePath);
                var gffFile = GffReader.Read(fileBytes);

                // Determine resource type from file extension
                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                var resourceType = Radoub.Formats.Common.ResourceTypes.FromExtension(ext);
                var provider = _providerFactory.GetProvider(resourceType);

                if (provider == null)
                {
                    allResults.AddRange(changes.Select(c => new ReplaceResult
                    {
                        Success = false, Field = c.Match.Field,
                        OldValue = c.Match.FullFieldValue, NewValue = c.ReplacementText,
                        Skipped = true, SkipReason = $"No provider for extension: {ext}"
                    }));
                    continue;
                }

                // Build ReplaceOperations from PendingChanges
                var operations = changes.Select(c => new ReplaceOperation
                {
                    Match = c.Match,
                    ReplacementText = c.ReplacementText
                }).ToList();

                var results = provider.Replace(gffFile, operations);
                allResults.AddRange(results);

                // Write modified GFF back to disk if any replacements succeeded
                if (results.Any(r => r.Success))
                {
                    var modifiedBytes = GffWriter.Write(gffFile);
                    await File.WriteAllBytesAsync(filePath, modifiedBytes);
                    filesModified++;
                }
            }

            return new BatchReplaceResult
            {
                Success = true,
                FilesModified = filesModified,
                ReplacementsMade = allResults.Count(r => r.Success),
                BackupManifest = manifest,
                ChangeLog = allResults
            };
        }
        catch (Exception ex)
        {
            // Attempt rollback on failure
            await _backupService.RestoreAsync(manifest);

            return new BatchReplaceResult
            {
                Success = false,
                Error = ex.Message,
                BackupManifest = manifest,
                ChangeLog = allResults
            };
        }
    }
}
