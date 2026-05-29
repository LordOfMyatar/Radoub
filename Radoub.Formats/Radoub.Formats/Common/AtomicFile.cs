using System;
using System.IO;
using Radoub.Formats.Logging;

namespace Radoub.Formats.Common;

/// <summary>
/// Cross-OS atomic file-replace helper (#2256).
///
/// Consolidates the "write to temp, then swap into place" pattern that tools were
/// re-implementing inline (Fence, ErfWriter, ...). The previous inline form —
/// File.Delete(dest) then File.Move(temp, dest) — opens a window where the
/// destination is gone if the process dies or the move fails between the two calls.
///
/// This helper performs the swap with a single File.Move(temp, dest, overwrite:true),
/// which is the correct cross-platform atomic primitive:
///   - Windows: maps to MoveFileEx with MOVEFILE_REPLACE_EXISTING — atomic on NTFS.
///   - Linux/macOS: maps to rename(2) — atomic POSIX rename.
/// In both cases the rename is atomic ONLY when source and destination are on the
/// same volume/filesystem. Callers MUST write the temp file beside the destination
/// (e.g. dest + ".tmp"), never in the OS temp directory, or the move degrades to a
/// non-atomic copy+delete across volumes.
///
/// File.Replace was deliberately NOT used: on Windows it gives a backup token and
/// preserves ACLs, but on .NET for Unix it has weaker/edge-case behavior (throws if
/// the destination is missing, cross-device quirks). File.Move(overwrite:true) behaves
/// uniformly on every OS, matching the precedent set in ErfWriter (#2244).
/// </summary>
public static class AtomicFile
{
    /// <summary>
    /// Atomically replaces <paramref name="destinationPath"/> with the contents of
    /// <paramref name="sourcePath"/> (typically a freshly-written temp file).
    /// </summary>
    /// <param name="sourcePath">
    /// The fully-written replacement file. Consumed (moved) by this call. Should be on
    /// the same volume as <paramref name="destinationPath"/> for the move to be atomic.
    /// </param>
    /// <param name="destinationPath">The final path to replace (or create if absent).</param>
    /// <param name="backupPath">
    /// Optional path to copy the previous destination contents to before replacing.
    /// If the destination does not exist, no backup is created. Any existing file at
    /// this path is overwritten. Backup failures are logged but do not abort the replace.
    /// </param>
    /// <exception cref="ArgumentException">A path argument is null or empty.</exception>
    /// <exception cref="FileNotFoundException">The source file does not exist.</exception>
    public static void Replace(string sourcePath, string destinationPath, string? backupPath)
    {
        if (string.IsNullOrEmpty(sourcePath))
            throw new ArgumentException("Source path is required.", nameof(sourcePath));
        if (string.IsNullOrEmpty(destinationPath))
            throw new ArgumentException("Destination path is required.", nameof(destinationPath));
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Atomic replace source not found.", sourcePath);

        // Back up the existing destination before we overwrite it. Best-effort: a
        // backup failure should not prevent the (still-atomic) replace from happening.
        if (!string.IsNullOrEmpty(backupPath) && File.Exists(destinationPath))
        {
            try
            {
                File.Copy(destinationPath, backupPath, overwrite: true);
            }
            catch (IOException ex)
            {
                UnifiedLogger.Log(LogLevel.WARN,
                    $"Backup before atomic replace failed: {ex.Message}", "AtomicFile", "Common");
            }
            catch (UnauthorizedAccessException ex)
            {
                UnifiedLogger.Log(LogLevel.WARN,
                    $"Backup before atomic replace denied: {ex.Message}", "AtomicFile", "Common");
            }
        }

        // Single atomic swap. overwrite:true handles both the replace-existing and
        // create-new cases (File.Move(overwrite:true) succeeds when dest is absent).
        // The original survives in place until the rename completes — no window where
        // the destination is missing (#2256).
        File.Move(sourcePath, destinationPath, overwrite: true);
    }
}
