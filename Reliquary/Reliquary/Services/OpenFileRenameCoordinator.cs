using System;
using System.Threading.Tasks;

namespace PlaceableEditor.Services;

/// <summary>
/// Pure orchestration for renaming the currently-open placeable (#2424). The F4 browser validates
/// the new name and raises FileRenameRequested; the host owns the rename because it holds the
/// in-memory model (and any session lock) the panel can't see. This coordinator sequences the steps
/// — save the pending edits, release the lock, move the file on disk, reopen from the new path — via
/// injected callbacks so the ordering and guards are unit-testable without a window. Mirrors
/// Relique's RenameOpenFileAsync; siblings share this single-resource pattern.
/// </summary>
public static class OpenFileRenameCoordinator
{
    public enum Result
    {
        Renamed,
        NotCurrentFile,
        MoveFailed,
    }

    /// <summary>Host-supplied steps. Move returns false on a failed disk move so reopen is skipped.</summary>
    public sealed class Callbacks
    {
        public required Func<Task<bool>> SaveAsync { get; init; }
        public required Action ReleaseLock { get; init; }
        public required Func<string, string, bool> Move { get; init; }
        public required Func<string, Task> ReopenAsync { get; init; }
    }

    /// <summary>
    /// Save (if dirty) → release lock → move → reopen. No-ops with <see cref="Result.NotCurrentFile"/>
    /// if the open file changed underneath us (selection moved between prompt and handler).
    /// </summary>
    public static async Task<Result> RenameAsync(
        string oldPath, string newPath, string? currentFilePath, bool isDirty, Callbacks callbacks)
    {
        if (string.IsNullOrEmpty(currentFilePath)
            || !string.Equals(currentFilePath, oldPath, StringComparison.OrdinalIgnoreCase))
        {
            return Result.NotCurrentFile;
        }

        if (isDirty)
            await callbacks.SaveAsync();

        // Release the lock before moving; reopen reacquires on the new path.
        callbacks.ReleaseLock();

        if (!callbacks.Move(oldPath, newPath))
            return Result.MoveFailed;

        await callbacks.ReopenAsync(newPath);
        return Result.Renamed;
    }
}
