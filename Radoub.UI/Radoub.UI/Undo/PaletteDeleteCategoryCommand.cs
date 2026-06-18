using System;
using System.Collections.Generic;
using System.Linq;
using Radoub.Formats.Itp;
using Radoub.UI.Services.Palette;

namespace Radoub.UI.Undo;

/// <summary>
/// Reversible delete-with-reparent for a palette category (#2477, M3). The pure
/// <see cref="PaletteReorgMutator.RemoveCategory"/> reparents children to the deleted category's
/// parent and moves blueprints to Uncategorized (staging their PaletteID to the retired Id) — none
/// of which a simple inverse can restore. This command snapshots the full pre-delete state inside
/// <see cref="Do"/> (so Redo re-snapshots correctly) and reconstructs it on <see cref="Undo"/>.
/// The refresh callback runs after both Do and Undo, with mutate-refresh-rollback on Do.
/// </summary>
public sealed class PaletteDeleteCategoryCommand : IUndoableCommand
{
    private readonly ItpFile _itp;
    private readonly IBlueprintPaletteStore _store;
    private readonly PaletteCategoryNode _category;
    private readonly Action? _onChanged;

    // Pre-delete snapshot, captured in Do().
    private List<PaletteNode>? _parentList;
    private int _index = -1;
    private List<PaletteNode>? _children;
    private List<PaletteBlueprintNode>? _blueprints;
    private Dictionary<string, byte>? _blueprintIds;

    public PaletteDeleteCategoryCommand(
        ItpFile itp, IBlueprintPaletteStore store, PaletteCategoryNode category, Action? onChanged)
    {
        _itp = itp ?? throw new ArgumentNullException(nameof(itp));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _category = category ?? throw new ArgumentNullException(nameof(category));
        _onChanged = onChanged;
    }

    public string Description => $"Delete category '{_category.Name ?? _category.Id.ToString()}'";

    public bool Do()
    {
        // Snapshot BEFORE the mutation (Redo re-runs Do, so re-snapshot each time).
        _parentList = FindParentList(_itp.MainNodes, _category);
        if (_parentList == null) return false; // not in tree
        _index = _parentList.IndexOf(_category);
        _children = new List<PaletteNode>(_category.Children);
        _blueprints = new List<PaletteBlueprintNode>(_category.Blueprints);
        _blueprintIds = _blueprints
            .Where(b => _store.GetPaletteId(b.ResRef) is not null)
            .ToDictionary(b => b.ResRef, b => _store.GetPaletteId(b.ResRef)!.Value, StringComparer.OrdinalIgnoreCase);

        if (!PaletteReorgMutator.RemoveCategory(_itp, _store, _category))
            return false;

        try
        {
            _onChanged?.Invoke();
            return true;
        }
        catch
        {
            try { RestoreSnapshot(); } catch { /* best-effort */ }
            return false;
        }
    }

    public void Undo()
    {
        RestoreSnapshot();
        _onChanged?.Invoke();
    }

    private void RestoreSnapshot()
    {
        if (_parentList == null || _children == null || _blueprints == null || _blueprintIds == null)
            return;

        // RemoveCategory reparented the children into _parentList at _index. Pull them back out so
        // we can re-insert the category in their place.
        foreach (var child in _children)
            _parentList.Remove(child);

        // Restore the category's own contents, then re-insert it at its original index.
        _category.Children.Clear();
        _category.Children.AddRange(_children);
        _category.Blueprints.Clear();
        _category.Blueprints.AddRange(_blueprints);

        int at = Math.Max(0, Math.Min(_index, _parentList.Count));
        _parentList.Insert(at, _category);

        // Re-stage each blueprint's original PaletteID (RemoveCategory had moved them to the retired Id).
        foreach (var kvp in _blueprintIds)
            _store.SetPaletteId(kvp.Key, kvp.Value);
    }

    private static List<PaletteNode>? FindParentList(List<PaletteNode> nodes, PaletteNode target)
    {
        if (nodes.Contains(target)) return nodes;
        foreach (var node in nodes)
        {
            var children = ChildListOf(node);
            if (children == null) continue;
            var found = FindParentList(children, target);
            if (found != null) return found;
        }
        return null;
    }

    private static List<PaletteNode>? ChildListOf(PaletteNode? parent) => parent switch
    {
        PaletteCategoryNode cat => cat.Children,
        PaletteBranchNode br => br.Children,
        _ => null,
    };
}
