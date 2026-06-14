using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Radoub.Formats.Logging;
using Radoub.UI.Undo;

namespace Manifest.Views;

/// <summary>
/// Undo/redo wiring for Manifest (#2253 / epic #2231 Sprint 3). Manifest is a writing tool, but
/// per the cross-tool decision (and matching what Parley already does for dialogue prose) every
/// editable field records ONE whole-field document undo step per focus session — there is no
/// per-character native undo here. The shared <see cref="RecordedFieldEditCommand{T}"/> reverts
/// the whole previous committed value; structural add/delete go through the dedicated commands in
/// <see cref="Manifest.Services.AddCategoryCommand"/> et al.
///
/// Pattern mirrors Relique's MainWindow.Undo: a per-document <see cref="UndoRedoManager"/>, menu
/// refreshed on StateChanged, Ctrl+Z/Y dispatched from the window key handler. Re-entrancy is
/// guarded by <see cref="_suppressUndo"/> (undo-driven setters must not re-record) and bind-time
/// events are guarded by the existing <c>_isUpdatingPanel</c> flag.
/// </summary>
public partial class MainWindow
{
    private readonly UndoRedoManager _undo = new();

    /// <summary>True while undo/redo drives a setter, so the resulting change event is not
    /// re-recorded as a new command.</summary>
    private bool _suppressUndo;

    // Whole-field text undo baseline: captured on GotFocus, recorded on LostFocus/save-commit.
    private TextBox? _activeFieldBox;
    private string _activeFieldBaseline = string.Empty;

    /// <summary>Drop undo/redo history. Called per document (file open / new) so each document
    /// starts fresh (#2231 — undo is per-document, never crosses files).</summary>
    private void ClearUndo() => _undo.Clear();

    /// <summary>Connect the undo manager to menu refresh once (called from the constructor).</summary>
    private void WireUndo()
    {
        _undo.StateChanged += (_, _) => RefreshUndoMenu();
        RefreshUndoMenu();
    }

    private void OnUndoClick(object? sender, RoutedEventArgs e)
    {
        // Commit any in-progress text edit so it lands on the stack before we undo it.
        CommitFocusedEdit();
        // Each command refreshes its own UI (field commands repaint the panel and keep selection;
        // structural commands rebuild the tree). We do NOT blanket-rebuild the tree here — doing so
        // cleared the TreeView selection and blanked the property panel on a field undo (#2253 UAT).
        _undo.Undo();
        MarkDirty();
    }

    private void OnRedoClick(object? sender, RoutedEventArgs e)
    {
        _undo.Redo();
        MarkDirty();
    }


    /// <summary>
    /// Record an already-applied whole-field edit through the undo manager (#2231). The caller has
    /// (or is about to) set <paramref name="newValue"/> on the model; the command's setter applies
    /// new on redo and old on undo, guarded so it does not re-record. No-op when suppressed,
    /// bind-time, or unchanged.
    /// </summary>
    private void RecordFieldEdit<T>(T oldValue, T newValue, Action<T> apply, string description)
    {
        if (_suppressUndo || _isUpdatingPanel) return;
        if (Equals(oldValue, newValue)) return;

        // The model is already at newValue (the handler set it before recording). On the initial
        // Execute we must NOT repaint the panel — the user is mid-edit and a repaint would reset
        // the caret/selection. On undo/redo we DO repaint so the reverted/redone value shows, while
        // keeping the tree selection (no tree rebuild — that blanked the panel, #2253 UAT).
        var firstDo = true;
        _undo.Execute(new RecordedFieldEditCommand<T>(oldValue, newValue, v =>
        {
            _suppressUndo = true;
            try
            {
                apply(v);
                if (!firstDo) UpdatePropertyPanel();
                firstDo = false;
            }
            finally { _suppressUndo = false; }
        }, description));
    }

    /// <summary>
    /// Execute a structural command (add/delete) through the manager. The wrapper refreshes the
    /// tree/panel on Do/Undo/Redo so the UI stays in sync without the command knowing about the UI.
    /// </summary>
    private void ExecuteStructural(IUndoableCommand command)
    {
        var wrapped = new RefreshingCommand(command, () =>
        {
            _suppressUndo = true;
            try { UpdateTree(); UpdatePropertyPanel(); UpdateStatusBarCounts(); }
            finally { _suppressUndo = false; }
        });
        _undo.Execute(wrapped);
    }

    /// <summary>Wire whole-field text undo on a prose/text box: snapshot the box's CURRENTLY
    /// DISPLAYED text on focus-in as the undo baseline. We read the box (not a model getter)
    /// because the displayed value can differ from the model's slot-0 default — e.g. a category
    /// name resolved from a TLK StrRef shows resolved text while GetDefault() is empty. Capturing
    /// the model would make undo revert to that empty/wrong value (the #2253 UAT bug; same class
    /// of issue hit in Relique). Recording happens in the LostFocus handler / CommitFocusedEdit.</summary>
    private void WireTextFieldUndo(TextBox? box)
    {
        if (box == null) return;
        box.GotFocus += (_, _) =>
        {
            if (_isUpdatingPanel) return;
            _activeFieldBox = box;
            _activeFieldBaseline = box.Text ?? string.Empty;
        };
    }

    /// <summary>
    /// Sync the Edit-menu Undo/Redo items to the manager state (enablement + hint text). The
    /// accelerator itself is dispatched from OnWindowKeyDown; InputGesture only renders the hint.
    /// </summary>
    private void RefreshUndoMenu()
    {
        if (UndoMenuItem != null)
        {
            UndoMenuItem.IsEnabled = _undo.CanUndo;
            UndoMenuItem.Header = _undo.CanUndo ? $"_Undo {_undo.UndoDescription}" : "_Undo";
        }
        if (RedoMenuItem != null)
        {
            RedoMenuItem.IsEnabled = _undo.CanRedo;
            RedoMenuItem.Header = _undo.CanRedo ? $"_Redo {_undo.RedoDescription}" : "_Redo";
        }
    }

    /// <summary>Adapter that runs a structural command and a UI refresh together as one undo step,
    /// so undo/redo keep the tree/panel in sync without the command knowing about the UI.
    ///
    /// Preserves the #2254 mutate→refresh→rollback discipline: if the refresh throws on Do(), the
    /// inner command is undone (model rolled back) and Do() returns false so the manager does NOT
    /// record it — the editor is never left dirty behind a half-rendered tree.</summary>
    private sealed class RefreshingCommand : IUndoableCommand
    {
        private readonly IUndoableCommand _inner;
        private readonly Action _refresh;
        public RefreshingCommand(IUndoableCommand inner, Action refresh) { _inner = inner; _refresh = refresh; }
        public string Description => _inner.Description;

        public bool Do()
        {
            if (!_inner.Do()) return false;
            try
            {
                _refresh();
                return true;
            }
            catch (Exception ex)
            {
                _inner.Undo(); // roll back the model mutation
                UnifiedLogger.LogJournal(LogLevel.ERROR,
                    $"Refresh failed after structural '{_inner.Description}' — rolled back: {ex.GetType().Name}: {ex.Message}");
                return false; // refuse-to-push: don't record a change that was reverted
            }
        }

        public void Undo() { _inner.Undo(); _refresh(); }
    }
}
