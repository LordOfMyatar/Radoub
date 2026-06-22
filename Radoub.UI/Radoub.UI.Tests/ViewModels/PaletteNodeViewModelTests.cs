using System.Linq;
using Radoub.Formats.Itp;
using Radoub.UI.Services.Palette;
using Radoub.UI.ViewModels;
using Xunit;

namespace Radoub.UI.Tests.ViewModels;

public class PaletteNodeViewModelTests
{
    private sealed class FakeGateway : IBlueprintFileGateway
    {
        public byte ReadPaletteId(string filePath) => 0;
        public byte[] ProduceBytesWithPaletteId(string filePath, byte id) => new[] { id };
        public byte ReadPaletteIdFromBytes(byte[] bytes) => bytes[0];
    }

    // Build a VM with a tree: Weapons(1) { acid_dagger(in-sync), rusty(drifted) }, plus an
    // uncategorized blueprint not listed anywhere.
    // Tree = category structure only (Weapons id 1, Armor id 2). Placement comes from each
    // blueprint's PaletteID, not the tree's Blueprints lists.
    private static PaletteEditorViewModel BuildVm()
    {
        var itp = new ItpFile();
        itp.MainNodes.Add(new PaletteCategoryNode { Id = 1, Name = "Weapons" });
        itp.MainNodes.Add(new PaletteCategoryNode { Id = 2, Name = "Armor" });

        var store = new LooseFileBlueprintStore(new FakeGateway(), new[]
        {
            ("acid_dagger", "p/acid_dagger.uti"),
            ("rusty", "p/rusty.uti"),
            ("loose", "p/loose.uti"),
        });
        store.SetPaletteId("acid_dagger", 1); // -> Weapons
        store.SetPaletteId("rusty", 1);        // -> Weapons
        // "loose" stays at PaletteID 0 (no category 0) -> Uncategorized
        return new PaletteEditorViewModel(itp, store);
    }

    [Fact]
    public void BuildForest_places_blueprint_leaves_under_their_PaletteId_category()
    {
        var vm = BuildVm();
        var forest = PaletteNodeViewModel.BuildForest(vm);

        var weapons = forest.Single(n => n.Kind == PaletteNodeKind.Category && n.Name == "Weapons");
        Assert.Equal(2, weapons.Children.Count(c => c.Kind == PaletteNodeKind.Blueprint));
        Assert.Contains(weapons.Children, c => c.Name == "acid_dagger");
        Assert.Contains(weapons.Children, c => c.Name == "rusty");

        var armor = forest.Single(n => n.Name == "Armor");
        Assert.Empty(armor.Children); // nothing points at id 2
    }

    [Fact]
    public void BuildForest_appends_virtual_uncategorized_node_with_its_blueprints()
    {
        var vm = BuildVm();
        var forest = PaletteNodeViewModel.BuildForest(vm);

        var unc = forest.Single(n => n.Kind == PaletteNodeKind.Uncategorized);
        Assert.Contains(unc.Children, c => c.Name == "loose"); // PaletteID 0 -> no category
        // virtual node has no backing model node
        Assert.Null(unc.Model);
    }

    [Fact]
    public void BuildForest_reflects_PaletteId_change_not_stale_tree_listing()
    {
        // Stale tree lists sword under Weapons(1), but its PaletteID says Armor(2): it must appear
        // under Armor (the file's PaletteID wins).
        var itp = new ItpFile();
        var weapons = new PaletteCategoryNode { Id = 1, Name = "Weapons" };
        weapons.Blueprints.Add(new PaletteBlueprintNode { ResRef = "sword" }); // stale listing
        itp.MainNodes.Add(weapons);
        itp.MainNodes.Add(new PaletteCategoryNode { Id = 2, Name = "Armor" });

        var store = new LooseFileBlueprintStore(new FakeGateway(), new[] { ("sword", "p/sword.uti") });
        store.SetPaletteId("sword", 2); // file says Armor

        var forest = PaletteNodeViewModel.BuildForest(new PaletteEditorViewModel(itp, store));

        Assert.Empty(forest.Single(n => n.Name == "Weapons").Children);
        Assert.Contains(forest.Single(n => n.Name == "Armor").Children, c => c.Name == "sword");
    }

    [Fact]
    public void Category_with_strref_resolves_name_via_resolver()
    {
        // Standard categories carry their name as a TLK StrRef, not a literal Name.
        var itp = new ItpFile();
        itp.MainNodes.Add(new PaletteCategoryNode { Id = 1, StrRef = 5432 }); // no literal Name
        var store = new LooseFileBlueprintStore(new FakeGateway(), System.Array.Empty<(string, string)>());
        var vm = new PaletteEditorViewModel(itp, store);

        var forest = PaletteNodeViewModel.BuildForest(vm, strRefResolver: s => s == 5432 ? "Armor" : null);

        Assert.Contains(forest, n => n.Kind == PaletteNodeKind.Category && n.Name == "Armor");
    }

    [Fact]
    public void Category_strref_falls_back_to_placeholder_when_unresolved()
    {
        var itp = new ItpFile();
        itp.MainNodes.Add(new PaletteCategoryNode { Id = 1, StrRef = 99 });
        var store = new LooseFileBlueprintStore(new FakeGateway(), System.Array.Empty<(string, string)>());
        var vm = new PaletteEditorViewModel(itp, store);

        // No resolver, or resolver returns null/empty -> placeholder showing the StrRef.
        var forest = PaletteNodeViewModel.BuildForest(vm, strRefResolver: _ => null);
        Assert.Contains(forest, n => n.Kind == PaletteNodeKind.Category && n.Name == "[StrRef 99]");
    }

    // --- Full-path tooltip for duplicate/nested name disambiguation (#2488) ---

    [Fact]
    public void FullPath_TopLevelCategory_IsJustItsName()
    {
        var vm = BuildVm();
        var forest = PaletteNodeViewModel.BuildForest(vm);

        var weapons = forest.Single(n => n.Name == "Weapons");
        Assert.Equal("Weapons", weapons.FullPath);
    }

    [Fact]
    public void FullPath_NestedNode_ChainsAncestorNames()
    {
        // Weapons › Custom 1 › a_blade  — the tree nests, so the path makes a repeated
        // "Custom 1" unambiguous on hover.
        var itp = new ItpFile();
        var weapons = new PaletteCategoryNode { Id = 1, Name = "Weapons" };
        var custom = new PaletteCategoryNode { Id = 5, Name = "Custom 1" };
        weapons.Children.Add(custom);
        itp.MainNodes.Add(weapons);

        var store = new LooseFileBlueprintStore(new FakeGateway(), new[] { ("a_blade", "p/a_blade.uti") });
        store.SetPaletteId("a_blade", 5); // -> Custom 1

        var forest = PaletteNodeViewModel.BuildForest(new PaletteEditorViewModel(itp, store));

        var weaponsVm = forest.Single(n => n.Name == "Weapons");
        var customVm = weaponsVm.Children.Single(c => c.Name == "Custom 1");
        var bladeVm = customVm.Children.Single(c => c.Name == "a_blade");

        Assert.Equal("Weapons › Custom 1", customVm.FullPath);
        Assert.Equal("Weapons › Custom 1 › a_blade", bladeVm.FullPath);
    }
}
