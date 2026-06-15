using System;
using System.Collections.Generic;
using System.Linq;
using Radoub.Formats.Itp;

namespace Radoub.UI.Services.Palette;

/// <summary>
/// Pure reorganization core for the ITP palette editor (#2476), mirroring the Relique
/// <c>PropertyListMutator</c> purity pattern. Every operation mutates the in-memory
/// <see cref="ItpFile"/> tree (and, for blueprint-touching ops, stages a matching
/// <c>PaletteID</c> via <see cref="IBlueprintPaletteStore"/>) and never touches disk —
/// the <see cref="PaletteSaveTransaction"/> commits the result atomically.
///
/// Operation contract (from the design spec, each unit-tested):
/// - <see cref="MoveBlueprint"/> is the only blueprint-touching op besides delete-with-reparent;
///   it is a dual write (tree entry + staged <c>PaletteID</c>).
/// - <see cref="MoveCategory"/>/<see cref="AddCategory"/>/<see cref="RenameCategory"/>/
///   <see cref="ReorderWithin"/> are structural-only (they never change a <c>PaletteID</c>).
/// - <see cref="MoveCategory"/> refuses cycles (no nesting a category into itself or a descendant).
/// - Category <c>Id</c>s are preserved on move/reorder and retired (never recycled) on delete;
///   <see cref="AddCategory"/> only ever advances the allocator.
/// - <see cref="RemoveCategory"/> reparents contents (blueprints to Uncategorized, child
///   categories to the deleted category's parent) — never cascade-deletes, never orphans.
///
/// Methods return <c>true</c> when the mutation was applied, <c>false</c> on a no-op
/// (invalid input, blueprint absent from the pool, cycle, etc.). Callers compose
/// mutate-refresh-rollback at the UI layer (see <c>PropertyListMutator</c>).
/// </summary>
public static class PaletteReorgMutator
{
    /// <summary>
    /// Move a blueprint from <paramref name="from"/> to <paramref name="to"/>: remove the tree
    /// entry under <paramref name="from"/>, add it under <paramref name="to"/>, and stage the
    /// blueprint's <c>PaletteID</c> to <paramref name="to"/>'s Id (the dual write). No-op (returns
    /// false, tree untouched) if the blueprint is not in the pool or not under <paramref name="from"/>.
    /// </summary>
    public static bool MoveBlueprint(
        ItpFile itp,
        IBlueprintPaletteStore store,
        string resRef,
        PaletteCategoryNode from,
        PaletteCategoryNode to)
    {
        if (itp == null) throw new ArgumentNullException(nameof(itp));
        if (store == null) throw new ArgumentNullException(nameof(store));
        if (from == null) throw new ArgumentNullException(nameof(from));
        if (to == null) throw new ArgumentNullException(nameof(to));
        if (string.IsNullOrEmpty(resRef)) return false;
        if (!store.Contains(resRef)) return false;

        var entry = from.Blueprints.FirstOrDefault(b =>
            string.Equals(b.ResRef, resRef, StringComparison.OrdinalIgnoreCase));
        if (entry == null) return false;

        from.Blueprints.Remove(entry);
        to.Blueprints.Add(entry);

        // Dual write: stage the blueprint's PaletteID to match its new tree home. If the store
        // refuses (blueprint vanished from the pool mid-op), roll the tree change back.
        if (!store.SetPaletteId(resRef, to.Id))
        {
            to.Blueprints.Remove(entry);
            from.Blueprints.Add(entry);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Move <paramref name="cat"/> to be a child of <paramref name="newParent"/> (or to the MAIN
    /// root when <paramref name="newParent"/> is null) at <paramref name="index"/>. Structural only —
    /// <c>PaletteID</c>s are untouched and the category's <c>Id</c> is preserved. Refused (returns
    /// false) if <paramref name="newParent"/> is <paramref name="cat"/> itself or any descendant of
    /// <paramref name="cat"/> (cycle guard), or if <paramref name="cat"/> is not currently in the tree.
    /// </summary>
    public static bool MoveCategory(ItpFile itp, PaletteCategoryNode cat, PaletteNode? newParent, int index)
    {
        if (itp == null) throw new ArgumentNullException(nameof(itp));
        if (cat == null) throw new ArgumentNullException(nameof(cat));

        // Cycle guard: a category may not be reparented into itself or one of its descendants.
        if (ReferenceEquals(newParent, cat)) return false;
        if (newParent is PaletteNode np && IsDescendantOf(cat, np)) return false;

        var currentParentList = FindParentList(itp, cat);
        if (currentParentList == null) return false; // not in tree

        var targetList = ChildListOf(newParent) ?? itp.MainNodes;

        // Clamp index into the target list (after removal if same list).
        currentParentList.Remove(cat);
        int clamped = Math.Max(0, Math.Min(index, targetList.Count));
        targetList.Insert(clamped, cat);
        return true;
    }

    /// <summary>
    /// Add a new empty category under <paramref name="parent"/> (or MAIN root when null) with the
    /// given <paramref name="name"/>. Its <c>Id</c> is drawn from the allocator (always advancing,
    /// never reusing a retired Id). Returns the created node, or null on invalid input.
    /// </summary>
    public static PaletteCategoryNode? AddCategory(ItpFile itp, PaletteNode? parent, string name)
    {
        if (itp == null) throw new ArgumentNullException(nameof(itp));
        if (string.IsNullOrWhiteSpace(name)) return null;

        byte id = AllocateCategoryId(itp);
        var node = new PaletteCategoryNode { Id = id, Name = name };

        var targetList = ChildListOf(parent) ?? itp.MainNodes;
        targetList.Add(node);

        // Advance the allocator so this Id is never recycled (#2476 ID retirement).
        itp.NextUseableId = (byte)(id + 1);
        return node;
    }

    /// <summary>
    /// Rename <paramref name="cat"/>. Structural only; the <c>Id</c> is preserved so blueprint
    /// <c>PaletteID</c> references stay valid. Clears <c>StrRef</c> (a literal name overrides a TLK
    /// reference). Returns false on empty name.
    /// </summary>
    public static bool RenameCategory(PaletteCategoryNode cat, string newName)
    {
        if (cat == null) throw new ArgumentNullException(nameof(cat));
        if (string.IsNullOrWhiteSpace(newName)) return false;

        cat.Name = newName;
        cat.StrRef = null; // literal name now authoritative
        return true;
    }

    /// <summary>
    /// Remove <paramref name="cat"/> from the tree. Contents are reparented, never orphaned or
    /// cascade-deleted: blueprints move to Uncategorized (their tree entry is dropped and their
    /// <c>PaletteID</c> is staged to a nonexistent id so they classify as uncategorized), and child
    /// categories/branches are reparented to <paramref name="cat"/>'s parent. The removed category's
    /// <c>Id</c> is retired (the allocator never recycles it). Returns false if not in the tree.
    /// </summary>
    public static bool RemoveCategory(ItpFile itp, IBlueprintPaletteStore store, PaletteCategoryNode cat)
    {
        if (itp == null) throw new ArgumentNullException(nameof(itp));
        if (store == null) throw new ArgumentNullException(nameof(store));
        if (cat == null) throw new ArgumentNullException(nameof(cat));

        var parentList = FindParentList(itp, cat);
        if (parentList == null) return false;

        int at = parentList.IndexOf(cat);
        parentList.Remove(cat);

        // Reparent child categories/branches to the deleted category's parent, in place.
        for (int i = 0; i < cat.Children.Count; i++)
            parentList.Insert(at + i, cat.Children[i]);
        cat.Children.Clear();

        // Blueprints become Uncategorized: drop the tree entry, stage PaletteID to the deleted
        // (now-retired) Id so the blueprint no longer maps to any live category.
        foreach (var bp in cat.Blueprints)
            store.SetPaletteId(bp.ResRef, cat.Id);
        cat.Blueprints.Clear();

        // Retire the Id — never recycled. Ensure the allocator is past it.
        if (itp.NextUseableId is not byte nid || nid <= cat.Id)
            itp.NextUseableId = (byte)(cat.Id + 1);
        return true;
    }

    /// <summary>
    /// Reorder a child within <paramref name="parent"/> (or MAIN root when null) from
    /// <paramref name="oldIndex"/> to <paramref name="newIndex"/>. Structural only. Returns false on
    /// out-of-range / no-op indices.
    /// </summary>
    public static bool ReorderWithin(ItpFile itp, PaletteNode? parent, int oldIndex, int newIndex)
    {
        if (itp == null) throw new ArgumentNullException(nameof(itp));
        var list = ChildListOf(parent) ?? itp.MainNodes;

        if (oldIndex < 0 || oldIndex >= list.Count) return false;
        if (newIndex < 0 || newIndex >= list.Count) return false;
        if (oldIndex == newIndex) return false;

        var node = list[oldIndex];
        list.RemoveAt(oldIndex);
        list.Insert(newIndex, node);
        return true;
    }

    /// <summary>
    /// Classify a blueprint against the loaded tree (display rule: the tree wins).
    /// </summary>
    public static PalettePlacement Classify(ItpFile itp, IBlueprintPaletteStore store, string resRef)
    {
        if (itp == null) throw new ArgumentNullException(nameof(itp));
        if (store == null) throw new ArgumentNullException(nameof(store));

        var home = itp.GetCategories()
            .FirstOrDefault(c => c.Blueprints.Any(b =>
                string.Equals(b.ResRef, resRef, StringComparison.OrdinalIgnoreCase)));

        // Not listed anywhere = not filed, regardless of what its PaletteID claims.
        if (home == null)
            return new PalettePlacement(PalettePlacementKind.Uncategorized, null);

        var id = store.GetPaletteId(resRef);
        // Listed under a category, but the blueprint's own PaletteID disagrees -> drifted.
        if (id != home.Id)
            return new PalettePlacement(PalettePlacementKind.Drifted, home);

        return new PalettePlacement(PalettePlacementKind.InSync, home);
    }

    // ---- helpers -------------------------------------------------------------

    /// <summary>The byte Id the next new category should take. Advances past every existing Id
    /// and past <c>NextUseableId</c>; never reuses a value (retired ids stay retired).</summary>
    private static byte AllocateCategoryId(ItpFile itp)
    {
        int max = -1;
        foreach (var c in itp.GetCategories())
            if (c.Id > max) max = c.Id;
        if (itp.NextUseableId is byte nid && nid - 1 > max) max = nid - 1;
        return (byte)Math.Min(max + 1, byte.MaxValue);
    }

    /// <summary>The mutable child list a node parents (category Children, branch Children),
    /// or null for a leaf/blueprint (caller substitutes MAIN root).</summary>
    private static List<PaletteNode>? ChildListOf(PaletteNode? parent) => parent switch
    {
        PaletteCategoryNode cat => cat.Children,
        PaletteBranchNode br => br.Children,
        _ => null,
    };

    /// <summary>The list that currently holds <paramref name="target"/> (MAIN root or any node's
    /// Children), or null if it is not in the tree.</summary>
    private static List<PaletteNode>? FindParentList(ItpFile itp, PaletteNode target)
    {
        if (itp.MainNodes.Contains(target)) return itp.MainNodes;
        return FindParentListIn(itp.MainNodes, target);
    }

    private static List<PaletteNode>? FindParentListIn(List<PaletteNode> nodes, PaletteNode target)
    {
        foreach (var node in nodes)
        {
            var children = ChildListOf(node);
            if (children == null) continue;
            if (children.Contains(target)) return children;
            var found = FindParentListIn(children, target);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>True if <paramref name="candidate"/> is <paramref name="cat"/> or appears anywhere
    /// in <paramref name="cat"/>'s descendant subtree (cycle-guard ancestor check).</summary>
    private static bool IsDescendantOf(PaletteCategoryNode cat, PaletteNode candidate)
    {
        foreach (var child in cat.Children)
        {
            if (ReferenceEquals(child, candidate)) return true;
            if (ChildListOf(child) is { } grand && IsDescendantOf(grand, candidate)) return true;
        }
        return false;
    }

    private static bool IsDescendantOf(List<PaletteNode> nodes, PaletteNode candidate)
    {
        foreach (var child in nodes)
        {
            if (ReferenceEquals(child, candidate)) return true;
            if (ChildListOf(child) is { } grand && IsDescendantOf(grand, candidate)) return true;
        }
        return false;
    }
}

/// <summary>How a blueprint relates to the loaded palette tree.</summary>
public enum PalettePlacementKind
{
    /// <summary>Listed under a category whose Id matches the blueprint's PaletteID.</summary>
    InSync,
    /// <summary>Listed under a category, but the blueprint's PaletteID names a different id.</summary>
    Drifted,
    /// <summary>Not listed anywhere in the tree (regardless of PaletteID).</summary>
    Uncategorized,
}

/// <summary>The result of classifying a blueprint: its kind and (for listed blueprints) its
/// tree home category.</summary>
public readonly record struct PalettePlacement(PalettePlacementKind Kind, PaletteCategoryNode? Home);
