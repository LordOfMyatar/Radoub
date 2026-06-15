using System;
using System.Collections.Generic;
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

    private static PaletteContext MakeContext(PaletteResourceType type)
    {
        var itp = new ItpFile();
        var cat = new PaletteCategoryNode { Id = 1, Name = type + "Cat" };
        itp.MainNodes.Add(cat);
        var store = new LooseFileBlueprintStore(new FakeGateway(), Array.Empty<(string, string)>());
        return new PaletteContext(type, itp, store, $"p/{type}.itp");
    }

    private static PaletteEditorHostViewModel MakeHost(
        Func<Task<DirtySwitchChoice>>? prompt = null,
        Func<IReadOnlyList<PaletteFileWrite>, PaletteSaveResult>? commit = null,
        Action<PaletteResourceType>? onLoad = null)
    {
        return new PaletteEditorHostViewModel(
            loadContext: t => { onLoad?.Invoke(t); return MakeContext(t); },
            promptDirty: prompt ?? (() => Task.FromResult(DirtySwitchChoice.Discard)),
            commit: commit ?? (_ => new PaletteSaveResult(true, null)));
    }

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
    public async Task SwitchType_when_clean_does_not_prompt_and_rebuilds()
    {
        int prompts = 0;
        var loaded = new List<PaletteResourceType>();
        var host = MakeHost(
            prompt: () => { prompts++; return Task.FromResult(DirtySwitchChoice.Discard); },
            onLoad: loaded.Add);
        await host.SwitchResourceTypeAsync(PaletteResourceType.Item);
        await host.SwitchResourceTypeAsync(PaletteResourceType.Creature);
        Assert.Equal(0, prompts); // clean -> no prompt
        Assert.Equal(new[] { PaletteResourceType.Item, PaletteResourceType.Creature }, loaded);
        Assert.Equal(PaletteResourceType.Creature, host.ActiveContext!.Type);
    }

    [Fact]
    public async Task SwitchType_when_dirty_and_cancel_keeps_current_context()
    {
        var host = MakeHost(prompt: () => Task.FromResult(DirtySwitchChoice.Cancel));
        await host.SwitchResourceTypeAsync(PaletteResourceType.Item);
        host.ActiveContext!.ViewModel.IsDirty = true; // simulate an edit

        await host.SwitchResourceTypeAsync(PaletteResourceType.Creature); // should be refused
        Assert.Equal(PaletteResourceType.Item, host.ActiveContext!.Type); // unchanged
    }

    [Fact]
    public async Task SwitchType_when_dirty_and_save_commits_then_switches()
    {
        int commits = 0;
        var host = MakeHost(
            prompt: () => Task.FromResult(DirtySwitchChoice.Save),
            commit: _ => { commits++; return new PaletteSaveResult(true, null); });
        await host.SwitchResourceTypeAsync(PaletteResourceType.Item);
        host.ActiveContext!.ViewModel.IsDirty = true;

        await host.SwitchResourceTypeAsync(PaletteResourceType.Creature);
        Assert.Equal(1, commits);
        Assert.Equal(PaletteResourceType.Creature, host.ActiveContext!.Type);
    }

    [Fact]
    public async Task Save_commits_write_set_and_clears_dirty_on_success()
    {
        var host = MakeHost(commit: _ => new PaletteSaveResult(true, null));
        await host.SwitchResourceTypeAsync(PaletteResourceType.Item);
        host.ActiveContext!.ViewModel.IsDirty = true;

        bool ok = host.Save();
        Assert.True(ok);
        Assert.False(host.ActiveContext!.ViewModel.IsDirty);
    }

    [Fact]
    public async Task Save_keeps_dirty_when_commit_fails()
    {
        var host = MakeHost(commit: _ => new PaletteSaveResult(false, new Exception("boom")));
        await host.SwitchResourceTypeAsync(PaletteResourceType.Item);
        host.ActiveContext!.ViewModel.IsDirty = true;

        bool ok = host.Save();
        Assert.False(ok);
        Assert.True(host.ActiveContext!.ViewModel.IsDirty); // not cleared on failure
    }
}
