using Radoub.Formats.Itp;
using Radoub.UI.Services.Palette;
using Radoub.UI.ViewModels;
using Xunit;

namespace Radoub.UI.Tests.ViewModels;

public class PaletteEditorViewModelFileBlueprintTests
{
    private sealed class FakeGateway : IBlueprintFileGateway
    {
        public byte ReadPaletteId(string filePath) => 0;
        public byte[] ProduceBytesWithPaletteId(string filePath, byte id) => new[] { id };
        public byte ReadPaletteIdFromBytes(byte[] bytes) => bytes[0];
    }

    [Fact]
    public void FileBlueprint_adds_tree_entry_and_stages_palette_id()
    {
        var itp = new ItpFile();
        var weapons = new PaletteCategoryNode { Id = 3, Name = "Weapons" };
        itp.MainNodes.Add(weapons);
        var store = new LooseFileBlueprintStore(new FakeGateway(), new[] { ("loose", "p/loose.uti") });
        var vm = new PaletteEditorViewModel(itp, store);

        // before: uncategorized
        Assert.Equal(PalettePlacementKind.Uncategorized, vm.Classify("loose").Kind);

        Assert.True(vm.FileBlueprint("loose", weapons));

        Assert.Contains(weapons.Blueprints, b => b.ResRef == "loose");
        Assert.Equal((byte)3, store.GetPaletteId("loose"));
        Assert.Equal(PalettePlacementKind.InSync, vm.Classify("loose").Kind);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void FileBlueprint_unknown_resref_is_a_noop()
    {
        var itp = new ItpFile();
        var cat = new PaletteCategoryNode { Id = 1 };
        itp.MainNodes.Add(cat);
        var store = new LooseFileBlueprintStore(new FakeGateway(), System.Array.Empty<(string, string)>());
        var vm = new PaletteEditorViewModel(itp, store);

        Assert.False(vm.FileBlueprint("ghost", cat));
        Assert.Empty(cat.Blueprints);
    }

    [Fact]
    public void FileBlueprint_already_listed_blueprint_is_a_noop()
    {
        // Guard against double-filing: a blueprint already in the tree must not get a second entry.
        var itp = new ItpFile();
        var weapons = new PaletteCategoryNode { Id = 1, Name = "Weapons" };
        weapons.Blueprints.Add(new PaletteBlueprintNode { ResRef = "sword" });
        itp.MainNodes.Add(weapons);
        var armor = new PaletteCategoryNode { Id = 2, Name = "Armor" };
        itp.MainNodes.Add(armor);
        var store = new LooseFileBlueprintStore(new FakeGateway(), new[] { ("sword", "p/sword.uti") });
        store.SetPaletteId("sword", 1);
        var vm = new PaletteEditorViewModel(itp, store);

        Assert.False(vm.FileBlueprint("sword", armor)); // already listed under Weapons
        Assert.Empty(armor.Blueprints);
        Assert.Single(weapons.Blueprints); // unchanged
    }

    [Fact]
    public void FileBlueprint_rolls_back_when_refresh_throws()
    {
        var itp = new ItpFile();
        var cat = new PaletteCategoryNode { Id = 5 };
        itp.MainNodes.Add(cat);
        var store = new LooseFileBlueprintStore(new FakeGateway(), new[] { ("loose", "p/loose.uti") });
        var vm = new PaletteEditorViewModel(itp, store, onTreeChanged: () => throw new System.InvalidOperationException());

        Assert.False(vm.FileBlueprint("loose", cat));
        Assert.Empty(cat.Blueprints);              // tree entry removed on rollback
        Assert.Equal((byte)0, store.GetPaletteId("loose")); // staged id restored
        Assert.False(vm.IsDirty);
    }
}
