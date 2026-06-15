using System.Collections.Generic;
using System.Linq;
using Radoub.Formats.Itp;
using Radoub.UI.Services.Palette;
using Radoub.UI.ViewModels;
using Xunit;

namespace Radoub.UI.Tests.ViewModels;

/// <summary>
/// Logic tests for <see cref="PaletteEditorViewModel"/> (#2476, Milestone 2). The VM holds
/// the working <see cref="ItpFile"/> and the blueprint pool, exposes reorg ops, the
/// Uncategorized projection, and drift queries. No AXAML / FlaUI here — that is Milestone 3.
/// </summary>
public class PaletteEditorViewModelTests
{
    private sealed class FakeStore : IBlueprintPaletteStore
    {
        private readonly Dictionary<string, byte> _ids = new(System.StringComparer.OrdinalIgnoreCase);
        public FakeStore(params (string ResRef, byte Id)[] seed)
        {
            foreach (var (r, i) in seed) _ids[r] = i;
        }
        public byte? GetPaletteId(string resRef) => _ids.TryGetValue(resRef, out var v) ? v : (byte?)null;
        public bool SetPaletteId(string resRef, byte paletteId)
        {
            if (!_ids.ContainsKey(resRef)) return false;
            _ids[resRef] = paletteId; return true;
        }
        public bool Contains(string resRef) => _ids.ContainsKey(resRef);
        public IReadOnlyCollection<string> ResRefs => _ids.Keys;
    }

    private static PaletteBlueprintNode Bp(string r) => new() { ResRef = r };
    private static PaletteCategoryNode Cat(byte id, string name, params PaletteBlueprintNode[] bps)
    {
        var c = new PaletteCategoryNode { Id = id, Name = name };
        c.Blueprints.AddRange(bps);
        return c;
    }

    private static (ItpFile, FakeStore) Setup()
    {
        var itp = new ItpFile();
        itp.MainNodes.Add(Cat(1, "Weapons", Bp("wpn_sword")));
        itp.MainNodes.Add(Cat(2, "Armor"));
        // pool has the listed sword plus a loose unfiled blueprint
        var store = new FakeStore(("wpn_sword", 1), ("loose_ring", 7));
        return (itp, store);
    }

    [Fact]
    public void MoveBlueprint_AppliesDualWriteAndMarksDirty()
    {
        var (itp, store) = Setup();
        var vm = new PaletteEditorViewModel(itp, store);
        var weapons = itp.GetCategories().First(c => c.Id == 1);
        var armor = itp.GetCategories().First(c => c.Id == 2);

        bool ok = vm.MoveBlueprint("wpn_sword", weapons, armor);

        Assert.True(ok);
        Assert.True(vm.IsDirty);
        Assert.Equal((byte)2, store.GetPaletteId("wpn_sword"));
    }

    [Fact]
    public void NewViewModel_IsNotDirty()
    {
        var (itp, store) = Setup();
        var vm = new PaletteEditorViewModel(itp, store);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void Uncategorized_ListsPoolBlueprintsNotInTree()
    {
        var (itp, store) = Setup();
        var vm = new PaletteEditorViewModel(itp, store);

        var uncategorized = vm.GetUncategorized().ToList();

        // loose_ring is in the pool but nowhere in the tree -> uncategorized;
        // wpn_sword is filed -> not uncategorized.
        Assert.Contains("loose_ring", uncategorized);
        Assert.DoesNotContain("wpn_sword", uncategorized);
    }

    [Fact]
    public void RefreshFailure_RollsBackModelAndStaysClean()
    {
        var (itp, store) = Setup();
        // refresh callback throws -> mutate-refresh-rollback must restore the tree
        var vm = new PaletteEditorViewModel(itp, store, onTreeChanged: () => throw new System.InvalidOperationException("render boom"));
        var weapons = itp.GetCategories().First(c => c.Id == 1);
        var armor = itp.GetCategories().First(c => c.Id == 2);

        bool ok = vm.MoveBlueprint("wpn_sword", weapons, armor);

        Assert.False(ok);
        Assert.Contains(weapons.Blueprints, b => b.ResRef == "wpn_sword"); // rolled back
        Assert.Empty(armor.Blueprints);
        Assert.Equal((byte)1, store.GetPaletteId("wpn_sword"));            // PaletteID rolled back too
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void AddCategory_AddsToTreeAndMarksDirty()
    {
        var (itp, store) = Setup();
        var vm = new PaletteEditorViewModel(itp, store);

        var created = vm.AddCategory(null, "Potions");

        Assert.NotNull(created);
        Assert.True(vm.IsDirty);
        Assert.Contains(created, itp.MainNodes);
    }

    [Fact]
    public void Classify_PlacesByPaletteId_IgnoringStaleTreeListing()
    {
        // sword is (stale-ly) listed under cat 1 in the tree, but its own PaletteID is 99 — which
        // names no live category — so it classifies Uncategorized. The file's PaletteID is
        // authoritative for placement; the tree listing is ignored.
        var itp = new ItpFile();
        itp.MainNodes.Add(Cat(1, "Weapons", Bp("wpn_sword")));
        var store = new FakeStore(("wpn_sword", 99));
        var vm = new PaletteEditorViewModel(itp, store);

        Assert.Equal(PalettePlacementKind.Uncategorized, vm.Classify("wpn_sword").Kind);
    }
}
