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
    public void BuildWriteSet_reconciles_tree_to_palette_ids()
    {
        // sword's tree listing is stale (under Weapons 1) but its PaletteID says Armor 2.
        // BuildWriteSet must reconcile the in-memory tree so the .itp it writes lists sword under
        // Armor and not under Weapons.
        var itp = new ItpFile();
        var weapons = new PaletteCategoryNode { Id = 1, Name = "Weapons" };
        weapons.Blueprints.Add(new PaletteBlueprintNode { ResRef = "sword" });
        itp.MainNodes.Add(weapons);
        var armor = new PaletteCategoryNode { Id = 2, Name = "Armor" };
        itp.MainNodes.Add(armor);

        var store = new LooseFileBlueprintStore(new FakeGateway(), new[] { ("sword", "p/sword.uti") });
        store.SetPaletteId("sword", 2); // file says Armor

        var ctx = new PaletteContext(PaletteResourceType.Item, itp, store, "p/itempalcus.itp");
        ctx.BuildWriteSet(); // triggers reconciliation

        Assert.DoesNotContain(weapons.Blueprints, b => b.ResRef == "sword");
        Assert.Contains(armor.Blueprints, b => b.ResRef == "sword");
    }

    [Fact]
    public void BuildWriteSet_drops_tree_entry_when_palette_id_names_no_category()
    {
        // sword listed under Weapons(1) but PaletteID 99 names no live category -> reconcile drops
        // it from the tree entirely (it is Uncategorized; never written as a real placement).
        var itp = new ItpFile();
        var weapons = new PaletteCategoryNode { Id = 1, Name = "Weapons" };
        weapons.Blueprints.Add(new PaletteBlueprintNode { ResRef = "sword" });
        itp.MainNodes.Add(weapons);

        var store = new LooseFileBlueprintStore(new FakeGateway(), new[] { ("sword", "p/sword.uti") });
        store.SetPaletteId("sword", 99);

        var ctx = new PaletteContext(PaletteResourceType.Item, itp, store, "p/itempalcus.itp");
        ctx.BuildWriteSet();

        Assert.Empty(weapons.Blueprints);
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
