using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Radoub.UI.Undo;

namespace ItemEditor.Views;

/// <summary>
/// Undo/redo wiring for Relique (Sprint 1 of epic #2231). Mirrors Reliquary's pattern: a
/// per-document <see cref="UndoRedoManager"/>, menu enablement refreshed on StateChanged, and
/// Ctrl+Z/Y dispatched from the window key handler with a TextBox-focus guard (see
/// <see cref="MainWindow.OnWindowKeyDown"/>). Property/variable mutations are routed through
/// <see cref="IUndoableCommand"/> instances; scalar TextBox edits keep Avalonia's native undo.
/// </summary>
public partial class MainWindow
{
    private readonly UndoRedoManager _undo = new();
    private bool _editorWired;

    /// <summary>Connect the undo manager to menu refresh + dirty marking once (called from construction).</summary>
    private void WireEditor()
    {
        if (_editorWired) return;
        _editorWired = true;

        _undo.StateChanged += (_, _) => RefreshUndoMenu();
        // Command-based edits flow through the undo manager; binding-based scalar edits flow through
        // the VM. Mark dirty on undo state changes too so the title bar reflects undo/redo.
        _undo.StateChanged += (_, _) => MarkDirty();
        RefreshUndoMenu();

        // Whole-field undo for the editable TwoWay text fields (#2231). Setters resolve the current
        // ViewModel at commit time; getters read its committed value for the focus snapshot.
        WireFieldUndo(this.FindControl<TextBox>("NameTextBox"),
            () => _itemViewModel?.Name ?? string.Empty,
            v => { if (_itemViewModel != null) _itemViewModel.Name = v; }, "edit name");
        WireFieldUndo(this.FindControl<TextBox>("TagTextBox"),
            () => _itemViewModel?.Tag ?? string.Empty,
            v => { if (_itemViewModel != null) _itemViewModel.Tag = v; }, "edit tag");
        WireFieldUndo(this.FindControl<TextBox>("DescriptionTextBox"),
            () => _itemViewModel?.Description ?? string.Empty,
            v => { if (_itemViewModel != null) _itemViewModel.Description = v; }, "edit description");
        WireFieldUndo(this.FindControl<TextBox>("DescIdentifiedTextBox"),
            () => _itemViewModel?.DescIdentified ?? string.Empty,
            v => { if (_itemViewModel != null) _itemViewModel.DescIdentified = v; }, "edit identified description");
        WireFieldUndo(this.FindControl<TextBox>("CommentTextBox"),
            () => _itemViewModel?.Comment ?? string.Empty,
            v => { if (_itemViewModel != null) _itemViewModel.Comment = v; }, "edit comment");
    }

    private void OnUndoClick(object? sender, RoutedEventArgs e) => _undo.Undo();

    private void OnRedoClick(object? sender, RoutedEventArgs e) => _undo.Redo();

    // --- Whole-field text undo (#2231) ---
    //
    // Relique is a blueprint editor → document/whole-field undo (see project policy). A text field
    // becomes one undo step on focus-loss/Enter: snapshot the committed value on GotFocus, and on
    // LostFocus record a RecordedFieldEditCommand reverting the whole value if it changed. We read
    // the TextBox's live Text (not the VM getter) as the new value, so the binding's UpdateSource
    // timing can't cause us to miss or misread an edit.

    /// <summary>The field whose edit is currently in progress (focused), or null.</summary>
    private TextBox? _activeFieldEdit;
    private string _activeFieldBaseline = string.Empty;
    private Action<string>? _activeFieldSetter;
    private string _activeFieldLabel = string.Empty;

    /// <summary>
    /// Wire a TwoWay text field for whole-field undo. <paramref name="setter"/> applies a value to
    /// the current ViewModel; it is resolved at commit time so a rebound VM (new document) is fine —
    /// undo history is cleared per document anyway.
    /// </summary>
    private void WireFieldUndo(TextBox? box, Func<string> getter, Action<string> setter, string label)
    {
        if (box == null) return;

        box.GotFocus += (_, _) =>
        {
            _activeFieldEdit = box;
            _activeFieldBaseline = getter() ?? string.Empty;
            _activeFieldSetter = setter;
            _activeFieldLabel = label;
        };
        box.LostFocus += (_, _) => CommitFieldEdit(box);
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) CommitFieldEdit(box);
        };
    }

    /// <summary>Record the focused field's edit (if any) as a single undo step. Used by the Ctrl+Z
    /// path so an in-progress text edit lands on the stack before the undo runs.</summary>
    private void CommitFocusedFieldEdit()
    {
        if (_activeFieldEdit != null) CommitFieldEdit(_activeFieldEdit);
    }

    private void CommitFieldEdit(TextBox box)
    {
        if (_activeFieldEdit != box || _activeFieldSetter == null) return;

        var newValue = box.Text ?? string.Empty;
        var baseline = _activeFieldBaseline;
        var setter = _activeFieldSetter;
        var label = _activeFieldLabel;

        // Clear active state first so re-entrancy (e.g. setter → focus changes) can't double-record.
        _activeFieldEdit = null;
        _activeFieldSetter = null;

        if (newValue == baseline) return; // no change → nothing to record

        // The binding may already have written newValue to the VM; the command's Do() re-applies it
        // (idempotent) and is the correct redo action. Undo restores the whole baseline value.
        _undo.Execute(new RecordedFieldEditCommand<string>(baseline, newValue, setter, label));

        // Re-arm the baseline so a further edit in the same focus session records from here.
        if (box.IsFocused)
        {
            _activeFieldEdit = box;
            _activeFieldBaseline = newValue;
            _activeFieldSetter = setter;
            _activeFieldLabel = label;
        }
    }

    /// <summary>
    /// Sync the Edit-menu Undo/Redo items to the manager state — enablement plus the
    /// "_Undo {description}" hint. InputGesture on a MenuItem only renders hint text in Avalonia;
    /// the actual accelerator is dispatched from <see cref="MainWindow.OnWindowKeyDown"/>.
    /// </summary>
    private void RefreshUndoMenu()
    {
        var undoItem = this.FindControl<MenuItem>("UndoMenuItem");
        if (undoItem != null)
        {
            undoItem.IsEnabled = _undo.CanUndo;
            undoItem.Header = _undo.CanUndo ? $"_Undo {_undo.UndoDescription}" : "_Undo";
        }

        var redoItem = this.FindControl<MenuItem>("RedoMenuItem");
        if (redoItem != null)
        {
            redoItem.IsEnabled = _undo.CanRedo;
            redoItem.Header = _undo.CanRedo ? $"_Redo {_undo.RedoDescription}" : "_Redo";
        }
    }
}
