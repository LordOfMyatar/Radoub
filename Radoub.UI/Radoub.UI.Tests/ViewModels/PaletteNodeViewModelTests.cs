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
    private static PaletteEditorViewModel BuildVm()
    {
        var itp = new ItpFile();
        var weapons = new PaletteCategoryNode { Id = 1, Name = "Weapons" };
        weapons.Blueprints.Add(new PaletteBlueprintNode { ResRef = "acid_dagger" });
        weapons.Blueprints.Add(new PaletteBlueprintNode { ResRef = "rusty" });
        itp.MainNodes.Add(weapons);

        var store = new LooseFileBlueprintStore(new FakeGateway(), new[]
        {
            ("acid_dagger", "p/acid_dagger.uti"), // PaletteID 0 from fake -> set to 1 (in sync)
            ("rusty", "p/rusty.uti"),             // leave at 0 -> drifted (tree says cat 1)
            ("loose", "p/loose.uti"),             // not in tree -> uncategorized
        });
        store.SetPaletteId("acid_dagger", 1);
        return new PaletteEditorViewModel(itp, store);
    }

    [Fact]
    public void BuildForest_includes_categories_with_blueprint_leaves_inline()
    {
        var vm = BuildVm();
        var forest = PaletteNodeViewModel.BuildForest(vm);

        var weapons = forest.Single(n => n.Kind == PaletteNodeKind.Category && n.Name == "Weapons");
        Assert.Equal(2, weapons.Children.Count(c => c.Kind == PaletteNodeKind.Blueprint));
        Assert.Contains(weapons.Children, c => c.Name == "acid_dagger");
    }

    [Fact]
    public void BuildForest_appends_virtual_uncategorized_node_with_its_blueprints()
    {
        var vm = BuildVm();
        var forest = PaletteNodeViewModel.BuildForest(vm);

        var unc = forest.Single(n => n.Kind == PaletteNodeKind.Uncategorized);
        Assert.Contains(unc.Children, c => c.Name == "loose");
        // virtual node has no backing model node
        Assert.Null(unc.Model);
    }

    [Fact]
    public void Drifted_blueprint_leaf_is_flagged()
    {
        var vm = BuildVm();
        var forest = PaletteNodeViewModel.BuildForest(vm);
        var weapons = forest.Single(n => n.Name == "Weapons");

        var rusty = weapons.Children.Single(c => c.Name == "rusty");
        var acid = weapons.Children.Single(c => c.Name == "acid_dagger");
        Assert.True(rusty.IsDrifted);   // PaletteID 0 != tree cat 1
        Assert.False(acid.IsDrifted);   // PaletteID 1 == tree cat 1
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
}
