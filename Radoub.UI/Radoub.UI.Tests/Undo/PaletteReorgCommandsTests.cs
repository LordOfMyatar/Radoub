using System.Linq;
using Radoub.Formats.Itp;
using Radoub.UI.Services.Palette;
using Radoub.UI.Undo;
using Xunit;

namespace Radoub.UI.Tests.Undo;

/// <summary>
/// Inverse commands for the non-delete palette reorg ops (#2484): blueprint move/file, category
/// add/rename/move. Mirror <see cref="PaletteDeleteCategoryCommandTests"/>: Do applies + snapshots,
/// Undo reverses, Redo (a second Do) returns to the post-op state, and a throwing refresh
/// self-rolls-back and reports false (so the undo stack is never poisoned, #2231).
/// </summary>
public class PaletteReorgCommandsTests
{
    private sealed class FakeGateway : IBlueprintFileGateway
    {
        public byte ReadPaletteId(string filePath) => 0;
        public byte[] ProduceBytesWithPaletteId(string filePath, byte id) => new[] { id };
        public byte ReadPaletteIdFromBytes(byte[] bytes) => bytes[0];
    }

    // root: [Misc(9), Weapons(1){ acid_dagger(id1) }]; loose blueprint is uncategorized (id 0).
    private static (ItpFile itp, LooseFileBlueprintStore store, PaletteCategoryNode misc, PaletteCategoryNode weapons) Build()
    {
        var itp = new ItpFile();
        var misc = new PaletteCategoryNode { Id = 9, Name = "Misc" };
        var weapons = new PaletteCategoryNode { Id = 1, Name = "Weapons" };
        weapons.Blueprints.Add(new PaletteBlueprintNode { ResRef = "acid_dagger" });
        itp.MainNodes.Add(misc);
        itp.MainNodes.Add(weapons);

        var store = new LooseFileBlueprintStore(new FakeGateway(), new[]
        {
            ("acid_dagger", "p/acid_dagger.uti"),
            ("loose", "p/loose.uti"),
        });
        store.SetPaletteId("acid_dagger", 1); // in sync with Weapons
        // "loose" stays at id 0 -> uncategorized
        return (itp, store, misc, weapons);
    }

    // ---- MoveBlueprintCommand (move + file-from-uncategorized via PaletteID) ----

    [Fact]
    public void MoveBlueprint_Do_stages_new_palette_id()
    {
        var (itp, store, misc, _) = Build();
        var cmd = new PaletteMoveBlueprintCommand(store, "acid_dagger", misc.Id, onChanged: null);

        Assert.True(cmd.Do());
        Assert.Equal((byte)9, store.GetPaletteId("acid_dagger"));
        Assert.Equal(PalettePlacementKind.InSync, PaletteReorgMutator.Classify(itp, store, "acid_dagger").Home is { } h && h.Id == 9 ? PalettePlacementKind.InSync : PalettePlacementKind.Uncategorized);
    }

    [Fact]
    public void MoveBlueprint_Undo_restores_original_palette_id()
    {
        var (_, store, misc, _) = Build();
        var cmd = new PaletteMoveBlueprintCommand(store, "acid_dagger", misc.Id, onChanged: null);
        cmd.Do();
        cmd.Undo();
        Assert.Equal((byte)1, store.GetPaletteId("acid_dagger"));
    }

    [Fact]
    public void MoveBlueprint_files_uncategorized_blueprint_and_undo_returns_it_to_uncategorized()
    {
        var (itp, store, _, weapons) = Build();
        var cmd = new PaletteMoveBlueprintCommand(store, "loose", weapons.Id, onChanged: null);

        Assert.True(cmd.Do());
        Assert.Equal((byte)1, store.GetPaletteId("loose"));

        cmd.Undo();
        Assert.Equal((byte)0, store.GetPaletteId("loose"));
        Assert.Equal(PalettePlacementKind.Uncategorized, PaletteReorgMutator.Classify(itp, store, "loose").Kind);
    }

    [Fact]
    public void MoveBlueprint_Do_Undo_Redo_returns_to_moved_state()
    {
        var (_, store, misc, _) = Build();
        var cmd = new PaletteMoveBlueprintCommand(store, "acid_dagger", misc.Id, onChanged: null);
        cmd.Do();
        cmd.Undo();
        cmd.Do(); // redo
        Assert.Equal((byte)9, store.GetPaletteId("acid_dagger"));
    }

    [Fact]
    public void MoveBlueprint_Do_returns_false_and_rolls_back_when_refresh_throws()
    {
        var (_, store, misc, _) = Build();
        var cmd = new PaletteMoveBlueprintCommand(store, "acid_dagger", misc.Id,
            onChanged: () => throw new System.InvalidOperationException());

        Assert.False(cmd.Do());
        Assert.Equal((byte)1, store.GetPaletteId("acid_dagger")); // unchanged
    }

    [Fact]
    public void MoveBlueprint_Do_returns_false_when_already_in_target()
    {
        var (_, store, _, weapons) = Build();
        var cmd = new PaletteMoveBlueprintCommand(store, "acid_dagger", weapons.Id, onChanged: null);
        Assert.False(cmd.Do()); // already id 1
    }

    // ---- RenameCategoryCommand ----

    [Fact]
    public void Rename_Do_sets_name_and_clears_strref()
    {
        var (_, store, _, weapons) = Build();
        weapons.StrRef = 5432; // standard category with a TLK name
        var cmd = new PaletteRenameCategoryCommand(weapons, "Blades", onChanged: null);

        Assert.True(cmd.Do());
        Assert.Equal("Blades", weapons.Name);
        Assert.Null(weapons.StrRef);
    }

    [Fact]
    public void Rename_Undo_restores_name_and_strref()
    {
        var (_, _, _, weapons) = Build();
        weapons.Name = "Weapons";
        weapons.StrRef = 5432;
        var cmd = new PaletteRenameCategoryCommand(weapons, "Blades", onChanged: null);
        cmd.Do();
        cmd.Undo();
        Assert.Equal("Weapons", weapons.Name);
        Assert.Equal((uint)5432, weapons.StrRef);
    }

    [Fact]
    public void Rename_Do_returns_false_on_empty_name()
    {
        var (_, _, _, weapons) = Build();
        var cmd = new PaletteRenameCategoryCommand(weapons, "   ", onChanged: null);
        Assert.False(cmd.Do());
    }

    [Fact]
    public void Rename_Do_returns_false_and_rolls_back_when_refresh_throws()
    {
        var (_, _, _, weapons) = Build();
        var cmd = new PaletteRenameCategoryCommand(weapons, "Blades",
            onChanged: () => throw new System.InvalidOperationException());
        Assert.False(cmd.Do());
        Assert.Equal("Weapons", weapons.Name);
    }

    // ---- AddCategoryCommand ----

    [Fact]
    public void Add_Do_creates_category_under_parent()
    {
        var (itp, _, _, weapons) = Build();
        var cmd = new PaletteAddCategoryCommand(itp, weapons, "Melee", onChanged: null);

        Assert.True(cmd.Do());
        Assert.Contains(weapons.Children.OfType<PaletteCategoryNode>(), c => c.Name == "Melee");
    }

    [Fact]
    public void Add_Undo_removes_the_created_category()
    {
        var (itp, _, _, weapons) = Build();
        var cmd = new PaletteAddCategoryCommand(itp, weapons, "Melee", onChanged: null);
        cmd.Do();
        cmd.Undo();
        Assert.DoesNotContain(weapons.Children.OfType<PaletteCategoryNode>(), c => c.Name == "Melee");
    }

    [Fact]
    public void Add_Do_Undo_Redo_reuses_same_id_and_node()
    {
        var (itp, _, _, weapons) = Build();
        var cmd = new PaletteAddCategoryCommand(itp, weapons, "Melee", onChanged: null);
        cmd.Do();
        var created = weapons.Children.OfType<PaletteCategoryNode>().Single(c => c.Name == "Melee");
        byte firstId = created.Id;
        cmd.Undo();
        cmd.Do(); // redo must NOT allocate a new id

        var again = weapons.Children.OfType<PaletteCategoryNode>().Single(c => c.Name == "Melee");
        Assert.Same(created, again);
        Assert.Equal(firstId, again.Id);
    }

    [Fact]
    public void Add_Do_returns_false_on_empty_name()
    {
        var (itp, _, _, weapons) = Build();
        var cmd = new PaletteAddCategoryCommand(itp, weapons, "  ", onChanged: null);
        Assert.False(cmd.Do());
    }

    // ---- MoveCategoryCommand ----

    [Fact]
    public void MoveCategory_Do_reparents_then_Undo_restores_position()
    {
        // Move Misc(9) under Weapons(1); undo returns it to root index 0.
        var (itp, _, misc, weapons) = Build();
        var cmd = new PaletteMoveCategoryCommand(itp, misc, weapons, index: 0, onChanged: null);

        Assert.True(cmd.Do());
        Assert.Contains(weapons.Children, n => ReferenceEquals(n, misc));
        Assert.DoesNotContain(itp.MainNodes, n => ReferenceEquals(n, misc));

        cmd.Undo();
        Assert.Same(misc, itp.MainNodes[0]);
        Assert.DoesNotContain(weapons.Children, n => ReferenceEquals(n, misc));
    }

    [Fact]
    public void MoveCategory_Do_returns_false_on_cycle()
    {
        // Nesting Weapons into itself is a cycle -> refused.
        var (itp, _, _, weapons) = Build();
        var cmd = new PaletteMoveCategoryCommand(itp, weapons, weapons, index: 0, onChanged: null);
        Assert.False(cmd.Do());
    }

    [Fact]
    public void MoveCategory_Do_Undo_Redo_returns_to_moved_state()
    {
        var (itp, _, misc, weapons) = Build();
        var cmd = new PaletteMoveCategoryCommand(itp, misc, weapons, index: 0, onChanged: null);
        cmd.Do();
        cmd.Undo();
        cmd.Do(); // redo
        Assert.Contains(weapons.Children, n => ReferenceEquals(n, misc));
    }

    [Fact]
    public void MoveCategory_Do_returns_false_and_rolls_back_when_refresh_throws()
    {
        var (itp, _, misc, weapons) = Build();
        var cmd = new PaletteMoveCategoryCommand(itp, misc, weapons, index: 0,
            onChanged: () => throw new System.InvalidOperationException());
        Assert.False(cmd.Do());
        Assert.Same(misc, itp.MainNodes[0]); // rolled back to root
    }
}
