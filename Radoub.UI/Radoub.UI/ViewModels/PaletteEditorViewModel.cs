using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Radoub.Formats.Itp;
using Radoub.UI.Services.Palette;

namespace Radoub.UI.ViewModels;

/// <summary>
/// Editing logic for the ITP palette editor (#2476, Milestone 2). Holds the working
/// <see cref="ItpFile"/> tree and the blueprint pool (<see cref="IBlueprintPaletteStore"/>),
/// and exposes the reorganization operations, the read-only Uncategorized projection, and
/// drift classification.
///
/// Each op delegates to the pure <see cref="PaletteReorgMutator"/> and wraps it in the
/// mutate-refresh-rollback pattern (the Relique <c>PropertyListMutator</c> discipline): the
/// model is mutated, a UI refresh callback runs, and if the refresh throws the model change
/// is rolled back so the tree and the view never diverge. Ops return <c>true</c> when applied,
/// <c>false</c> on a no-op or a rolled-back refresh failure.
///
/// This is the logic layer only — the AXAML <c>PaletteEditorControl</c>, drag-drop, the
/// Trebuchet host, and undo/redo wiring are Milestone 3. The save transaction itself lives
/// in <see cref="PaletteSaveTransaction"/>; this VM builds the write set and invokes it.
/// </summary>
public partial class PaletteEditorViewModel : ObservableObject
{
    private readonly ItpFile _itp;
    private readonly IBlueprintPaletteStore _store;
    private readonly Action? _onTreeChanged;

    [ObservableProperty]
    private bool _isDirty;

    /// <param name="itp">The working palette tree (already read from the loose <c>*palcus.itp</c>).</param>
    /// <param name="store">The blueprint pool — the loose module files of the selected type.</param>
    /// <param name="onTreeChanged">
    /// Optional UI refresh invoked after every successful mutation. If it throws, the mutation
    /// is rolled back (mutate-refresh-rollback). Null in headless/logic use.
    /// </param>
    public PaletteEditorViewModel(ItpFile itp, IBlueprintPaletteStore store, Action? onTreeChanged = null)
    {
        _itp = itp ?? throw new ArgumentNullException(nameof(itp));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _onTreeChanged = onTreeChanged;
    }

    /// <summary>The working palette tree.</summary>
    public ItpFile Palette => _itp;

    /// <summary>The blueprint pool.</summary>
    public IBlueprintPaletteStore Pool => _store;

    // ---- reorg ops (mutate -> refresh -> rollback) ---------------------------

    /// <summary>Move a blueprint between categories (dual write). See <see cref="PaletteReorgMutator.MoveBlueprint"/>.</summary>
    public bool MoveBlueprint(string resRef, PaletteCategoryNode from, PaletteCategoryNode to)
    {
        if (from == null) throw new ArgumentNullException(nameof(from));
        if (to == null) throw new ArgumentNullException(nameof(to));

        byte? originalId = _store.GetPaletteId(resRef);
        if (!PaletteReorgMutator.MoveBlueprint(_itp, _store, resRef, from, to))
            return false;

        return CommitOrRollback(() =>
        {
            // Inverse of the dual write: move the entry back and restore the PaletteID.
            PaletteReorgMutator.MoveBlueprint(_itp, _store, resRef, to, from);
            if (originalId is byte id) _store.SetPaletteId(resRef, id);
        });
    }

    /// <summary>
    /// File an uncategorized blueprint into <paramref name="to"/>: add a tree entry and stage the
    /// blueprint's <c>PaletteID</c> to <paramref name="to"/>'s Id (add-only dual write). Unlike
    /// <see cref="MoveBlueprint"/> there is no source category. No-op (false) if the blueprint is
    /// not in the pool or is already listed somewhere in the tree (use <see cref="MoveBlueprint"/>
    /// to recategorize a listed one). Rolls back the tree entry and the staged id if the refresh throws.
    /// </summary>
    public bool FileBlueprint(string resRef, PaletteCategoryNode to)
    {
        if (to == null) throw new ArgumentNullException(nameof(to));
        if (string.IsNullOrEmpty(resRef) || !_store.Contains(resRef)) return false;

        // Add-only: refuse if the blueprint is already listed in the tree. Adding a second tree
        // entry would silently corrupt the palette.
        if (PaletteReorgMutator.Classify(_itp, _store, resRef).Kind != PalettePlacementKind.Uncategorized)
            return false;

        byte? originalId = _store.GetPaletteId(resRef);
        var entry = new PaletteBlueprintNode { ResRef = resRef };
        to.Blueprints.Add(entry);
        if (!_store.SetPaletteId(resRef, to.Id))
        {
            to.Blueprints.Remove(entry);
            return false;
        }

        return CommitOrRollback(() =>
        {
            to.Blueprints.Remove(entry);
            if (originalId is byte id) _store.SetPaletteId(resRef, id);
        });
    }

    /// <summary>
    /// Set a blueprint's category by staging its <c>PaletteID</c> to <paramref name="to"/>'s Id —
    /// the single authoritative write for placement. The <c>.itp</c> tree entry is reconciled from
    /// PaletteIDs at save (<see cref="Services.Palette.PaletteContext"/>), so this op does not touch
    /// the tree directly; display re-derives placement from the new PaletteID on refresh. No-op
    /// (false) if the blueprint is not in the pool or already points at <paramref name="to"/>.
    /// Rolls back the staged id if the refresh throws.
    /// </summary>
    public bool SetBlueprintCategory(string resRef, PaletteCategoryNode to)
    {
        if (to == null) throw new ArgumentNullException(nameof(to));
        if (string.IsNullOrEmpty(resRef) || !_store.Contains(resRef)) return false;

        byte? originalId = _store.GetPaletteId(resRef);
        if (originalId == to.Id) return false; // already there
        if (!_store.SetPaletteId(resRef, to.Id)) return false;

        return CommitOrRollback(() =>
        {
            if (originalId is byte id) _store.SetPaletteId(resRef, id);
        });
    }

    /// <summary>Move/nest a category. See <see cref="PaletteReorgMutator.MoveCategory"/>.</summary>
    public bool MoveCategory(PaletteCategoryNode cat, PaletteNode? newParent, int index)
    {
        // Capture the current location so a refresh failure can put it back exactly.
        var (oldParent, oldIndex) = LocateChild(cat);
        if (oldIndex < 0) return false;

        if (!PaletteReorgMutator.MoveCategory(_itp, cat, newParent, index))
            return false;

        return CommitOrRollback(() =>
            PaletteReorgMutator.MoveCategory(_itp, cat, oldParent, oldIndex));
    }

    /// <summary>Add a new empty category. See <see cref="PaletteReorgMutator.AddCategory"/>.</summary>
    public PaletteCategoryNode? AddCategory(PaletteNode? parent, string name)
    {
        var created = PaletteReorgMutator.AddCategory(_itp, parent, name);
        if (created == null) return null;

        bool ok = CommitOrRollback(() =>
        {
            (ChildListOf(parent) ?? _itp.MainNodes).Remove(created);
        });
        return ok ? created : null;
    }

    /// <summary>Rename a category. See <see cref="PaletteReorgMutator.RenameCategory"/>.</summary>
    public bool RenameCategory(PaletteCategoryNode cat, string newName)
    {
        if (cat == null) throw new ArgumentNullException(nameof(cat));
        string oldName = cat.Name ?? string.Empty;
        uint? oldStrRef = cat.StrRef;

        if (!PaletteReorgMutator.RenameCategory(cat, newName))
            return false;

        return CommitOrRollback(() => { cat.Name = oldName; cat.StrRef = oldStrRef; });
    }

    /// <summary>Reorder a child within its parent. See <see cref="PaletteReorgMutator.ReorderWithin"/>.</summary>
    public bool ReorderWithin(PaletteNode? parent, int oldIndex, int newIndex)
    {
        if (!PaletteReorgMutator.ReorderWithin(_itp, parent, oldIndex, newIndex))
            return false;

        return CommitOrRollback(() =>
            PaletteReorgMutator.ReorderWithin(_itp, parent, newIndex, oldIndex));
    }

    // RemoveCategory is intentionally not exposed as a one-shot rollback op here: its
    // reparenting touches many nodes and PaletteIDs, so its undo belongs to the Milestone 3
    // undo/redo wiring (#2231). It is fully covered as a pure op in PaletteReorgMutator.

    // ---- projections ---------------------------------------------------------

    /// <summary>
    /// Pool blueprints with no usable placement (not listed anywhere in the tree). This is the
    /// view-only Uncategorized bucket — never written to the <c>.itp</c> as a real category.
    /// </summary>
    public IEnumerable<string> GetUncategorized()
        => _store.ResRefs.Where(r =>
            PaletteReorgMutator.Classify(_itp, _store, r).Kind == PalettePlacementKind.Uncategorized);

    /// <summary>Classify a pool blueprint against the tree (drift/uncategorized/in-sync).</summary>
    public PalettePlacement Classify(string resRef) => PaletteReorgMutator.Classify(_itp, _store, resRef);

    // ---- internals -----------------------------------------------------------

    /// <summary>
    /// Run the refresh callback after a mutation. On success mark dirty and return true; if the
    /// refresh throws, run <paramref name="rollback"/> to undo the model change and return false
    /// (dirty state unchanged).
    /// </summary>
    private bool CommitOrRollback(Action rollback)
    {
        try
        {
            _onTreeChanged?.Invoke();
            IsDirty = true;
            return true;
        }
        catch
        {
            try { rollback(); } catch { /* best-effort: rollback should not mask the original failure */ }
            return false;
        }
    }

    private (PaletteNode? Parent, int Index) LocateChild(PaletteNode target)
    {
        int rootIdx = _itp.MainNodes.IndexOf(target);
        if (rootIdx >= 0) return (null, rootIdx);
        return LocateChildIn(_itp.MainNodes, target);
    }

    private (PaletteNode? Parent, int Index) LocateChildIn(List<PaletteNode> nodes, PaletteNode target)
    {
        foreach (var node in nodes)
        {
            var children = ChildListOf(node);
            if (children == null) continue;
            int idx = children.IndexOf(target);
            if (idx >= 0) return (node, idx);
            var found = LocateChildIn(children, target);
            if (found.Index >= 0) return found;
        }
        return (null, -1);
    }

    private static List<PaletteNode>? ChildListOf(PaletteNode? parent) => parent switch
    {
        PaletteCategoryNode cat => cat.Children,
        PaletteBranchNode br => br.Children,
        _ => null,
    };
}
