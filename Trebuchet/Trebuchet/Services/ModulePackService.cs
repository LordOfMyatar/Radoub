using System;
using System.Collections.Generic;
using System.IO;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Logging;
using Radoub.UI.Services.Search;

namespace RadoubLauncher.Services;

/// <summary>
/// Packs a working directory back into a .mod (or .erf) archive with safe-write
/// guarantees:
///   1. Pre-read every source file into memory before any write begins — a
///      locked/unreadable input fails fast and leaves the prior .mod intact.
///   2. Snapshot the prior .mod to ~/Radoub/Backups/&lt;modname&gt;/&lt;timestamp&gt;/
///      before touching it (issue #2246).
///   3. Write to a sibling .tmp then atomic File.Replace, so a crash mid-write
///      cannot destroy the user's only .mod copy.
/// </summary>
public static class ModulePackService
{
    /// <summary>
    /// Pack a working directory into a .mod file.
    /// </summary>
    /// <param name="workingDir">Directory holding the unpacked module resources.</param>
    /// <param name="modFilePath">Destination .mod (or .erf) path.</param>
    /// <param name="backupRoot">
    /// Optional override for backup root (tests). Default is ~/Radoub/Backups/.
    /// </param>
    /// <returns>Resource count written.</returns>
    public static int PackDirectoryToMod(string workingDir, string modFilePath, string? backupRoot = null)
    {
        if (string.IsNullOrEmpty(workingDir)) throw new ArgumentException("workingDir required", nameof(workingDir));
        if (string.IsNullOrEmpty(modFilePath)) throw new ArgumentException("modFilePath required", nameof(modFilePath));

        var files = Directory.GetFiles(workingDir);
        var resourceData = new Dictionary<(string ResRef, ushort Type), byte[]>();
        var resources = new List<ErfResourceEntry>();

        // Phase 1 — pre-read every file. A read failure here throws BEFORE any
        // destructive write, so the prior .mod stays intact. Issue #2246.
        foreach (var filePath in files)
        {
            var extension = Path.GetExtension(filePath);
            var resourceType = ResourceTypes.FromExtension(extension);
            if (resourceType == ResourceTypes.Invalid)
                continue;

            var resRef = Path.GetFileNameWithoutExtension(filePath);
            var data = File.ReadAllBytes(filePath);
            var key = (resRef.ToLowerInvariant(), resourceType);

            resourceData[key] = data;
            resources.Add(new ErfResourceEntry
            {
                ResRef = resRef,
                ResourceType = resourceType,
                ResId = (uint)resources.Count
            });
        }

        var erf = new ErfFile
        {
            FileType = "MOD ",
            FileVersion = "V1.0",
            BuildYear = (uint)(DateTime.Now.Year - 1900),
            BuildDay = (uint)DateTime.Now.DayOfYear
        };
        erf.Resources.AddRange(resources);

        // Phase 2 — snapshot prior .mod if one exists.
        if (File.Exists(modFilePath))
        {
            try
            {
                var moduleName = Path.GetFileNameWithoutExtension(modFilePath);
                var backupService = new BackupService(backupRoot);
                // BackupService is async-only; block here because the pack
                // pipeline is already running on a Task.Run worker.
                backupService.BackupArchiveAsync(modFilePath, moduleName).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                // Backup failure must NOT abort the pack — but log loudly.
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Pre-pack backup failed for {UnifiedLogger.SanitizePath(modFilePath)}: {ex.Message}");
            }
        }

        // Phase 3 — write via temp + atomic replace.
        var tempPath = modFilePath + ".tmp";
        try
        {
            ErfWriter.Write(erf, tempPath, resourceData);

            if (File.Exists(modFilePath))
            {
                // overwrite:true required because target exists.
                File.Move(tempPath, modFilePath, overwrite: true);
            }
            else
            {
                File.Move(tempPath, modFilePath);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* swallow cleanup failure */ }
            }
        }

        return resources.Count;
    }
}
