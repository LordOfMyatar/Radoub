using System.Collections.Generic;
using System.Linq;
using Radoub.UI.Services.Palette;
using Xunit;

namespace Radoub.UI.Tests.Services.Palette;

public class LooseFileBlueprintStoreTests
{
    // Fake gateway: maps file path -> in-memory PaletteID; records produced bytes.
    private sealed class FakeGateway : IBlueprintFileGateway
    {
        public readonly Dictionary<string, byte> OnDisk = new();
        public readonly List<(string Path, byte Id)> Produced = new();
        public byte ReadPaletteId(string filePath) => OnDisk[filePath];
        public byte[] ProduceBytesWithPaletteId(string filePath, byte paletteId)
        {
            Produced.Add((filePath, paletteId));
            return new[] { paletteId }; // stand-in bytes
        }
        public byte ReadPaletteIdFromBytes(byte[] bytes) => bytes[0];
    }

    private static LooseFileBlueprintStore Build(FakeGateway g, params (string ResRef, string Path, byte Id)[] items)
    {
        foreach (var (_, path, id) in items) g.OnDisk[path] = id;
        var entries = items.Select(i => (i.ResRef, i.Path)).ToList();
        return new LooseFileBlueprintStore(g, entries);
    }

    [Fact]
    public void ResRefs_and_Contains_reflect_the_pool()
    {
        var g = new FakeGateway();
        var store = Build(g, ("acid_dagger", "p/acid_dagger.uti", 5));
        Assert.Contains("acid_dagger", store.ResRefs);
        Assert.True(store.Contains("ACID_DAGGER")); // case-insensitive
        Assert.False(store.Contains("nope"));
    }

    [Fact]
    public void GetPaletteId_returns_original_until_staged_then_staged()
    {
        var g = new FakeGateway();
        var store = Build(g, ("a", "p/a.uti", 5));
        Assert.Equal((byte)5, store.GetPaletteId("a"));
        Assert.True(store.SetPaletteId("a", 9));
        Assert.Equal((byte)9, store.GetPaletteId("a"));
    }

    [Fact]
    public void SetPaletteId_unknown_resref_returns_false()
    {
        var g = new FakeGateway();
        var store = Build(g, ("a", "p/a.uti", 5));
        Assert.False(store.SetPaletteId("ghost", 1));
    }

    [Fact]
    public void BuildBlueprintWrites_emits_only_changed_blueprints()
    {
        var g = new FakeGateway();
        var store = Build(g, ("a", "p/a.uti", 5), ("b", "p/b.uti", 7));
        store.SetPaletteId("a", 9);     // changed
        store.SetPaletteId("b", 7);     // staged equal to original -> no write
        var writes = store.BuildBlueprintWrites().ToList();
        Assert.Single(writes);
        Assert.Equal("p/a.uti", writes[0].Path);
        // ProduceBytes uses the gateway with the staged id
        var bytes = writes[0].ProduceBytes();
        Assert.Equal(new byte[] { 9 }, bytes);
    }

    [Fact]
    public void BuildBlueprintWrites_validator_accepts_matching_reread()
    {
        var g = new FakeGateway();
        var store = Build(g, ("a", "p/a.uti", 5));
        store.SetPaletteId("a", 9);
        var w = store.BuildBlueprintWrites().Single();
        // Validate re-reads PaletteID from produced bytes; fake encodes id as bytes[0].
        Assert.True(w.Validate(new byte[] { 9 }));
        Assert.False(w.Validate(new byte[] { 8 }));
    }
}
