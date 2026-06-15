using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Radoub.Formats.Logging;

namespace Radoub.UI.Services.Palette;

/// <summary>
/// One file in a palette save: where it goes, how to produce its bytes, and how to
/// validate that those bytes re-read correctly. <see cref="Validate"/> receives the
/// produced bytes and returns true when they are acceptable (e.g. they re-parse to a
/// structurally identical model). A validator that throws counts as a failure.
/// </summary>
/// <param name="Path">Destination path of the original file to replace.</param>
/// <param name="ProduceBytes">Serializer producing the new file contents.</param>
/// <param name="Validate">Re-read guard run against the produced bytes before commit.</param>
public readonly record struct PaletteFileWrite(
    string Path,
    Func<byte[]> ProduceBytes,
    Func<byte[], bool> Validate);

/// <summary>Outcome of a <see cref="PaletteSaveTransaction.Commit"/>.</summary>
/// <param name="Success">True when every file was written; false means nothing was changed.</param>
/// <param name="Error">The failure (serialize, validate, or replace) when <see cref="Success"/> is false.</param>
public readonly record struct PaletteSaveResult(bool Success, Exception? Error);

/// <summary>
/// Atomic N-file save transaction for the palette editor (#2476). A single recategorize
/// touches the <c>.itp</c> plus one blueprint, but a category move or delete-with-reparent
/// can touch many blueprints at once — so the unit of save is the <c>.itp</c> plus
/// <em>every</em> touched blueprint, committed all-or-nothing.
///
/// Sequence (the highest-corruption-risk path in the tool, hence dedicated rollback tests):
/// <list type="number">
/// <item>Produce bytes for every write and stage each to a sibling <c>*.tmp</c> file.</item>
/// <item>Validate each staged file's bytes via its re-read guard.</item>
/// <item>Only if all staged + validated: atomically replace each original from its temp,
///       keeping per-file backups so a mid-replace failure rolls every original back.</item>
/// </list>
/// Any failure at any stage aborts the whole commit: no original is modified and every
/// temp file is cleaned up.
/// </summary>
public static class PaletteSaveTransaction
{
    public static PaletteSaveResult Commit(IReadOnlyList<PaletteFileWrite> writes)
    {
        if (writes == null) throw new ArgumentNullException(nameof(writes));
        if (writes.Count == 0) return new PaletteSaveResult(true, null);

        var staged = new List<(PaletteFileWrite Write, string Temp, byte[] Bytes)>();
        try
        {
            // Stage 1: produce + validate + write every temp before touching any original.
            foreach (var w in writes)
            {
                byte[] bytes = w.ProduceBytes();
                if (!w.Validate(bytes))
                    throw new InvalidDataException(
                        $"Re-read validation rejected staged bytes for '{Path.GetFileName(w.Path)}'.");

                string temp = w.Path + ".tmp";
                File.WriteAllBytes(temp, bytes);
                staged.Add((w, temp, bytes));
            }

            // Stage 2: all staged and valid — atomically replace each original.
            CommitStaged(staged);
            return new PaletteSaveResult(true, null);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Palette save aborted (all-or-nothing): {ex.Message}");
            CleanupTemps(staged);
            return new PaletteSaveResult(false, ex);
        }
    }

    /// <summary>
    /// Replace every original from its temp. Each replaced original is backed up first so that
    /// if a later replacement throws, all earlier ones are restored — preserving all-or-nothing
    /// even partway through the commit stage.
    /// </summary>
    private static void CommitStaged(List<(PaletteFileWrite Write, string Temp, byte[] Bytes)> staged)
    {
        var done = new List<(string Path, string? Backup, bool Created)>();
        var backups = new List<string>(); // every .bak created, cleaned up on both paths
        try
        {
            foreach (var (w, temp, _) in staged)
            {
                bool existed = File.Exists(w.Path);
                string? backup = null;
                if (existed)
                {
                    backup = w.Path + ".bak";
                    File.Copy(w.Path, backup, overwrite: true);
                    backups.Add(backup); // record before the risky replace so cleanup always sees it
                }

                File.Delete(w.Path); // safe: backup taken if it existed
                File.Move(temp, w.Path);
                done.Add((w.Path, backup, !existed));
            }

            // Success: drop every backup.
            foreach (var backup in backups) TryDelete(backup);
        }
        catch
        {
            // Roll back everything already replaced in this stage.
            foreach (var (path, backup, created) in done)
            {
                try
                {
                    if (created)
                    {
                        TryDelete(path); // file did not exist before — remove it
                    }
                    else if (backup != null)
                    {
                        File.Delete(path);
                        File.Copy(backup, path, overwrite: true);
                    }
                }
                catch (Exception restoreEx)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"Palette save rollback could not restore '{Path.GetFileName(path)}': {restoreEx.Message}");
                }
            }
            // Drop every backup taken this commit, including the one for the file whose replace threw.
            foreach (var backup in backups) TryDelete(backup);
            throw;
        }
    }

    private static void CleanupTemps(IEnumerable<(PaletteFileWrite Write, string Temp, byte[] Bytes)> staged)
    {
        foreach (var (_, temp, _) in staged)
            TryDelete(temp);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Palette save temp cleanup failed for '{Path.GetFileName(path)}': {ex.Message}");
        }
    }
}
