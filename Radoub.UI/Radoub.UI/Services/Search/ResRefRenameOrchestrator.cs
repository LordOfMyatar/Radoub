using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Radoub.Formats.Search.Rename;

namespace Radoub.UI.Services.Search;

/// <summary>
/// Executes a ResRef rename: preflight, backup, references-first writes,
/// atomic rename, verify, rollback on failure.
/// See spec Section 7 (NonPublic/Trebuchet/2026-05-03-resref-rename-design.md).
/// </summary>
public class ResRefRenameOrchestrator
{
    private readonly BackupService _backupService;
    private readonly SearchProviderFactory _providerFactory;

    public ResRefRenameOrchestrator(BackupService backupService)
        : this(backupService, SearchProviderFactory.CreateDefault())
    {
    }

    public ResRefRenameOrchestrator(BackupService backupService, SearchProviderFactory providerFactory)
    {
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
    }

    /// <summary>
    /// Execute a set of ResRef rename plans on disk.
    /// Order: preflight (mtime+size) → backup → references first → rename last → verify → rollback on failure.
    /// </summary>
    public async Task<ResRefRenameResult> ExecuteAsync(
        IReadOnlyList<ResRefRenamePlan> plans,
        string moduleName,
        IReadOnlyList<FilePreflightSnapshot>? preflightSnapshots = null)
    {
        // Phase 1: Preflight (mtime+size check)
        if (preflightSnapshots != null)
        {
            foreach (var snap in preflightSnapshots)
            {
                if (!File.Exists(snap.FilePath))
                    return ResRefRenameResult.Fail($"Preflight failed: {snap.FilePath} no longer exists");

                var info = new FileInfo(snap.FilePath);
                if (info.LastWriteTimeUtc != snap.LastWriteTimeUtc || info.Length != snap.Length)
                    return ResRefRenameResult.Fail(
                        $"Preflight failed: {Path.GetFileName(snap.FilePath)} was modified between preview and execute");
            }
        }

        // Determine the work set
        var renameMap = new List<(string Old, string New)>();
        var filesToBackup = new HashSet<string>();
        foreach (var plan in plans.Where(p => p.IsSelected))
        {
            filesToBackup.Add(plan.SourceFilePath);
            foreach (var r in plan.SelectedReferences)
                filesToBackup.Add(r.FilePath);
            renameMap.Add((plan.SourceFilePath, plan.TargetFilePath));
        }

        // Reference-only files (when a plan is unticked but its references are ticked)
        foreach (var plan in plans.Where(p => !p.IsSelected))
        {
            foreach (var r in plan.SelectedReferences)
                filesToBackup.Add(r.FilePath);
        }

        // Phase 2: Backup
        var manifest = await _backupService.BackupFilesAsync(filesToBackup.ToList(), moduleName);

        int refsUpdated = 0;
        var completedRenames = new List<(string Old, string New)>();

        try
        {
            // Phase 3a: GFF reference updates (per file, batched across all plans)
            // We must collect references across all plans by file, since one file
            // could carry references to multiple renamed ResRefs.
            var gffRefsByFile = new Dictionary<string, List<(ResRefReference Ref, string NewName)>>(StringComparer.OrdinalIgnoreCase);
            foreach (var plan in plans)
            {
                foreach (var r in plan.SelectedReferences
                    .Where(r => r.ScopeTier == ResRefScopeTier.TypedGffField
                             || r.ScopeTier == ResRefScopeTier.DlgScriptParam))
                {
                    if (!gffRefsByFile.TryGetValue(r.FilePath, out var list))
                    {
                        list = new List<(ResRefReference, string)>();
                        gffRefsByFile[r.FilePath] = list;
                    }
                    list.Add((r, plan.NewName));
                }
            }

            foreach (var kvp in gffRefsByFile)
            {
                var gff = GffReader.Read(File.ReadAllBytes(kvp.Key));
                foreach (var (refRow, newName) in kvp.Value)
                {
                    if (ApplyGffReferenceUpdate(gff, refRow, newName))
                        refsUpdated++;
                }
                await WriteAtomicAsync(kvp.Key, GffWriter.Write(gff));
            }

            // Phase 3b: NSS text replacements (per file, batched across plans)
            var nssRefsByFile = new Dictionary<string, List<(ResRefReference Ref, string OldName, string NewName)>>(StringComparer.OrdinalIgnoreCase);
            foreach (var plan in plans)
            {
                foreach (var r in plan.SelectedReferences
                    .Where(r => r.ScopeTier == ResRefScopeTier.NssQuotedString
                             || r.ScopeTier == ResRefScopeTier.NssBareSubstring))
                {
                    if (!nssRefsByFile.TryGetValue(r.FilePath, out var list))
                    {
                        list = new List<(ResRefReference, string, string)>();
                        nssRefsByFile[r.FilePath] = list;
                    }
                    list.Add((r, plan.OldName, plan.NewName));
                }
            }

            foreach (var kvp in nssRefsByFile)
            {
                var applied = await ApplyNssReferenceUpdates(kvp.Key, kvp.Value);
                refsUpdated += applied;
            }

            // Phase 3c: Rename files (after references are updated)
            foreach (var (oldPath, newPath) in renameMap)
            {
                File.Move(oldPath, newPath);
                completedRenames.Add((oldPath, newPath));
            }

            // Phase 4: Verify
            foreach (var (oldPath, newPath) in renameMap)
            {
                if (!File.Exists(newPath))
                    throw new IOException($"Verify failed: expected file at {newPath} after rename");
                if (File.Exists(oldPath))
                    throw new IOException($"Verify failed: orphan file remains at {oldPath} after rename");
            }

            return ResRefRenameResult.Ok(renameMap.Count, refsUpdated, manifest);
        }
        catch (Exception ex)
        {
            // Rollback path: reverse renames first, then restore content from backup.
            var rollbackOk = true;

            foreach (var (oldPath, newPath) in completedRenames)
            {
                try
                {
                    if (File.Exists(newPath) && !File.Exists(oldPath))
                        File.Move(newPath, oldPath);
                }
                catch
                {
                    rollbackOk = false;
                }
            }

            var restoreOk = await _backupService.RestoreAsync(manifest);
            rollbackOk = rollbackOk && restoreOk;

            return new ResRefRenameResult
            {
                Success = false,
                Error = ex.Message,
                BackupManifest = manifest,
                RollbackAttempted = true,
                RollbackSucceeded = rollbackOk
            };
        }
    }

    /// <summary>
    /// Apply a single GFF reference update. Returns true if the update was applied,
    /// false if the location string did not parse to a known location format.
    /// Currently handles: top-level scalar fields and the "Creature List > Item N > FieldName" GIT pattern.
    /// Other nested locations (UTC Equip_ItemList, UTM StoreList[], DLG ActionParams, etc.) will be added in Chunk 5.
    /// </summary>
    private static bool ApplyGffReferenceUpdate(GffFile gff, ResRefReference refRow, string newValue)
    {
        var loc = refRow.Location ?? string.Empty;

        // Pattern: "Creature List > Item N > FieldName"
        if (loc.StartsWith("Creature List > Item ", StringComparison.Ordinal))
        {
            var parts = loc.Split(" > ");
            if (parts.Length < 3) return false;
            var idxText = parts[1].Substring("Item ".Length);
            if (!int.TryParse(idxText, out var itemIdx)) return false;
            var fieldName = parts[2];

            var listField = gff.RootStruct.GetField("Creature List");
            if (listField?.Value is GffList list && itemIdx < list.Elements.Count)
            {
                var f = list.Elements[itemIdx].GetField(fieldName);
                if (f != null)
                {
                    f.Value = newValue;
                    return true;
                }
            }
            return false;
        }

        // Top-level field: use Field.GffPath when present, otherwise the Location string itself.
        var topFieldName = refRow.Field?.GffPath ?? loc;
        if (string.IsNullOrEmpty(topFieldName)) return false;

        var topField = gff.RootStruct.GetField(topFieldName);
        if (topField != null)
        {
            topField.Value = newValue;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Applies NSS reference updates and returns the count of references actually applied.
    /// Some refs may be skipped if the file content drifted between scan and execute
    /// (preflight is mtime+size-based, so a same-size edit slips through).
    /// </summary>
    private static async Task<int> ApplyNssReferenceUpdates(
        string nssPath,
        IReadOnlyList<(ResRefReference Ref, string OldName, string NewName)> refs)
    {
        var source = await File.ReadAllTextAsync(nssPath);
        int applied = 0;

        // Apply replacements in REVERSE OFFSET ORDER so earlier offsets remain valid.
        var sorted = refs.OrderByDescending(r => r.Ref.MatchOffset).ToList();

        var sb = new System.Text.StringBuilder(source);
        foreach (var (refRow, oldName, newName) in sorted)
        {
            if (refRow.MatchOffset < 0 || refRow.MatchOffset + refRow.MatchLength > sb.Length)
                continue;

            var actual = sb.ToString(refRow.MatchOffset, refRow.MatchLength);
            if (!string.Equals(actual, refRow.OldValue, StringComparison.OrdinalIgnoreCase))
            {
                // File content drifted (preflight is mtime+size, not byte-identity). Skip this row.
                continue;
            }

            sb.Remove(refRow.MatchOffset, refRow.MatchLength);
            sb.Insert(refRow.MatchOffset, newName);
            applied++;
        }

        var tempPath = nssPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, sb.ToString());
        File.Move(tempPath, nssPath, overwrite: true);
        return applied;
    }

    /// <summary>
    /// Atomic write via temp-file + rename. Survives mid-write process kill —
    /// either the original is intact or the new content is fully written and named.
    /// </summary>
    private static async Task WriteAtomicAsync(string path, byte[] bytes)
    {
        var tempPath = path + ".tmp";
        await File.WriteAllBytesAsync(tempPath, bytes);
        File.Move(tempPath, path, overwrite: true);
    }

    /// <summary>
    /// Capture file mtime + size for a set of paths.
    /// Pass to ExecuteAsync as preflightSnapshots to detect concurrent modification.
    /// </summary>
    public static IReadOnlyList<FilePreflightSnapshot> CaptureSnapshots(IEnumerable<string> filePaths)
    {
        var snapshots = new List<FilePreflightSnapshot>();
        foreach (var path in filePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path)) continue;
            var info = new FileInfo(path);
            snapshots.Add(new FilePreflightSnapshot(path, info.LastWriteTimeUtc, info.Length));
        }
        return snapshots;
    }
}

/// <summary>
/// Snapshot of file metadata captured at preview time. The orchestrator
/// re-reads metadata at preflight and aborts if either differs.
/// </summary>
public record FilePreflightSnapshot(string FilePath, DateTime LastWriteTimeUtc, long Length);
