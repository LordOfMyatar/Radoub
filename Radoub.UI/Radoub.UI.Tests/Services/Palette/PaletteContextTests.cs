using System.Linq;
using Radoub.Formats.Itp;
using Radoub.UI.Services.Palette;
using Xunit;

namespace Radoub.UI.Tests.Services.Palette;

public class PaletteContextTests
{
    private sealed class FakeGateway : IBlueprintFileGateway
    {
        public byte ReadPaletteId(string filePath) => 0;
        public byte[] ProduceBytesWithPaletteId(string filePath, byte paletteId) => new[] { paletteId };
        public byte ReadPaletteIdFromBytes(byte[] bytes) => bytes[0];
    }

    [Fact]
    public void BuildWriteSet_includes_the_itp_then_changed_blueprints()
    {
        var itp = new ItpFile { NextUseableId = 10 };
        var cat = new PaletteCategoryNode { Id = 1, Name = "Weapons" };
        itp.MainNodes.Add(cat);

        var store = new LooseFileBlueprintStore(new FakeGateway(),
            new[] { ("a", "p/a.uti"), ("b", "p/b.uti") });
        store.SetPaletteId("a", 1); // changed from 0

        var ctx = new PaletteContext(PaletteResourceType.Item, itp, store, "p/itempalcus.itp");

        var writes = ctx.BuildWriteSet().ToList();
        Assert.Equal("p/itempalcus.itp", writes[0].Path);          // .itp first
        Assert.Contains(writes.Skip(1), w => w.Path == "p/a.uti"); // changed blueprint
        Assert.DoesNotContain(writes.Skip(1), w => w.Path == "p/b.uti"); // unchanged
        // .itp ProduceBytes round-trips through ItpWriter/ItpReader
        var bytes = writes[0].ProduceBytes();
        Assert.True(writes[0].Validate(bytes));
    }

    [Fact]
    public void Context_exposes_viewmodel_and_undo_manager()
    {
        var itp = new ItpFile();
        var store = new LooseFileBlueprintStore(new FakeGateway(), System.Array.Empty<(string, string)>());
        var ctx = new PaletteContext(PaletteResourceType.Item, itp, store, "p/itempalcus.itp");
        Assert.NotNull(ctx.ViewModel);
        Assert.NotNull(ctx.UndoManager);
        Assert.Same(itp, ctx.Palette);
    }
}
