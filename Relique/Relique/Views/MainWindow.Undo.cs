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

        // Flag checkboxes (#2231).
        WireFlagUndo(this.FindControl<CheckBox>("PlotCheckBox"),
            () => _itemViewModel?.Plot ?? false,
            v => { if (_itemViewModel != null) _itemViewModel.Plot = v; }, "toggle Plot");
        WireFlagUndo(this.FindControl<CheckBox>("CursedCheckBox"),
            () => _itemViewModel?.Cursed ?? false,
            v => { if (_itemViewModel != null) _itemViewModel.Cursed = v; }, "toggle Cursed");
        WireFlagUndo(this.FindControl<CheckBox>("StolenCheckBox"),
            () => _itemViewModel?.Stolen ?? false,
            v => { if (_itemViewModel != null) _itemViewModel.Stolen = v; }, "toggle Stolen");
        WireFlagUndo(this.FindControl<CheckBox>("IdentifiedCheckBox"),
            () => _itemViewModel?.Identified ?? false,
            v => { if (_itemViewModel != null) _itemViewModel.Identified = v; }, "toggle Identified");
        WireFlagUndo(this.FindControl<CheckBox>("DropableCheckBox"),
            () => _itemViewModel?.Dropable ?? false,
            v => { if (_itemViewModel != null) _itemViewModel.Dropable = v; }, "toggle Droppable");

        // Numeric fields (#2231).
        WireNumericUndo(this.FindControl<NumericUpDown>("AddCostUpDown"), "change additional cost");
        WireNumericUndo(this.FindControl<NumericUpDown>("StackSizeUpDown"), "change stack size");
        WireNumericUndo(this.FindControl<NumericUpDown>("ChargesUpDown"), "change charges");

        // Appearance color fields (#2231).
        WireNumericUndo(this.FindControl<NumericUpDown>("Cloth1ColorInput"), "change cloth 1 color");
        WireNumericUndo(this.FindControl<NumericUpDown>("Cloth2ColorInput"), "change cloth 2 color");
        WireNumericUndo(this.FindControl<NumericUpDown>("Leather1ColorInput"), "change leather 1 color");
        WireNumericUndo(this.FindControl<NumericUpDown>("Leather2ColorInput"), "change leather 2 color");
        WireNumericUndo(this.FindControl<NumericUpDown>("Metal1ColorInput"), "change metal 1 color");
        WireNumericUndo(this.FindControl<NumericUpDown>("Metal2ColorInput"), "change metal 2 color");
    }

    /// <summary>True while undo/redo drives a model-part change (re-entrancy guard, #2231).</summary>
    private bool _suppressModelPartUndo;

    /// <summary>
    /// Record a model-part change through undo (#2231). The combo is not bound to the VM, so when the
    /// selection handler fires <paramref name="getter"/> still returns the pre-change value — captured
    /// as the undo baseline. The change is applied (and reverted) via <paramref name="setter"/>, then
    /// the editor refreshes its model-part combos + icon preview so undo/redo keeps the UI in sync.
    /// </summary>
    private void RecordModelPartChange(System.Func<byte> getter, System.Action<byte> setter, byte newValue, string label)
    {
        if (_suppressModelPartUndo || _itemViewModel == null) return;
        if (getter() == newValue) return;

        var oldValue = getter();
        _undo.Execute(new RecordedFieldEditCommand<byte>(oldValue, newValue, v =>
        {
            _suppressModelPartUndo = true;
            try
            {
                setter(v);
                // Re-sync the combos + icon preview to the model (undo/redo path). _isLoading guards
                // the repopulate from re-recording via its own handlers.
                if (_currentItem != null)
                {
                    var wasLoading = _isLoading;
                    _isLoading = true;
                    try { UpdateConditionalFields(_currentItem.BaseItem); }
                    finally { _isLoading = wasLoading; }
                }
            }
            finally { _suppressModelPartUndo = false; }
        }, label));
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

    /// <summary>True while undo/redo drives a flag setter, so the resulting IsCheckedChanged is not
    /// re-recorded as a new command (#2231).</summary>
    private bool _suppressFlagUndo;

    /// <summary>
    /// Wire a TwoWay-bound flag CheckBox for undo. The binding mutates the VM on click, so we record
    /// the already-applied toggle via RecordedFieldEditCommand on IsCheckedChanged. Skipped while
    /// loading or while undo/redo is driving the setter (re-entrancy guard).
    /// </summary>
    private void WireFlagUndo(CheckBox? box, Func<bool> getter, Action<bool> setter, string label)
    {
        if (box == null) return;

        box.IsCheckedChanged += (_, _) =>
        {
            if (_isLoading || _suppressFlagUndo || _itemViewModel == null) return;

            bool newValue = box.IsChecked == true;
            bool oldValue = !newValue; // two-state checkbox: old is the opposite of the new state
            if (getter() != newValue) return; // binding hasn't applied yet / no real change

            _undo.Execute(new RecordedFieldEditCommand<bool>(oldValue, newValue, v =>
            {
                _suppressFlagUndo = true;
                try { setter(v); }
                finally { _suppressFlagUndo = false; }
            }, label));
        };
    }

    /// <summary>True while undo/redo drives a NumericUpDown setter (re-entrancy guard, #2231).</summary>
    private bool _suppressNumericUndo;

    /// <summary>
    /// Wire a TwoWay-bound NumericUpDown for undo. ValueChanged carries old + new directly, so each
    /// edit becomes one whole-value undo step. Undo drives the control's Value (updating UI + the
    /// bound VM); the guard stops that from re-recording.
    /// </summary>
    private void WireNumericUndo(NumericUpDown? box, string label)
    {
        if (box == null) return;

        box.ValueChanged += (_, e) =>
        {
            if (_isLoading || _suppressNumericUndo || _itemViewModel == null) return;
            if (e.OldValue == e.NewValue) return;

            var oldValue = e.OldValue;
            var newValue = e.NewValue;
            _undo.Execute(new RecordedFieldEditCommand<decimal?>(oldValue, newValue, v =>
            {
                _suppressNumericUndo = true;
                try { box.Value = v; }
                finally { _suppressNumericUndo = false; }
            }, label));
        };
    }

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
