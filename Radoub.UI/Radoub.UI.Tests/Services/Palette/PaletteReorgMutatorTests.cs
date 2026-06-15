using System.Collections.Generic;
using System.Linq;
using Radoub.Formats.Itp;
using Radoub.UI.Services.Palette;
using Xunit;

namespace Radoub.UI.Tests.Services.Palette;

/// <summary>
/// Pure unit tests for <see cref="PaletteReorgMutator"/> — every reorg op, no FlaUI (#2476).
/// </summary>
public class PaletteReorgMutatorTests
{
    /// <summary>In-memory blueprint pool standing in for the four concrete formats.</summary>
    private sealed class FakeBlueprintStore : IBlueprintPaletteStore
    {
        private readonly Dictionary<string, byte> _ids = new(System.StringComparer.OrdinalIgnoreCase);
        public FakeBlueprintStore(params (string ResRef, byte Id)[] seed)
        {
            foreach (var (resRef, id) in seed) _ids[resRef] = id;
        }
        public byte? GetPaletteId(string resRef) => _ids.TryGetValue(resRef, out var v) ? v : (byte?)null;
        public bool SetPaletteId(string resRef, byte paletteId)
        {
            if (!_ids.ContainsKey(resRef)) return false;
            _ids[resRef] = paletteId;
            return true;
        }
        public bool Contains(string resRef) => _ids.ContainsKey(resRef);
    }

    private static PaletteBlueprintNode Bp(string resRef) => new() { ResRef = resRef };

    private static PaletteCategoryNode Cat(byte id, string name, params PaletteBlueprintNode[] bps)
    {
        var c = new PaletteCategoryNode { Id = id, Name = name };
        c.Blueprints.AddRange(bps);
        return c;
    }

    // Tree: MAIN -> [ Weapons(id1){ wpn_sword }, Armor(id2){ } ]
    private static ItpFile TwoCategoryTree()
    {
        var itp = new ItpFile();
        itp.MainNodes.Add(Cat(1, "Weapons", Bp("wpn_sword")));
        itp.MainNodes.Add(Cat(2, "Armor"));
        return itp;
    }

    [Fact]
    public void MoveBlueprint_RelocatesTreeEntryAndStagesPaletteId()
    {
        var itp = TwoCategoryTree();
        var store = new FakeBlueprintStore(("wpn_sword", 1));
        var weapons = itp.GetCategories().First(c => c.Id == 1);
        var armor = itp.GetCategories().First(c => c.Id == 2);

        bool ok = PaletteReorgMutator.MoveBlueprint(itp, store, "wpn_sword", weapons, armor);

        Assert.True(ok);
        Assert.DoesNotContain(weapons.Blueprints, b => b.ResRef == "wpn_sword");
        Assert.Contains(armor.Blueprints, b => b.ResRef == "wpn_sword");
        Assert.Equal((byte)2, store.GetPaletteId("wpn_sword")); // dual write half
    }

    [Fact]
    public void MoveBlueprint_BlueprintNotInPool_DoesNotMutateTree()
    {
        var itp = TwoCategoryTree();
        var store = new FakeBlueprintStore(); // empty pool
        var weapons = itp.GetCategories().First(c => c.Id == 1);
        var armor = itp.GetCategories().First(c => c.Id == 2);

        bool ok = PaletteReorgMutator.MoveBlueprint(itp, store, "wpn_sword", weapons, armor);

        Assert.False(ok);
        Assert.Contains(weapons.Blueprints, b => b.ResRef == "wpn_sword"); // unchanged
        Assert.Empty(armor.Blueprints);
    }

    // ---- MoveCategory: cycle guard + ID preservation -------------------------

    // parent(id10) -> child(id11) -> grandchild(id12)
    private static ItpFile NestedCategoryTree()
    {
        var grand = Cat(12, "Grand");
        var child = Cat(11, "Child");
        child.Children.Add(grand);
        var parent = Cat(10, "Parent");
        parent.Children.Add(child);
        var itp = new ItpFile();
        itp.MainNodes.Add(parent);
        return itp;
    }

    [Fact]
    public void MoveCategory_IntoOwnDescendant_IsRefused()
    {
        var itp = NestedCategoryTree();
        var parent = itp.GetCategories().First(c => c.Id == 10);
        var grand = itp.GetCategories().First(c => c.Id == 12);

        bool ok = PaletteReorgMutator.MoveCategory(itp, parent, grand, 0);

        Assert.False(ok);
        Assert.Contains(parent.Children, n => ReferenceEquals(n, grand) == false); // tree intact
        Assert.Contains(grand, itp.GetCategories().First(c => c.Id == 11).Children);
    }

    [Fact]
    public void MoveCategory_IntoItself_IsRefused()
    {
        var itp = NestedCategoryTree();
        var parent = itp.GetCategories().First(c => c.Id == 10);

        Assert.False(PaletteReorgMutator.MoveCategory(itp, parent, parent, 0));
    }

    [Fact]
    public void MoveCategory_ToNewParent_PreservesIdAndReparents()
    {
        var itp = TwoCategoryTree();      // Weapons(1){sword}, Armor(2)
        var weapons = itp.GetCategories().First(c => c.Id == 1);
        var armor = itp.GetCategories().First(c => c.Id == 2);

        bool ok = PaletteReorgMutator.MoveCategory(itp, armor, weapons, 0);

        Assert.True(ok);
        Assert.Equal((byte)2, armor.Id);                       // id preserved
        Assert.Contains(armor, weapons.Children);              // reparented
        Assert.DoesNotContain(armor, itp.MainNodes);           // off root
        Assert.Contains(armor, itp.GetCategories());           // still reachable
    }

    // ---- AddCategory: ID retirement / always-advance -------------------------

    [Fact]
    public void AddCategory_AllocatesNextIdAndAdvancesAllocator()
    {
        var itp = TwoCategoryTree(); // ids 1,2
        var created = PaletteReorgMutator.AddCategory(itp, null, "Potions");

        Assert.NotNull(created);
        Assert.Equal((byte)3, created!.Id);             // max(1,2)+1
        Assert.Equal((byte)4, itp.NextUseableId);       // advanced past it
        Assert.Contains(created, itp.MainNodes);
    }

    [Fact]
    public void AddCategory_AfterRemove_DoesNotRecycleRetiredId()
    {
        var itp = TwoCategoryTree(); // ids 1,2
        var store = new FakeBlueprintStore(("wpn_sword", 1));
        var armor = itp.GetCategories().First(c => c.Id == 2);

        PaletteReorgMutator.RemoveCategory(itp, store, armor); // retires id 2
        var created = PaletteReorgMutator.AddCategory(itp, null, "Potions");

        Assert.NotNull(created);
        Assert.NotEqual((byte)2, created!.Id);          // retired id not recycled
        Assert.Equal((byte)3, created.Id);
    }

    [Fact]
    public void AddCategory_RespectsExistingNextUseableId()
    {
        var itp = TwoCategoryTree();
        itp.NextUseableId = 50;                          // skeleton-sourced allocator

        var created = PaletteReorgMutator.AddCategory(itp, null, "Potions");

        Assert.Equal((byte)50, created!.Id);
        Assert.Equal((byte)51, itp.NextUseableId);
    }

    // ---- RenameCategory ------------------------------------------------------

    [Fact]
    public void RenameCategory_SetsNamePreservesIdClearsStrRef()
    {
        var cat = Cat(7, "Old");
        cat.StrRef = 1234;

        bool ok = PaletteReorgMutator.RenameCategory(cat, "New");

        Assert.True(ok);
        Assert.Equal("New", cat.Name);
        Assert.Equal((byte)7, cat.Id);
        Assert.Null(cat.StrRef);
    }

    [Fact]
    public void RenameCategory_EmptyName_Refused()
        => Assert.False(PaletteReorgMutator.RenameCategory(Cat(7, "Old"), "   "));

    // ---- RemoveCategory: reparent, never orphan ------------------------------

    [Fact]
    public void RemoveCategory_NonEmpty_ReparentsBlueprintsToUncategorizedAndChildrenToParent()
    {
        // root -> outer(id5){ outer_bp } -> [ inner(id6){ inner_bp } ]
        var inner = Cat(6, "Inner", Bp("inner_bp"));
        var outer = Cat(5, "Outer", Bp("outer_bp"));
        outer.Children.Add(inner);
        var itp = new ItpFile();
        itp.MainNodes.Add(outer);
        var store = new FakeBlueprintStore(("outer_bp", 5), ("inner_bp", 6));

        bool ok = PaletteReorgMutator.RemoveCategory(itp, store, outer);

        Assert.True(ok);
        Assert.DoesNotContain(outer, itp.MainNodes);
        // child category reparented to root (outer's parent)
        Assert.Contains(inner, itp.MainNodes);
        Assert.Contains(inner, itp.GetCategories());
        // outer's blueprint becomes uncategorized: not listed anywhere, PaletteID points at retired id
        Assert.DoesNotContain(itp.GetCategories(), c => c.Blueprints.Any(b => b.ResRef == "outer_bp"));
        var placement = PaletteReorgMutator.Classify(itp, store, "outer_bp");
        Assert.Equal(PalettePlacementKind.Uncategorized, placement.Kind);
        // inner_bp still listed under the reparented inner category, in sync
        Assert.Equal(PalettePlacementKind.InSync, PaletteReorgMutator.Classify(itp, store, "inner_bp").Kind);
    }

    [Fact]
    public void RemoveCategory_NotInTree_Refused()
    {
        var itp = TwoCategoryTree();
        var store = new FakeBlueprintStore();
        Assert.False(PaletteReorgMutator.RemoveCategory(itp, store, Cat(99, "Ghost")));
    }

    // ---- ReorderWithin -------------------------------------------------------

    [Fact]
    public void ReorderWithin_MovesNodeToNewIndex()
    {
        var itp = TwoCategoryTree(); // [Weapons(1), Armor(2)]
        bool ok = PaletteReorgMutator.ReorderWithin(itp, null, 0, 1);

        Assert.True(ok);
        Assert.Equal((byte)2, ((PaletteCategoryNode)itp.MainNodes[0]).Id);
        Assert.Equal((byte)1, ((PaletteCategoryNode)itp.MainNodes[1]).Id);
    }

    [Fact]
    public void ReorderWithin_OutOfRangeOrNoop_Refused()
    {
        var itp = TwoCategoryTree();
        Assert.False(PaletteReorgMutator.ReorderWithin(itp, null, 0, 0));   // no-op
        Assert.False(PaletteReorgMutator.ReorderWithin(itp, null, 5, 0));   // bad old
        Assert.False(PaletteReorgMutator.ReorderWithin(itp, null, 0, 9));   // bad new
    }

    // ---- Classify: drift vs uncategorized ------------------------------------

    [Fact]
    public void Classify_ResRefNowhereInTree_IsUncategorizedEvenWithValidPaletteId()
    {
        var itp = TwoCategoryTree();                       // sword listed under cat 1
        // pool has a blueprint with a valid-looking PaletteID, but it is not in the tree
        var store = new FakeBlueprintStore(("orphan", 1));

        Assert.Equal(PalettePlacementKind.Uncategorized,
            PaletteReorgMutator.Classify(itp, store, "orphan").Kind);
    }

    [Fact]
    public void Classify_ListedButPaletteIdDisagrees_IsDrifted()
    {
        var itp = TwoCategoryTree();                       // sword listed under cat 1
        var store = new FakeBlueprintStore(("wpn_sword", 99)); // PaletteID says 99

        var p = PaletteReorgMutator.Classify(itp, store, "wpn_sword");
        Assert.Equal(PalettePlacementKind.Drifted, p.Kind);
        Assert.Equal((byte)1, p.Home!.Id);                // displayed per tree (tree wins)
    }

    [Fact]
    public void Classify_ListedAndPaletteIdAgrees_IsInSync()
    {
        var itp = TwoCategoryTree();
        var store = new FakeBlueprintStore(("wpn_sword", 1));

        Assert.Equal(PalettePlacementKind.InSync,
            PaletteReorgMutator.Classify(itp, store, "wpn_sword").Kind);
    }
}
