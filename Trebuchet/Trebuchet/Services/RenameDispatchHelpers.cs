using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Radoub.Formats.Search.Rename;
using Radoub.UI.Services.Search;

namespace RadoubLauncher.Services;

/// <summary>
/// Helpers used by MarlinspikePanel to convert a BatchReplacePreview that contains
/// filename-row matches into a set of ResRefRenamePlan instances ready for
/// ResRefRenameOrchestrator. Pure logic — extracted from the codebehind so it
/// can be unit tested. Per spec Section 5 (rename dispatch path).
/// </summary>
public static class RenameDispatchHelpers
{
    /// <summary>
    /// True when the preview contains at least one match against the virtual
    /// filename field (FilenameSearchProvider.FilenameField). Indicates the Replace
    /// flow must dispatch to ResRefRenameOrchestrator instead of the standard
    /// BatchReplaceService.ExecuteReplaceAsync path.
    /// </summary>
    public static bool HasFilenameMatches(BatchReplacePreview preview)
    {
        if (preview == null) return false;
        return preview.Changes.Any(c => c.Match.Field.GffPath == FilenameSearchProvider.FilenameField.GffPath);
    }

    /// <summary>
    /// Build ResRefRenamePlans from filename-row PendingChanges in a preview.
    /// Skips entries whose validator result is invalid. Caller is responsible
    /// for surfacing skipped entries to the user (e.g., via status bar).
    /// </summary>
    /// <param name="rejectedReasons">
    /// Optional sink: each skipped entry appends a user-facing reason ("name" — error)
    /// so the caller can show the specific validator message instead of a generic
    /// "all rejected" line (#2182).
    /// </param>
    public static IReadOnlyList<ResRefRenamePlan> BuildRenamePlansFromPreview(
        BatchReplacePreview preview, string moduleDir, ResRefValidator validator,
        ICollection<string>? rejectedReasons = null)
    {
        if (preview == null) return Array.Empty<ResRefRenamePlan>();
        if (string.IsNullOrEmpty(moduleDir)) return Array.Empty<ResRefRenamePlan>();
        if (validator == null) throw new ArgumentNullException(nameof(validator));

        var plans = new List<ResRefRenamePlan>();
        var filenameChanges = preview.Changes
            .Where(c => c.Match.Field.GffPath == FilenameSearchProvider.FilenameField.GffPath)
            .ToList();

        if (filenameChanges.Count == 0) return plans;

        var existingResRefsByExt = BuildExistingResRefIndex(moduleDir);

        foreach (var change in filenameChanges)
        {
            var oldFilePath = change.FilePath;
            var oldName = Path.GetFileNameWithoutExtension(oldFilePath);
            var ext = Path.GetExtension(oldFilePath);
            if (string.IsNullOrEmpty(ext)) continue;

            var proposedNewName = ApplyReplacement(oldName, change.Match, change.ReplacementText);

            // Existing names in the target extension scope, MINUS the file being renamed
            // (validator must not flag a no-op rename as a collision).
            var existingMinusSelf = existingResRefsByExt.TryGetValue(ext, out var set)
                ? new HashSet<string>(set, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            existingMinusSelf.Remove(oldName);

            var validation = validator.Validate(proposedNewName, existingMinusSelf, ext);

            if (!validation.IsValid)
            {
                // Skip invalid entries — caller may inspect plans count vs preview filename count
                // to surface a status message. Capture the specific reason for #2182.
                rejectedReasons?.Add($"\"{proposedNewName}\" — {validation.Error}");
                continue;
            }

            plans.Add(new ResRefRenamePlan
            {
                OldName = oldName,
                NewName = validation.NormalizedName,
                ResourceType = ResourceTypes.FromExtension(ext),
                Validation = validation,
                SourceFilePath = oldFilePath,
                TargetFilePath = Path.Combine(
                    Path.GetDirectoryName(oldFilePath)!,
                    $"{validation.NormalizedName}{ext}")
            });
        }

        return plans;
    }

    /// <summary>
    /// Index every file in the module directory by extension. Used as the existing-name
    /// set for collision detection during validation.
    /// </summary>
    public static Dictionary<string, HashSet<string>> BuildExistingResRefIndex(string moduleDir)
    {
        var byExt = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(moduleDir) || !Directory.Exists(moduleDir))
            return byExt;

        foreach (var path in Directory.EnumerateFiles(moduleDir))
        {
            var ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext)) continue;
            var name = Path.GetFileNameWithoutExtension(path);
            if (!byExt.TryGetValue(ext, out var set))
                byExt[ext] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            set.Add(name);
        }
        return byExt;
    }

    /// <summary>
    /// Apply a SearchMatch's offset/length-based replacement to a string.
    /// Used to compute proposed new ResRef names from filename matches.
    /// </summary>
    public static string ApplyReplacement(string oldName, SearchMatch match, string replacementText)
    {
        if (oldName == null) return string.Empty;
        if (match == null) return oldName;

        var offset = Math.Clamp(match.MatchOffset, 0, oldName.Length);
        var length = Math.Clamp(match.MatchLength, 0, oldName.Length - offset);

        return string.Concat(
            oldName.AsSpan(0, offset),
            replacementText ?? string.Empty,
            oldName.AsSpan(offset + length));
    }

    /// <summary>
    /// Walk files in the module directory, parse each one, and append matching
    /// references to each plan's References list.
    ///
    /// Honors the user's file-type filter (unchecked types are skipped) AND, when
    /// <paramref name="allowedFilePaths"/> is non-null, restricts the scan to ONLY
    /// those paths. This is the "surgical rename" path — reference updates are
    /// confined to files the user explicitly selected, leaving everything else
    /// untouched. When <paramref name="allowedFilePaths"/> is null, the scan
    /// covers every file in <paramref name="moduleDir"/> (legacy module-wide mode).
    /// </summary>
    public static async Task PopulateReferencesAsync(
        IReadOnlyList<ResRefRenamePlan> plans,
        string moduleDir,
        bool includeNss,
        SearchCriteria criteria,
        IReadOnlySet<string>? allowedFilePaths = null)
    {
        if (plans == null || plans.Count == 0) return;
        if (string.IsNullOrEmpty(moduleDir) || !Directory.Exists(moduleDir)) return;

        var refScanner = new ResRefReferenceScanner();
        var nssScanner = includeNss ? new NssReferenceScanner() : null;

        var fileTypeFilter = criteria?.FileTypeFilter;

        foreach (var path in Directory.EnumerateFiles(moduleDir))
        {
            // Surgical mode: skip any file outside the user's selection set.
            // The file being renamed itself doesn't carry a self-reference, so
            // excluding it from the scan is harmless when it's not in the set.
            if (allowedFilePaths != null && !allowedFilePaths.Contains(path))
                continue;

            var ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext)) continue;

            var resourceType = ResourceTypes.FromExtension(ext);
            if (resourceType == 0) continue;

            // Honor file-type filter — unchecked types skipped (scope-respecting per spec)
            if (fileTypeFilter != null && fileTypeFilter.Count > 0
                && !fileTypeFilter.Contains(resourceType))
                continue;

            if (string.Equals(ext, ".nss", StringComparison.OrdinalIgnoreCase))
            {
                if (nssScanner == null) continue;
                foreach (var plan in plans)
                {
                    var refs = nssScanner.Scan(path, plan.OldName);
                    foreach (var r in refs)
                        plan.References.Add(CloneReferenceWithNewValue(r, plan.NewName));
                }
                // also process plans synchronously; nothing async per file beyond this
                continue;
            }

            // GFF-style file: read once, then scan against every plan
            byte[] bytes;
            try
            {
                bytes = await File.ReadAllBytesAsync(path);
            }
            catch
            {
                continue;
            }

            GffFile gff;
            try
            {
                gff = GffReader.Read(bytes);
            }
            catch
            {
                continue;  // not a parseable GFF; skip
            }

            foreach (var plan in plans)
            {
                var refs = refScanner.Scan(gff, resourceType, plan.OldName, path);
                foreach (var r in refs)
                    plan.References.Add(CloneReferenceWithNewValue(r, plan.NewName));
            }
        }
    }

    /// <summary>
    /// Build a residual preview containing only the non-filename rows from the
    /// original preview. Used by the rename dispatch flow so that content matches
    /// (e.g. ITP Name field) that weren't consumed by the rename are still
    /// processed by the standard replace path afterward.
    ///
    /// <paramref name="renameMap"/> maps source file paths to their post-rename
    /// targets, used to remap any residual change whose FilePath refers to a file
    /// that was just renamed. Rows whose FilePath is not in the map are passed
    /// through unchanged.
    ///
    /// Preserves <see cref="BatchReplacePreview.AllowResRefReplace"/> and per-row
    /// <see cref="PendingChange.IsSelected"/> state. Does not include FileGroups
    /// (BatchReplaceService.ExecuteReplaceAsync iterates Changes, not FileGroups).
    /// </summary>
    public static BatchReplacePreview BuildResidualPreview(
        BatchReplacePreview preview,
        IReadOnlyDictionary<string, string> renameMap)
    {
        var residual = new BatchReplacePreview
        {
            AllowResRefReplace = preview?.AllowResRefReplace ?? false,
            PreserveCase = preview?.PreserveCase ?? false
        };
        if (preview == null) return residual;

        foreach (var change in preview.Changes)
        {
            if (change.Match.Field.GffPath == FilenameSearchProvider.FilenameField.GffPath)
                continue;  // filename rows are handled by the rename path

            var newPath = renameMap != null && renameMap.TryGetValue(change.FilePath, out var mapped)
                ? mapped
                : change.FilePath;

            residual.Changes.Add(new PendingChange
            {
                Match = change.Match,
                ReplacementText = change.ReplacementText,
                FilePath = newPath,
                IsSelected = change.IsSelected,
                PreserveCase = change.PreserveCase
            });
        }
        return residual;
    }

    /// <summary>
    /// ResRefReference is a class (not record) — duplicate explicit copy so we
    /// can attach the per-plan NewValue without mutating the scanner's outputs
    /// across multiple plans. See Chunk 1a Task 1a.16 for the design rationale.
    /// </summary>
    private static ResRefReference CloneReferenceWithNewValue(ResRefReference source, string newValue)
    {
        return new ResRefReference
        {
            FilePath = source.FilePath,
            ResourceType = source.ResourceType,
            Field = source.Field,
            Location = source.Location,
            OldValue = source.OldValue,
            NewValue = newValue,
            ScopeTier = source.ScopeTier,
            MatchOffset = source.MatchOffset,
            MatchLength = source.MatchLength,
            IsSelected = source.IsSelected
        };
    }
}
