using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Radoub.Formats.Itp;
using Radoub.UI.Services.Palette;
using Radoub.UI.ViewModels;
using Xunit;

namespace Radoub.UI.Tests.ViewModels;

public class PaletteEditorHostViewModelTests
{
    private sealed class FakeGateway : IBlueprintFileGateway
    {
        public byte ReadPaletteId(string filePath) => 0;
        public byte[] ProduceBytesWithPaletteId(string filePath, byte id) => new[] { id };
        public byte ReadPaletteIdFromBytes(byte[] bytes) => bytes[0];
    }

    // A context with two categories (ids 1,2) and one pool blueprint (PaletteID 0 -> uncategorized).
    private static PaletteContext MakeContext(PaletteResourceType type)
    {
        var itp = new ItpFile();
        itp.MainNodes.Add(new PaletteCategoryNode { Id = 1, Name = type + "_A" });
        itp.MainNodes.Add(new PaletteCategoryNode { Id = 2, Name = type + "_B" });
        var store = new LooseFileBlueprintStore(new FakeGateway(), new[] { ("bp", "p/bp.uti") });
        return new PaletteContext(type, itp, store, $"p/{type}.itp");
    }

    private static PaletteEditorHostViewModel MakeHost(
        Func<IReadOnlyList<PaletteFileWrite>, PaletteSaveResult>? commit = null,
        Action<PaletteResourceType>? onLoad = null,
        Func<string, string?>? lockHolder = null)
    {
        return new PaletteEditorHostViewModel(
            loadContext: t => { onLoad?.Invoke(t); return MakeContext(t); },
            commit: commit ?? (_ => new PaletteSaveResult(true, null)),
            lockHolder: lockHolder ?? (_ => null)); // no lock by default
    }

    private static PaletteCategoryNode Cat(PaletteEditorHostViewModel host, byte id)
        => host.ActiveContext!.Palette.GetCategories().Single(c => c.Id == id);

    [Fact]
    public async Task LoadType_builds_context_and_forest()
    {
        var host = MakeHost();
        await host.SwitchResourceTypeAsync(PaletteResourceType.Item);
        Assert.NotNull(host.ActiveContext);
        Assert.Equal(PaletteResourceType.Item, host.ActiveContext!.Type);
        Assert.NotEmpty(host.Forest);
    }

    [Fact]
    public async Task SwitchType_reloads_fresh_context_per_type()
    {
        var loaded = new List<PaletteResourceType>();
        var host = MakeHost(onLoad: loaded.Add);
        await host.SwitchResourceTypeAsync(PaletteResourceType.Item);
        await host.SwitchResourceTypeAsync(PaletteResourceType.Creature);
        Assert.Equal(new[] { PaletteResourceType.Item, PaletteResourceType.Creature }, loaded);
        Assert.Equal(PaletteResourceType.Creature, host.ActiveContext!.Type);
    }

    [Fact]
    public async Task MoveBlueprint_commits_immediately()
    {
        int commits = 0;
        var host = MakeHost(commit: w => { commits++; return new PaletteSaveResult(true, null); });
        await host.SwitchResourceTypeAsync(PaletteResourceType.Item);

        bool ok = host.MoveBlueprintToCategory("bp", Cat(host, 1));
        Assert.True(ok);
        Assert.Equal(1, commits);                               // saved on the move
        Assert.Equal((byte)1, host.ActiveContext!.Store.GetPaletteId("bp")); // PaletteID set
    }

    [Fact]
    public async Task MoveBlueprint_commit_failure_reloads_and_raises_SaveFailed()
    {
        string? reported = null;
        int loads = 0;
        var host = MakeHost(
            commit: _ => new PaletteSaveResult(false, new Exception("disk locked")),
            onLoad: _ => loads++);
        host.SaveFailed += m => reported = m;
        await host.SwitchResourceTypeAsync(PaletteResourceType.Item); // loads == 1

        bool ok = host.MoveBlueprintToCategory("bp", Cat(host, 1));
        Assert.False(ok);
        Assert.Equal(2, loads);                 // reloaded from disk to re-sync after failure
        Assert.Contains("disk locked", reported);
    }

    [Fact]
    public async Task MoveBlueprint_refused_when_open_in_another_tool()
    {
        int commits = 0;
        string? reported = null;
        var host = MakeHost(
            commit: _ => { commits++; return new PaletteSaveResult(true, null); },
            lockHolder: path => path.EndsWith("bp.uti") ? "Relique" : null);
        host.SaveFailed += m => reported = m;
        await host.SwitchResourceTypeAsync(PaletteResourceType.Item);

        bool ok = host.MoveBlueprintToCategory("bp", Cat(host, 1));
        Assert.False(ok);
        Assert.Equal(0, commits);                      // nothing written
        Assert.Contains("Relique", reported);          // names the holding tool
        Assert.Equal((byte)0, host.ActiveContext!.Store.GetPaletteId("bp")); // PaletteID unchanged
    }

    [Fact]
    public async Task ReloadActiveFromDisk_rebuilds_without_prompt()
    {
        int loads = 0;
        var host = MakeHost(onLoad: _ => loads++);
        await host.SwitchResourceTypeAsync(PaletteResourceType.Item); // loads == 1
        host.ReloadActiveFromDisk();
        Assert.Equal(2, loads);
        Assert.NotNull(host.ActiveContext);
    }

    // --- Undo/redo across all reorg gestures (#2484) ---

    [Fact]
    public async Task MoveBlueprint_is_undoable_and_redoable()
    {
        var host = MakeHost();
        await host.SwitchResourceTypeAsync(PaletteResourceType.Item);

        host.MoveBlueprintToCategory("bp", Cat(host, 1));
        Assert.Equal((byte)1, host.ActiveContext!.Store.GetPaletteId("bp"));
        Assert.True(host.ActiveContext.UndoManager.CanUndo);

        host.Undo();
        Assert.Equal((byte)0, host.ActiveContext.Store.GetPaletteId("bp")); // back to uncategorized

        host.Redo();
        Assert.Equal((byte)1, host.ActiveContext.Store.GetPaletteId("bp"));
    }

    [Fact]
    public async Task AddCategory_is_undoable_and_redoable_with_stable_id()
    {
        var host = MakeHost();
        await host.SwitchResourceTypeAsync(PaletteResourceType.Item);

        Assert.True(host.AddCategory(null, "New Cat"));
        var added = host.ActiveContext!.Palette.GetCategories().Single(c => c.Name == "New Cat");
        byte id = added.Id;

        host.Undo();
        Assert.DoesNotContain(host.ActiveContext.Palette.GetCategories(), c => c.Name == "New Cat");

        host.Redo();
        var again = host.ActiveContext.Palette.GetCategories().Single(c => c.Name == "New Cat");
        Assert.Equal(id, again.Id); // redo reuses the same id, never reallocates
    }

    [Fact]
    public async Task RenameCategory_is_undoable()
    {
        var host = MakeHost();
        await host.SwitchResourceTypeAsync(PaletteResourceType.Item);
        var cat = Cat(host, 1);
        string original = cat.Name!;

        Assert.True(host.RenameCategory(cat, "Renamed"));
        Assert.Equal("Renamed", cat.Name);

        host.Undo();
        Assert.Equal(original, cat.Name);
    }

    [Fact]
    public async Task MoveCategory_is_undoable()
    {
        var host = MakeHost();
        await host.SwitchResourceTypeAsync(PaletteResourceType.Item);
        var moved = Cat(host, 2);
        var parent = Cat(host, 1);

        Assert.True(host.MoveCategory(moved, parent, 0));
        Assert.Contains(parent.Children, n => ReferenceEquals(n, moved));

        host.Undo();
        Assert.DoesNotContain(parent.Children, n => ReferenceEquals(n, moved));
        Assert.Contains(host.ActiveContext!.Palette.MainNodes, n => ReferenceEquals(n, moved));
    }

    [Fact]
    public async Task Undo_and_redo_each_commit_to_disk()
    {
        int commits = 0;
        var host = MakeHost(commit: _ => { commits++; return new PaletteSaveResult(true, null); });
        await host.SwitchResourceTypeAsync(PaletteResourceType.Item);

        host.RenameCategory(Cat(host, 1), "X"); // commit #1
        host.Undo();                            // commit #2 (persist the revert)
        host.Redo();                            // commit #3
        Assert.Equal(3, commits);
    }

    [Fact]
    public async Task Reorg_that_self_rolls_back_on_commit_failure_records_no_undo()
    {
        // A rename whose commit fails: the op reverts (reload-from-disk) and must leave no undo entry
        // to replay later.
        var host = MakeHost(commit: _ => new PaletteSaveResult(false, new Exception("locked")));
        await host.SwitchResourceTypeAsync(PaletteResourceType.Item);

        bool ok = host.RenameCategory(Cat(host, 1), "X");
        Assert.False(ok);
        // The command Do() succeeded (rename applied + forest rebuilt), so it WAS recorded; but the
        // commit failed and reloaded from disk. The undo stack belongs to the now-replaced context.
        // Either way, undoing must not throw and must not resurrect "X".
        host.Undo();
        Assert.DoesNotContain(host.ActiveContext!.Palette.GetCategories(), c => c.Name == "X");
    }

    // --- #2484 review fixes: empty-stack no-op + cross-tool lock guard on undo/redo ---

    [Fact]
    public async Task Undo_or_redo_with_empty_history_does_not_commit()
    {
        int commits = 0;
        var host = MakeHost(commit: _ => { commits++; return new PaletteSaveResult(true, null); });
        await host.SwitchResourceTypeAsync(PaletteResourceType.Item);

        host.Undo(); // nothing to undo
        host.Redo(); // nothing to redo
        Assert.Equal(0, commits); // no disk write on a no-op keystroke
    }

    [Fact]
    public async Task Undo_of_blueprint_move_refused_when_blueprint_open_in_another_tool()
    {
        // The blueprint is free at move time but gets opened in another tool before the undo.
        string? lockedBy = null;
        string? reported = null;
        int commits = 0;
        var host = MakeHost(
            commit: _ => { commits++; return new PaletteSaveResult(true, null); },
            lockHolder: path => path.EndsWith("bp.uti") ? lockedBy : null);
        host.SaveFailed += m => reported = m;
        await host.SwitchResourceTypeAsync(PaletteResourceType.Item);

        host.MoveBlueprintToCategory("bp", Cat(host, 1)); // free -> moves, commit #1
        Assert.Equal((byte)1, host.ActiveContext!.Store.GetPaletteId("bp"));
        int commitsAfterMove = commits;

        lockedBy = "Relique"; // another tool opens the blueprint
        host.Undo();          // must be refused — undo rewrites the file

        Assert.Equal((byte)1, host.ActiveContext.Store.GetPaletteId("bp")); // NOT reverted
        Assert.Equal(commitsAfterMove, commits);                            // no disk write
        Assert.Contains("Relique", reported);                              // names the holder
    }

    [Fact]
    public async Task Undo_of_blueprint_move_proceeds_once_the_other_tool_releases_the_lock()
    {
        string? lockedBy = "Relique";
        var host = MakeHost(lockHolder: path => path.EndsWith("bp.uti") ? lockedBy : null);
        await host.SwitchResourceTypeAsync(PaletteResourceType.Item);

        // Move requires the lock free; release it for the move, re-acquire, then release again.
        lockedBy = null;
        host.MoveBlueprintToCategory("bp", Cat(host, 1));
        Assert.Equal((byte)1, host.ActiveContext!.Store.GetPaletteId("bp"));

        lockedBy = null; // tool closed it
        host.Undo();
        Assert.Equal((byte)0, host.ActiveContext.Store.GetPaletteId("bp")); // reverted
    }
}
