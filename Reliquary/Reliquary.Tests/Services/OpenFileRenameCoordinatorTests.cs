using System.Collections.Generic;
using System.Threading.Tasks;
using PlaceableEditor.Services;

namespace PlaceableEditor.Tests.Services;

/// <summary>
/// Unit coverage for the open-file rename orchestration (#2424). The coordinator sequences
/// save → release-lock → move → reopen for the currently-open placeable without any UI, so the
/// ordering and guard logic can be verified without FlaUI.
/// </summary>
public class OpenFileRenameCoordinatorTests
{
    private static OpenFileRenameCoordinator.Callbacks RecordingCallbacks(List<string> log, bool moveOk = true) => new()
    {
        SaveAsync = () => { log.Add("save"); return Task.FromResult(true); },
        ReleaseLock = () => log.Add("release"),
        Move = (_, _) => { log.Add("move"); return moveOk; },
        ReopenAsync = _ => { log.Add("reopen"); return Task.CompletedTask; },
    };

    [Fact]
    public async Task RenameAsync_DirtyOpenFile_SavesReleasesMovesReopensInOrder()
    {
        var log = new List<string>();
        var result = await OpenFileRenameCoordinator.RenameAsync(
            oldPath: "old.utp", newPath: "new.utp",
            currentFilePath: "old.utp", isDirty: true,
            RecordingCallbacks(log));

        Assert.Equal(OpenFileRenameCoordinator.Result.Renamed, result);
        Assert.Equal(new[] { "save", "release", "move", "reopen" }, log);
    }

    [Fact]
    public async Task RenameAsync_CleanOpenFile_SkipsSave()
    {
        var log = new List<string>();
        var result = await OpenFileRenameCoordinator.RenameAsync(
            "old.utp", "new.utp", currentFilePath: "old.utp", isDirty: false,
            RecordingCallbacks(log));

        Assert.Equal(OpenFileRenameCoordinator.Result.Renamed, result);
        Assert.Equal(new[] { "release", "move", "reopen" }, log);
    }

    [Fact]
    public async Task RenameAsync_SelectionChangedUnderneath_DoesNothing()
    {
        var log = new List<string>();
        var result = await OpenFileRenameCoordinator.RenameAsync(
            "old.utp", "new.utp", currentFilePath: "different.utp", isDirty: true,
            RecordingCallbacks(log));

        Assert.Equal(OpenFileRenameCoordinator.Result.NotCurrentFile, result);
        Assert.Empty(log);
    }

    [Fact]
    public async Task RenameAsync_MoveFails_DoesNotReopen()
    {
        var log = new List<string>();
        var result = await OpenFileRenameCoordinator.RenameAsync(
            "old.utp", "new.utp", currentFilePath: "old.utp", isDirty: false,
            RecordingCallbacks(log, moveOk: false));

        Assert.Equal(OpenFileRenameCoordinator.Result.MoveFailed, result);
        Assert.Equal(new[] { "release", "move" }, log);
    }
}
