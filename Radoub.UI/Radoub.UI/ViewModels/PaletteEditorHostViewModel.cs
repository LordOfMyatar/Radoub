using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Radoub.UI.Services.Palette;

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
}
