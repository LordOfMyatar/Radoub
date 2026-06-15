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

    /// <summary>
    /// Optional TLK resolver for category names stored as a StrRef (standard categories). The host
    /// (e.g. Trebuchet) sets this from its <c>TlkService</c>; null leaves StrRef categories showing
    /// a <c>[StrRef N]</c> placeholder. Set before the first load / call <see cref="RebuildForest"/>.
    /// </summary>
    public Func<uint, string?>? StrRefResolver { get; set; }

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

    /// <summary>
    /// Rebuild the bindable forest from the active context's tree + Uncategorized bucket, preserving
    /// which categories were expanded across the rebuild. Categories keep their model identity through
    /// a reorg (only blueprints move between them), so expansion is restored by matching on the backing
    /// <see cref="PaletteNodeViewModel.Model"/>. Without this every drop collapses the whole tree.
    /// </summary>
    public void RebuildForest()
    {
        // Snapshot expansion (and the Uncategorized bucket's expansion, which has no model) before clearing.
        var expandedModels = new HashSet<PaletteNode>(ReferenceEqualityComparer.Instance);
        bool uncategorizedExpanded = false;
        foreach (var node in EnumerateForest(Forest))
        {
            if (!node.IsExpanded) continue;
            if (node.Model is { } m) expandedModels.Add(m);
            else if (node.Kind == PaletteNodeKind.Uncategorized) uncategorizedExpanded = true;
        }

        Forest.Clear();
        if (ActiveContext is null) return;
        foreach (var node in PaletteNodeViewModel.BuildForest(ActiveContext.ViewModel, StrRefResolver))
            Forest.Add(node);

        // Restore expansion on the rebuilt nodes.
        foreach (var node in EnumerateForest(Forest))
        {
            if (node.Model is { } m && expandedModels.Contains(m)) node.IsExpanded = true;
            else if (node.Kind == PaletteNodeKind.Uncategorized && uncategorizedExpanded) node.IsExpanded = true;
        }
    }

    /// <summary>Expand and select the category a drop landed in, so focus follows the drop.</summary>
    public void RevealCategory(PaletteCategoryNode category)
    {
        foreach (var node in EnumerateForest(Forest))
        {
            if (ReferenceEquals(node.Model, category))
            {
                ExpandAncestorsAndSelect(node);
                return;
            }
        }
    }

    private void ExpandAncestorsAndSelect(PaletteNodeViewModel target)
    {
        // Expand the target and every ancestor on the path to it; select the target.
        foreach (var (node, path) in EnumerateForestWithPath(Forest))
        {
            if (!ReferenceEquals(node, target)) continue;
            foreach (var ancestor in path) ancestor.IsExpanded = true;
            target.IsExpanded = true;
            target.IsSelected = true;
            return;
        }
    }

    private static System.Collections.Generic.IEnumerable<PaletteNodeViewModel> EnumerateForest(
        System.Collections.Generic.IEnumerable<PaletteNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in EnumerateForest(node.Children))
                yield return child;
        }
    }

    private static System.Collections.Generic.IEnumerable<(PaletteNodeViewModel Node, System.Collections.Generic.List<PaletteNodeViewModel> Path)>
        EnumerateForestWithPath(System.Collections.Generic.IEnumerable<PaletteNodeViewModel> nodes,
            System.Collections.Generic.List<PaletteNodeViewModel>? path = null)
    {
        path ??= new System.Collections.Generic.List<PaletteNodeViewModel>();
        foreach (var node in nodes)
        {
            yield return (node, path);
            path.Add(node);
            foreach (var item in EnumerateForestWithPath(node.Children, path))
                yield return item;
            path.RemoveAt(path.Count - 1);
        }
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

    /// <summary>Place a blueprint into a category by setting its PaletteID (the authoritative write).
    /// Works the same whether the blueprint was uncategorized or under another category.</summary>
    public bool MoveBlueprintToCategory(string resRef, PaletteCategoryNode to)
    {
        if (ActiveContext is null) return false;
        if (!AfterReorg(ActiveContext.ViewModel.SetBlueprintCategory(resRef, to))) return false;
        RevealCategory(to); // focus follows the drop: expand + select the destination
        return true;
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
        if (!AfterReorg(ActiveContext.ViewModel.MoveCategory(cat, newParent, index))) return false;
        if (newParent is PaletteCategoryNode parentCat) RevealCategory(parentCat);
        RevealCategory(cat); // select the moved category itself
        return true;
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
