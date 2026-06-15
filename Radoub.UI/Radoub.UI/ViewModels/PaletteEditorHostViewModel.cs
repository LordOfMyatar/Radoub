using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Radoub.Formats.Itp;
using Radoub.UI.Services.Palette;
using Radoub.UI.Undo;

namespace Radoub.UI.ViewModels;

/// <summary>The user's choice when switching resource type with unsaved changes.</summary>
public enum DirtySwitchChoice { Save, Discard, Cancel }

/// <summary>
/// DataContext for the shared palette editor control (#2477, M3). Owns exactly one active
/// <see cref="PaletteContext"/> at a time — each resource type is fully isolated. Switching type
/// tears the old context down (after a Save/Discard/Cancel prompt when dirty) and loads the new one
/// fresh from disk, so stale tree/undo/dirty state can never bleed across types. Disk load, the
/// dirty prompt, and the save commit are injected delegates so the orchestration is unit-testable
/// without a UI.
/// </summary>
public partial class PaletteEditorHostViewModel : ObservableObject
{
    private readonly Func<PaletteResourceType, PaletteContext> _loadContext;
    private readonly Func<Task<DirtySwitchChoice>> _promptDirty;
    private readonly Func<IReadOnlyList<PaletteFileWrite>, PaletteSaveResult> _commit;

    [ObservableProperty] private PaletteContext? _activeContext;

    /// <summary>The bindable tree forest for the active context (rebuilt on load + after reorg).</summary>
    public ObservableCollection<PaletteNodeViewModel> Forest { get; } = new();

    public PaletteEditorHostViewModel(
        Func<PaletteResourceType, PaletteContext> loadContext,
        Func<Task<DirtySwitchChoice>> promptDirty,
        Func<IReadOnlyList<PaletteFileWrite>, PaletteSaveResult> commit)
    {
        _loadContext = loadContext ?? throw new ArgumentNullException(nameof(loadContext));
        _promptDirty = promptDirty ?? throw new ArgumentNullException(nameof(promptDirty));
        _commit = commit ?? throw new ArgumentNullException(nameof(commit));
    }

    /// <summary>
    /// Switch the edited resource type. If the current context is dirty, prompt: Save commits then
    /// switches, Discard switches, Cancel aborts (current context kept). On switch the old context
    /// is dropped entirely and the new one is loaded fresh, then the forest is rebuilt.
    /// </summary>
    public async Task SwitchResourceTypeAsync(PaletteResourceType type)
    {
        if (ActiveContext is { ViewModel.IsDirty: true })
        {
            switch (await _promptDirty())
            {
                case DirtySwitchChoice.Cancel:
                    return;
                case DirtySwitchChoice.Save:
                    if (!Save()) return; // failed save aborts the switch (don't lose edits)
                    break;
                case DirtySwitchChoice.Discard:
                    break;
            }
        }

        // Full teardown: drop the old context before building the new one; RebuildForest clears
        // and repopulates the bindable forest.
        ActiveContext = _loadContext(type);
        RebuildForest();
    }

    /// <summary>Rebuild the bindable forest from the active context's tree + Uncategorized bucket.</summary>
    public void RebuildForest()
    {
        Forest.Clear();
        if (ActiveContext is null) return;
        foreach (var node in PaletteNodeViewModel.BuildForest(ActiveContext.ViewModel))
            Forest.Add(node);
    }

    /// <summary>Commit the active context's write-set. Clears dirty on success; leaves it set on
    /// failure (all-or-nothing — nothing was written).</summary>
    public bool Save()
    {
        if (ActiveContext is null) return false;
        var result = _commit(ActiveContext.BuildWriteSet());
        if (result.Success) ActiveContext.ViewModel.IsDirty = false;
        return result.Success;
    }

    // ---- reorg ops (route VM op -> rebuild forest -> mark dirty) -------------
    // The M2 VM ops are already mutate-refresh-rollback wrapped (a failed refresh self-reverts and
    // returns false). After a successful op the forest is rebuilt and the context marked dirty.
    //
    // Undo/redo: delete-with-reparent is fully reversible (PaletteDeleteCategoryCommand). Reversible
    // undo for blueprint move/file and category add/rename/move is tracked as follow-up work — each
    // needs a dedicated inverse command; wiring a no-op undo here would be worse than none.
    // TODO (#2484): add inverse undo commands for move/file/add/rename/move-category.

    /// <summary>Recategorize a listed blueprint, or file an uncategorized one, into a category.
    /// <paramref name="from"/> is the blueprint's current tree home (null when it is uncategorized).</summary>
    public bool MoveOrFileBlueprint(string resRef, PaletteCategoryNode? from, PaletteCategoryNode to)
    {
        if (ActiveContext is null) return false;
        var vm = ActiveContext.ViewModel;
        // No source => uncategorized: file (add-only). Otherwise move (drop-onto-own-home re-syncs drift).
        bool ok = from is null ? vm.FileBlueprint(resRef, to) : vm.MoveBlueprint(resRef, from, to);
        return AfterReorg(ok);
    }

    /// <summary>Add a new empty category under <paramref name="parent"/> (or root when null).</summary>
    public bool AddCategory(PaletteCategoryNode? parent, string name)
    {
        if (ActiveContext is null) return false;
        return AfterReorg(ActiveContext.ViewModel.AddCategory(parent, name) != null);
    }

    /// <summary>Rename a category.</summary>
    public bool RenameCategory(PaletteCategoryNode cat, string newName)
    {
        if (ActiveContext is null) return false;
        return AfterReorg(ActiveContext.ViewModel.RenameCategory(cat, newName));
    }

    /// <summary>Move/nest a category to a new parent and index.</summary>
    public bool MoveCategory(PaletteCategoryNode cat, PaletteNode? newParent, int index)
    {
        if (ActiveContext is null) return false;
        return AfterReorg(ActiveContext.ViewModel.MoveCategory(cat, newParent, index));
    }

    /// <summary>Delete a category, reparenting its contents. Reversible via the undo manager.</summary>
    public bool DeleteCategory(PaletteCategoryNode cat)
    {
        if (ActiveContext is null) return false;
        // Run Do() directly so we know whether it applied; only record on the undo stack when it did
        // (matches UndoRedoManager's refuse-to-push contract for self-rolled-back commands).
        var cmd = new PaletteDeleteCategoryCommand(
            ActiveContext.Palette, ActiveContext.Store, cat, onChanged: RebuildForest);
        ActiveContext.UndoManager.Execute(cmd);
        // Execute invokes Do() once; if it returned false the stack is untouched. A successful delete
        // leaves at least one undoable entry. We mark dirty whenever an undoable entry now exists.
        bool applied = ActiveContext.UndoManager.CanUndo;
        if (applied) ActiveContext.ViewModel.IsDirty = true;
        return applied;
    }

    public void Undo() { ActiveContext?.UndoManager.Undo(); RebuildForest(); }
    public void Redo() { ActiveContext?.UndoManager.Redo(); RebuildForest(); }

    // After a VM reorg op: on success rebuild the forest and mark dirty. (The VM op already ran its
    // own refresh callback; RebuildForest here is the host-owned forest the control binds to.)
    private bool AfterReorg(bool ok)
    {
        if (!ok || ActiveContext is null) return false;
        RebuildForest();
        ActiveContext.ViewModel.IsDirty = true;
        return true;
    }
}
