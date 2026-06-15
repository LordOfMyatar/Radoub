using System.Linq;
using Radoub.Formats.Itp;
using Radoub.UI.Services.Palette;
using Radoub.UI.Undo;
using Xunit;

namespace Radoub.UI.Tests.Undo;

public class PaletteDeleteCategoryCommandTests
{
    private sealed class FakeGateway : IBlueprintFileGateway
    {
        public byte ReadPaletteId(string filePath) => 0;
        public byte[] ProduceBytesWithPaletteId(string filePath, byte id) => new[] { id };
        public byte ReadPaletteIdFromBytes(byte[] bytes) => bytes[0];
    }

    // root: [Misc(9), Weapons(1){ acid_dagger(id1), Melee(2){} }]
    private static (ItpFile itp, LooseFileBlueprintStore store, PaletteCategoryNode weapons) Build()
    {
        var itp = new ItpFile();
        itp.MainNodes.Add(new PaletteCategoryNode { Id = 9, Name = "Misc" });
        var weapons = new PaletteCategoryNode { Id = 1, Name = "Weapons" };
        weapons.Blueprints.Add(new PaletteBlueprintNode { ResRef = "acid_dagger" });
        weapons.Children.Add(new PaletteCategoryNode { Id = 2, Name = "Melee" });
        itp.MainNodes.Add(weapons);

        var store = new LooseFileBlueprintStore(new FakeGateway(), new[] { ("acid_dagger", "p/acid_dagger.uti") });
        store.SetPaletteId("acid_dagger", 1); // in sync with Weapons
        return (itp, store, weapons);
    }

    [Fact]
    public void Do_removes_category_reparents_child_and_uncategorizes_blueprint()
    {
        var (itp, store, weapons) = Build();
        var cmd = new PaletteDeleteCategoryCommand(itp, store, weapons, onChanged: null);

        Assert.True(cmd.Do());

        // Weapons gone from root; Melee reparented to root at Weapons' old index (1).
        Assert.DoesNotContain(itp.MainNodes, n => ReferenceEquals(n, weapons));
        Assert.IsType<PaletteCategoryNode>(itp.MainNodes[1]);
        Assert.Equal((byte)2, ((PaletteCategoryNode)itp.MainNodes[1]).Id);
        // acid_dagger no longer listed -> uncategorized
        Assert.Equal(PalettePlacementKind.Uncategorized, PaletteReorgMutator.Classify(itp, store, "acid_dagger").Kind);
    }

    [Fact]
    public void Undo_restores_category_position_children_and_blueprint_placement()
    {
        var (itp, store, weapons) = Build();
        var cmd = new PaletteDeleteCategoryCommand(itp, store, weapons, onChanged: null);
        cmd.Do();
        cmd.Undo();

        // Weapons back at root index 1 with its child Melee and its blueprint, in sync again.
        Assert.Same(weapons, itp.MainNodes[1]);
        Assert.Contains(weapons.Children.OfType<PaletteCategoryNode>(), c => c.Id == 2);
        Assert.Contains(weapons.Blueprints, b => b.ResRef == "acid_dagger");
        Assert.Equal((byte)1, store.GetPaletteId("acid_dagger"));
        Assert.Equal(PalettePlacementKind.InSync, PaletteReorgMutator.Classify(itp, store, "acid_dagger").Kind);
    }

    [Fact]
    public void Do_returns_false_when_category_not_in_tree()
    {
        var (itp, store, _) = Build();
        var orphan = new PaletteCategoryNode { Id = 77 };
        var cmd = new PaletteDeleteCategoryCommand(itp, store, orphan, onChanged: null);
        Assert.False(cmd.Do());
    }

    [Fact]
    public void Undo_restores_multiple_children_in_order_with_sibling_after()
    {
        // Case B: root [A(8), W(1){children M(2),N(3)}, B(7)]. Deleting W reparents M,N at index 1
        // (so root becomes [A,M,N,B]); undo must restore [A,W,B] with W.Children == [M,N] in order.
        var itp = new ItpFile();
        itp.MainNodes.Add(new PaletteCategoryNode { Id = 8, Name = "A" });
        var w = new PaletteCategoryNode { Id = 1, Name = "W" };
        w.Children.Add(new PaletteCategoryNode { Id = 2, Name = "M" });
        w.Children.Add(new PaletteCategoryNode { Id = 3, Name = "N" });
        itp.MainNodes.Add(w);
        itp.MainNodes.Add(new PaletteCategoryNode { Id = 7, Name = "B" });
        var store = new LooseFileBlueprintStore(new FakeGateway(), System.Array.Empty<(string, string)>());

        var cmd = new PaletteDeleteCategoryCommand(itp, store, w, onChanged: null);
        cmd.Do();
        // after delete: [A, M, N, B]
        Assert.Equal(new byte[] { 8, 2, 3, 7 }, itp.MainNodes.Cast<PaletteCategoryNode>().Select(c => c.Id).ToArray());

        cmd.Undo();
        // restored: [A, W, B] with W.Children == [M, N]
        Assert.Equal(new byte[] { 8, 1, 7 }, itp.MainNodes.Cast<PaletteCategoryNode>().Select(c => c.Id).ToArray());
        Assert.Equal(new byte[] { 2, 3 }, w.Children.Cast<PaletteCategoryNode>().Select(c => c.Id).ToArray());
    }

    [Fact]
    public void Do_Undo_Redo_returns_to_post_delete_state()
    {
        var (itp, store, weapons) = Build();
        var cmd = new PaletteDeleteCategoryCommand(itp, store, weapons, onChanged: null);
        cmd.Do();
        cmd.Undo();
        cmd.Do(); // redo: re-snapshots from the restored state and re-applies

        Assert.DoesNotContain(itp.MainNodes, n => ReferenceEquals(n, weapons));
        Assert.Equal((byte)2, ((PaletteCategoryNode)itp.MainNodes[1]).Id); // Melee reparented again
        Assert.Equal(PalettePlacementKind.Uncategorized,
            PaletteReorgMutator.Classify(itp, store, "acid_dagger").Kind);
    }

    [Fact]
    public void Do_returns_false_and_rolls_back_when_refresh_throws()
    {
        var (itp, store, weapons) = Build();
        var cmd = new PaletteDeleteCategoryCommand(itp, store, weapons,
            onChanged: () => throw new System.InvalidOperationException());

        Assert.False(cmd.Do());
        // rolled back: Weapons still present with its contents intact
        Assert.Same(weapons, itp.MainNodes[1]);
        Assert.Contains(weapons.Blueprints, b => b.ResRef == "acid_dagger");
        Assert.Equal((byte)1, store.GetPaletteId("acid_dagger"));
    }
}
