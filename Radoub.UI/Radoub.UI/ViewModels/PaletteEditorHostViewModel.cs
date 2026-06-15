using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Radoub.Formats.Itp;
using Radoub.UI.Services.Palette;
using Radoub.UI.Undo;

namespace Radoub.UI.ViewModels;

/// <summary>
/// DataContext for the shared palette editor control (#2477, M3). Owns exactly one active
/// <see cref="PaletteContext"/> at a time — each resource type is fully isolated. Edits persist
/// immediately (save-on-move), so switching type just tears the old context down and loads the new
/// one fresh from disk; stale tree/undo state can never bleed across types. Disk load and the save
/// commit are injected delegates so the orchestration is unit-testable without a UI.
/// </summary>
public partial class PaletteEditorHostViewModel : ObservableObject
{
    private readonly Func<PaletteResourceType, PaletteContext> _loadContext;
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

    // Returns the name of the tool currently holding a cross-tool lock on the given file path, or
    // null if it is free. Defaults to the shared FileSessionLockService (Relique/QM/Fence acquire
    // these locks when they open a blueprint); injectable for headless tests.
    private readonly Func<string, string?> _lockHolder;

    public PaletteEditorHostViewModel(
        Func<PaletteResourceType, PaletteContext> loadContext,
        Func<IReadOnlyList<PaletteFileWrite>, PaletteSaveResult> commit,
        Func<string, string?>? lockHolder = null)
    {
        _loadContext = loadContext ?? throw new ArgumentNullException(nameof(loadContext));
        _commit = commit ?? throw new ArgumentNullException(nameof(commit));
        _lockHolder = lockHolder ?? (path => Services.FileSessionLockService.CheckLock(path)?.ToolName);
    }

    /// <summary>
    /// Switch the edited resource type. Edits persist immediately (save-on-move), so there are never
    /// unsaved changes to protect — the old context is dropped, the new one loaded fresh from disk,
    /// and the forest rebuilt. Reload re-reads the same way.
    /// </summary>
    public Task SwitchResourceTypeAsync(PaletteResourceType type)
    {
        ActiveContext = _loadContext(type);
        RebuildForest();
        return Task.CompletedTask;
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

    /// <summary>
    /// Raised when an immediate commit fails (the all-or-nothing transaction wrote nothing). The
    /// host surfaces this; the active context has already been reloaded from disk so editor and disk
    /// stay in sync. The argument is a short human-readable message.
    /// </summary>
    public event Action<string>? SaveFailed;

    // ---- reorg ops: save-immediately ----------------------------------------
    // The editor persists every move the moment it happens — there is no pending/dirty state and no
    // Save button. Each reorg: run the M2 VM op (mutate-refresh-rollback) -> rebuild forest ->
    // commit the whole palette to disk atomically. If the commit fails, the active context is
    // reloaded from disk so the editor never diverges from the files (which is what produced the
    // earlier save conflict), and SaveFailed is raised.
    //
    // Undo/redo: delete-with-reparent is reversible (PaletteDeleteCategoryCommand). Reversible undo
    // for the other ops is tracked in #2484.

    /// <summary>Commit the active palette to disk atomically (reconcile tree + write changed files).
    /// On failure, reload from disk so the editor matches the files, and raise <see cref="SaveFailed"/>.
    /// Returns true on success.</summary>
    private bool CommitNow()
    {
        if (ActiveContext is null) return false;
        var result = _commit(ActiveContext.BuildWriteSet());
        if (result.Success) return true;

        // Nothing was written (all-or-nothing). Re-sync the editor to disk and report.
        ReloadActiveFromDisk();
        SaveFailed?.Invoke(result.Error?.Message ?? "Save failed — no files were changed.");
        return false;
    }

    /// <summary>Reload the active resource type fresh from disk, bypassing the dirty prompt (used to
    /// re-sync after a failed commit; there are no edits worth protecting at that point).</summary>
    public void ReloadActiveFromDisk()
    {
        if (ActiveContext is null) return;
        ActiveContext = _loadContext(ActiveContext.Type);
        RebuildForest();
    }

    /// <summary>Place a blueprint into a category by setting its PaletteID (the authoritative write).
    /// Works the same whether the blueprint was uncategorized or under another category. Refused
    /// (with a SaveFailed warning) if the blueprint is currently open in another tool — moving it
    /// would write the file underneath that tool and lose its edits on the next save.</summary>
    public bool MoveBlueprintToCategory(string resRef, PaletteCategoryNode to)
    {
        if (ActiveContext is null) return false;

        // Cross-tool guard: the move rewrites the whole blueprint file (read-disk -> set PaletteID
        // -> write). If another running tool (Relique/QM/Fence) holds the file open, abort and warn
        // rather than clobber its in-flight edits.
        if (ActiveContext.Store.GetFilePath(resRef) is { } path && _lockHolder(path) is { } holder)
        {
            SaveFailed?.Invoke($"'{resRef}' is open in {holder}. Close it there, then try again.");
            return false;
        }

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

    /// <summary>Delete a category, reparenting its contents. Reversible via the undo manager; the
    /// result is committed to disk immediately.</summary>
    public bool DeleteCategory(PaletteCategoryNode cat)
    {
        if (ActiveContext is null) return false;
        bool before = ActiveContext.UndoManager.CanUndo;
        var cmd = new PaletteDeleteCategoryCommand(
            ActiveContext.Palette, ActiveContext.Store, cat, onChanged: RebuildForest);
        ActiveContext.UndoManager.Execute(cmd);
        // Execute invokes Do() once; if it self-rolled-back the stack is untouched.
        if (ActiveContext.UndoManager.CanUndo == before && before == false) return false; // no-op
        return CommitNow();
    }

    public void Undo() { ActiveContext?.UndoManager.Undo(); RebuildForest(); CommitNow(); }
    public void Redo() { ActiveContext?.UndoManager.Redo(); RebuildForest(); CommitNow(); }

    // After a VM reorg op: on success rebuild the forest and commit to disk immediately.
    private bool AfterReorg(bool ok)
    {
        if (!ok || ActiveContext is null) return false;
        RebuildForest();
        return CommitNow();
    }
}
