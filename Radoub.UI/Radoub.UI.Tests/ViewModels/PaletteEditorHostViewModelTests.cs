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
        Action<PaletteResourceType>? onLoad = null)
    {
        return new PaletteEditorHostViewModel(
            loadContext: t => { onLoad?.Invoke(t); return MakeContext(t); },
            commit: commit ?? (_ => new PaletteSaveResult(true, null)));
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
    public async Task ReloadActiveFromDisk_rebuilds_without_prompt()
    {
        int loads = 0;
        var host = MakeHost(onLoad: _ => loads++);
        await host.SwitchResourceTypeAsync(PaletteResourceType.Item); // loads == 1
        host.ReloadActiveFromDisk();
        Assert.Equal(2, loads);
        Assert.NotNull(host.ActiveContext);
    }
}
