using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Radoub.Formats.Gff;
using Radoub.Formats.Logging;
using Radoub.UI.Services.Search;

namespace RadoubLauncher.Services;

/// <summary>
/// Scans area .git files for creature and encounter faction references.
/// Used by FactionEditorViewModel to reindex FactionIDs when factions are deleted.
/// </summary>
public static class AreaScanService
{
    /// <summary>
    /// Finds all .git files in a module working directory.
    /// </summary>
    public static string[] FindGitFiles(string? workingDirectory)
    {
        if (string.IsNullOrEmpty(workingDirectory) || !Directory.Exists(workingDirectory))
            return Array.Empty<string>();

        return Directory.GetFiles(workingDirectory, "*.git");
    }

    /// <summary>
    /// Scans all area .git files and returns faction references found in creatures and encounters.
    /// </summary>
    public static List<AreaFactionRefs> ScanFactionReferences(string workingDirectory)
    {
        var results = new List<AreaFactionRefs>();
        var gitFiles = FindGitFiles(workingDirectory);

        foreach (var filePath in gitFiles)
        {
            try
            {
                var gff = GffReader.Read(filePath);
                var refs = new AreaFactionRefs
                {
                    FilePath = filePath,
                    AreaName = Path.GetFileNameWithoutExtension(filePath)
                };

                // Scan Creature List
                var creatureListField = gff.RootStruct.GetField("Creature List");
                if (creatureListField?.Value is GffList creatureList)
                {
                    foreach (var creature in creatureList.Elements)
                    {
                        var factionId = creature.GetFieldValue<uint>("FactionID", 0);
                        refs.CreatureFactionIds.Add(factionId);
                    }
                }

                // Scan Encounter List
                var encounterListField = gff.RootStruct.GetField("Encounter List");
                if (encounterListField?.Value is GffList encounterList)
                {
                    foreach (var encounter in encounterList.Elements)
                    {
                        var faction = encounter.GetFieldValue<uint>("Faction", 0);
                        refs.EncounterFactionIds.Add(faction);
                    }
                }

                results.Add(refs);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Failed to scan area file {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        return results;
    }

    /// <summary>
    /// Checks if any creature or encounter in the working directory references the given faction index.
    /// </summary>
    public static bool HasFactionReferences(string? workingDirectory, uint factionIndex)
    {
        if (string.IsNullOrEmpty(workingDirectory))
            return false;

        var gitFiles = FindGitFiles(workingDirectory);
        foreach (var filePath in gitFiles)
        {
            try
            {
                var gff = GffReader.Read(filePath);

                var creatureListField = gff.RootStruct.GetField("Creature List");
                if (creatureListField?.Value is GffList creatureList)
                {
                    foreach (var creature in creatureList.Elements)
                    {
                        if (creature.GetFieldValue<uint>("FactionID", uint.MaxValue) == factionIndex)
                            return true;
                    }
                }

                var encounterListField = gff.RootStruct.GetField("Encounter List");
                if (encounterListField?.Value is GffList encounterList)
                {
                    foreach (var encounter in encounterList.Elements)
                    {
                        if (encounter.GetFieldValue<uint>("Faction", uint.MaxValue) == factionIndex)
                            return true;
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Failed to scan area file {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        return false;
    }

    /// <summary>
    /// Finds all .utc files in a module working directory.
    /// </summary>
    public static string[] FindUtcFiles(string? workingDirectory)
    {
        if (string.IsNullOrEmpty(workingDirectory) || !Directory.Exists(workingDirectory))
            return Array.Empty<string>();

        return Directory.GetFiles(workingDirectory, "*.utc");
    }

    /// <summary>
    /// Reindexes creature and encounter FactionIDs after a faction is deleted.
    /// Scans .git files (area instances) and .utc files (blueprints).
    /// Creatures/encounters on the deleted faction are reassigned to the parent faction.
    /// Creatures/encounters above the deleted index are decremented.
    /// </summary>
    /// <param name="workingDirectory">Module working directory containing .git/.utc files</param>
    /// <param name="deletedIndex">Index of the faction being deleted</param>
    /// <param name="parentFactionId">FactionParentID of the deleted faction (for reassignment)</param>
    /// <param name="backupRoot">
    /// Optional override for backup root (tests). Default is ~/Radoub/Backups/.
    /// Backups snapshot every modified .git/.utc file before mutation (#2246).
    /// </param>
    /// <returns>Summary of changes made</returns>
    public static ReindexResult ReindexFactions(string? workingDirectory, uint deletedIndex, uint parentFactionId, string? backupRoot = null)
    {
        var result = new ReindexResult();

        if (string.IsNullOrEmpty(workingDirectory))
            return result;

        var backupService = new BackupService(backupRoot);
        var moduleName = Path.GetFileName(workingDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(moduleName)) moduleName = "module";

        // Determine the effective reassignment target.
        // If parent is also above deleted index, it needs decrementing too.
        // If parent is 0xFFFFFFFF (no parent), fall back to Commoner (2) — safe neutral faction.
        // PC (0) is not a viable faction for NPCs. Hostile (1) causes unintended aggression.
        uint reassignTarget;
        if (parentFactionId == 0xFFFFFFFF)
        {
            reassignTarget = 2; // Commoner — neutral, non-hostile fallback
        }
        else if (parentFactionId > deletedIndex)
        {
            reassignTarget = parentFactionId - 1;
        }
        else
        {
            reassignTarget = parentFactionId;
        }

        // Reindex .git files (area creature/encounter instances)
        ReindexGitFiles(workingDirectory, deletedIndex, reassignTarget, result, backupService, moduleName);

        // Reindex .utc files (creature blueprints)
        ReindexUtcFiles(workingDirectory, deletedIndex, reassignTarget, result, backupService, moduleName);

        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"Faction reindex complete: {result.FilesScanned} files scanned, {result.FilesModified} modified, " +
            $"{result.CreaturesReindexed} creatures + {result.EncountersReindexed} encounters + " +
            $"{result.BlueprintsReindexed} blueprints reindexed");

        return result;
    }

    private static void ReindexGitFiles(string workingDirectory, uint deletedIndex,
        uint reassignTarget, ReindexResult result, BackupService backupService, string moduleName)
    {
        var gitFiles = FindGitFiles(workingDirectory);
        result.FilesScanned += gitFiles.Length;

        foreach (var filePath in gitFiles)
        {
            try
            {
                var gff = GffReader.Read(filePath);
                bool fileModified = false;

                // Reindex Creature List — FactionID is WORD (ushort) in .git files
                var creatureListField = gff.RootStruct.GetField("Creature List");
                if (creatureListField?.Value is GffList creatureList)
                {
                    foreach (var creature in creatureList.Elements)
                    {
                        var field = creature.GetField("FactionID");
                        if (field == null) continue;

                        var factionId = creature.GetFieldValue<uint>("FactionID", 0);

                        if (factionId == deletedIndex)
                        {
                            SetFieldValuePreservingType(field, reassignTarget);
                            result.CreaturesReindexed++;
                            fileModified = true;
                        }
                        else if (factionId > deletedIndex)
                        {
                            SetFieldValuePreservingType(field, factionId - 1);
                            fileModified = true;
                        }
                    }
                }

                // Reindex Encounter List — Faction is DWORD (uint) in .git files
                var encounterListField = gff.RootStruct.GetField("Encounter List");
                if (encounterListField?.Value is GffList encounterList)
                {
                    foreach (var encounter in encounterList.Elements)
                    {
                        var field = encounter.GetField("Faction");
                        if (field == null) continue;

                        var faction = encounter.GetFieldValue<uint>("Faction", 0);

                        if (faction == deletedIndex)
                        {
                            SetFieldValuePreservingType(field, reassignTarget);
                            result.EncountersReindexed++;
                            fileModified = true;
                        }
                        else if (faction > deletedIndex)
                        {
                            SetFieldValuePreservingType(field, faction - 1);
                            fileModified = true;
                        }
                    }
                }

                if (fileModified)
                {
                    SafeWriteGff(gff, filePath, backupService, moduleName);
                    result.FilesModified++;
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"Reindexed factions in {Path.GetFileName(filePath)}");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"Failed to reindex factions in {Path.GetFileName(filePath)}: {ex.Message}");
                result.Errors.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
            }
        }
    }

    private static void ReindexUtcFiles(string workingDirectory, uint deletedIndex,
        uint reassignTarget, ReindexResult result, BackupService backupService, string moduleName)
    {
        var utcFiles = FindUtcFiles(workingDirectory);
        result.FilesScanned += utcFiles.Length;

        foreach (var filePath in utcFiles)
        {
            try
            {
                var gff = GffReader.Read(filePath);
                var field = gff.RootStruct.GetField("FactionID");
                if (field == null) continue;

                var factionId = gff.RootStruct.GetFieldValue<uint>("FactionID", 0);
                bool modified = false;

                if (factionId == deletedIndex)
                {
                    SetFieldValuePreservingType(field, reassignTarget);
                    result.BlueprintsReindexed++;
                    modified = true;
                }
                else if (factionId > deletedIndex)
                {
                    SetFieldValuePreservingType(field, factionId - 1);
                    modified = true;
                }

                if (modified)
                {
                    SafeWriteGff(gff, filePath, backupService, moduleName);
                    result.FilesModified++;
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"Reindexed faction in blueprint {Path.GetFileName(filePath)}");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"Failed to reindex faction in {Path.GetFileName(filePath)}: {ex.Message}");
                result.Errors.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Snapshot the existing file via BackupService, then write the modified GFF
    /// to a temp sibling and atomically rename. A crash between Phase 1 (backup)
    /// and Phase 3 (replace) leaves the original on disk; recovery is from the
    /// snapshot. Issue #2246.
    /// </summary>
    private static void SafeWriteGff(GffFile gff, string filePath, BackupService backupService, string moduleName)
    {
        // Snapshot the prior file before we touch it. Wrap in Task.Run because
        // BackupService.BackupArchiveAsync awaits without ConfigureAwait(false)
        // and ReindexFactions is called on the UI thread from
        // FactionEditorViewModel — a direct .GetAwaiter().GetResult() deadlocks
        // when the continuation tries to resume on the captured sync context.
        try
        {
            System.Threading.Tasks.Task.Run(() =>
                backupService.BackupArchiveAsync(filePath, moduleName)).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // Log loudly but do not abort — the atomic write below still
            // protects against mid-write corruption.
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Pre-reindex backup failed for {Path.GetFileName(filePath)}: {ex.Message}");
        }

        var tempPath = filePath + ".tmp";
        try
        {
            GffWriter.Write(gff, tempPath);
            File.Move(tempPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* swallow cleanup failure */ }
            }
        }
    }

    /// <summary>
    /// Sets a GFF field value while preserving its original type.
    /// Critical: WORD fields need ushort, DWORD fields need uint, etc.
    /// Without this, GffWriter.EncodeSimpleValue silently writes 0.
    /// </summary>
    private static void SetFieldValuePreservingType(GffField field, uint newValue)
    {
        field.Value = field.Type switch
        {
            GffField.BYTE => (object)(byte)newValue,
            GffField.WORD => (object)(ushort)newValue,
            GffField.SHORT => (object)(short)newValue,
            GffField.DWORD => (object)newValue,
            GffField.INT => (object)(int)newValue,
            _ => (object)newValue
        };
    }
}

/// <summary>
/// Faction references found in a single area .git file.
/// </summary>
public class AreaFactionRefs
{
    public string FilePath { get; set; } = string.Empty;
    public string AreaName { get; set; } = string.Empty;
    public List<uint> CreatureFactionIds { get; set; } = new();
    public List<uint> EncounterFactionIds { get; set; } = new();

    /// <summary>
    /// True if this area has any creature or encounter referencing the given faction.
    /// </summary>
    public bool ReferencesFaction(uint factionIndex)
    {
        return CreatureFactionIds.Contains(factionIndex) ||
               EncounterFactionIds.Contains(factionIndex);
    }
}

/// <summary>
/// Result of a faction reindex operation across area .git files.
/// </summary>
public class ReindexResult
{
    public int FilesScanned { get; set; }
    public int FilesModified { get; set; }
    public int CreaturesReindexed { get; set; }
    public int EncountersReindexed { get; set; }
    public int BlueprintsReindexed { get; set; }
    public List<string> Errors { get; set; } = new();

    public bool HasErrors => Errors.Count > 0;
    public int TotalReindexed => CreaturesReindexed + EncountersReindexed + BlueprintsReindexed;
}
