using System;
using Radoub.Formats.Itp;
using Radoub.UI.Services.Palette;

namespace Radoub.UI.Undo;

// Inverse commands for the non-delete palette reorg ops (#2484), companions to
// PaletteDeleteCategoryCommand. Each owns its mutate via the pure PaletteReorgMutator, runs the
// injected refresh with mutate-refresh-rollback on Do (so a throwing refresh self-rolls-back and
// reports false — the undo stack is never poisoned, #2231), and reverses on Undo. Redo (a second
// Do) returns to the post-op state.

/// <summary>
/// Reversible blueprint placement change (move between categories, or file an uncategorized one).
/// Placement is authoritative on the blueprint's <c>PaletteID</c> (#2477), so this stages the new
/// id on Do and the captured original id on Undo; the tree reconciles from PaletteIDs on refresh.
/// </summary>
public sealed class PaletteMoveBlueprintCommand : IUndoableCommand
{
    private readonly IBlueprintPaletteStore _store;
    private readonly string _resRef;
    private readonly byte _targetId;
    private readonly Action? _onChanged;
    private byte? _originalId;

    public PaletteMoveBlueprintCommand(IBlueprintPaletteStore store, string resRef, byte targetId, Action? onChanged)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _resRef = resRef ?? throw new ArgumentNullException(nameof(resRef));
        _targetId = targetId;
        _onChanged = onChanged;
    }

    public string Description => $"Move '{_resRef}'";

    public bool Do()
    {
        if (string.IsNullOrEmpty(_resRef) || !_store.Contains(_resRef)) return false;

        _originalId = _store.GetPaletteId(_resRef); // re-snapshot each Do (Redo re-runs)
        if (_originalId == _targetId) return false;  // already there — no-op
        if (!_store.SetPaletteId(_resRef, _targetId)) return false;

        try
        {
            _onChanged?.Invoke();
            return true;
        }
        catch
        {
            try { if (_originalId is byte id) _store.SetPaletteId(_resRef, id); } catch { /* best-effort */ }
            return false;
        }
    }

    public void Undo()
    {
        if (_originalId is byte id) _store.SetPaletteId(_resRef, id);
        _onChanged?.Invoke();
    }
}

/// <summary>Reversible category rename. Captures the old literal name and StrRef; the rename clears
/// StrRef (a literal name overrides a TLK reference), and Undo restores both.</summary>
public sealed class PaletteRenameCategoryCommand : IUndoableCommand
{
    private readonly PaletteCategoryNode _cat;
    private readonly string _newName;
    private readonly Action? _onChanged;
    private string _oldName = string.Empty;
    private uint? _oldStrRef;

    public PaletteRenameCategoryCommand(PaletteCategoryNode cat, string newName, Action? onChanged)
    {
        _cat = cat ?? throw new ArgumentNullException(nameof(cat));
        _newName = newName;
        _onChanged = onChanged;
    }

    public string Description => $"Rename category to '{_newName}'";

    public bool Do()
    {
        _oldName = _cat.Name ?? string.Empty;   // re-snapshot each Do
        _oldStrRef = _cat.StrRef;

        if (!PaletteReorgMutator.RenameCategory(_cat, _newName)) return false;

        try
        {
            _onChanged?.Invoke();
            return true;
        }
        catch
        {
            try { _cat.Name = _oldName; _cat.StrRef = _oldStrRef; } catch { /* best-effort */ }
            return false;
        }
    }

    public void Undo()
    {
        _cat.Name = _oldName;
        _cat.StrRef = _oldStrRef;
        _onChanged?.Invoke();
    }
}

/// <summary>Reversible add of a new empty category. The first Do allocates an id via the mutator;
/// Undo removes the node; Redo re-inserts the SAME node instance (no fresh allocation) so the id is
/// stable across undo/redo cycles.</summary>
public sealed class PaletteAddCategoryCommand : IUndoableCommand
{
    private readonly ItpFile _itp;
    private readonly PaletteNode? _parent;
    private readonly string _name;
    private readonly Action? _onChanged;
    private PaletteCategoryNode? _created;

    public PaletteAddCategoryCommand(ItpFile itp, PaletteNode? parent, string name, Action? onChanged)
    {
        _itp = itp ?? throw new ArgumentNullException(nameof(itp));
        _parent = parent;
        _name = name;
        _onChanged = onChanged;
    }

    public string Description => $"Add category '{_name}'";

    public bool Do()
    {
        if (_created == null)
        {
            // First Do: allocate + create through the mutator (advances the id allocator once).
            _created = PaletteReorgMutator.AddCategory(_itp, _parent, _name);
            if (_created == null) return false;
        }
        else
        {
            // Redo: re-insert the same node instance so its id is never reallocated.
            (PaletteReorgMutator.ChildListOf(_parent) ?? _itp.MainNodes).Add(_created);
        }

        try
        {
            _onChanged?.Invoke();
            return true;
        }
        catch
        {
            try { (PaletteReorgMutator.ChildListOf(_parent) ?? _itp.MainNodes).Remove(_created); } catch { /* best-effort */ }
            return false;
        }
    }

    public void Undo()
    {
        if (_created == null) return;
        (PaletteReorgMutator.ChildListOf(_parent) ?? _itp.MainNodes).Remove(_created);
        _onChanged?.Invoke();
    }
}

/// <summary>Reversible category move/nest. Captures the category's current parent list + index on Do
/// and moves it back there on Undo. Refuses cycles (delegated to the mutator).</summary>
public sealed class PaletteMoveCategoryCommand : IUndoableCommand
{
    private readonly ItpFile _itp;
    private readonly PaletteCategoryNode _cat;
    private readonly PaletteNode? _newParent;
    private readonly int _index;
    private readonly Action? _onChanged;
    private PaletteNode? _oldParent;
    private int _oldIndex = -1;

    public PaletteMoveCategoryCommand(
        ItpFile itp, PaletteCategoryNode cat, PaletteNode? newParent, int index, Action? onChanged)
    {
        _itp = itp ?? throw new ArgumentNullException(nameof(itp));
        _cat = cat ?? throw new ArgumentNullException(nameof(cat));
        _newParent = newParent;
        _index = index;
        _onChanged = onChanged;
    }

    public string Description => $"Move category '{_cat.Name ?? _cat.Id.ToString()}'";

    public bool Do()
    {
        // Capture the current location BEFORE the move (Redo re-runs Do from the restored state).
        (_oldParent, _oldIndex) = PaletteReorgMutator.LocateChild(_itp, _cat);
        if (_oldIndex < 0) return false; // not in tree

        if (!PaletteReorgMutator.MoveCategory(_itp, _cat, _newParent, _index)) return false;

        try
        {
            _onChanged?.Invoke();
            return true;
        }
        catch
        {
            try { PaletteReorgMutator.MoveCategory(_itp, _cat, _oldParent, _oldIndex); } catch { /* best-effort */ }
            return false;
        }
    }

    public void Undo()
    {
        PaletteReorgMutator.MoveCategory(_itp, _cat, _oldParent, _oldIndex);
        _onChanged?.Invoke();
    }
}
