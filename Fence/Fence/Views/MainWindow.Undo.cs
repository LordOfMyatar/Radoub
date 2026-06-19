using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MerchantEditor.Commands;
using MerchantEditor.ViewModels;
using Radoub.UI.Undo;

namespace MerchantEditor.Views;

/// <summary>
/// Undo/redo wiring for Fence (#2255 / epic #2231). Mirrors Relique's blueprint-editor pattern: a
/// per-document <see cref="UndoRedoManager"/>, menu enablement refreshed on StateChanged, and
/// Ctrl+Z/Y dispatched from <see cref="OnWindowKeyDown"/> with no TextBox-focus guard (document-level
/// undo). Unlike Relique, Fence's store-property controls are NOT bound to a ViewModel — the control's
/// own value is the live source of truth (the UTM model is rebuilt from the UI at save time, see
/// <c>UpdateStoreFromUI</c>). So the field/flag undo setters drive the control directly. Inventory and
/// variable add/remove route through <see cref="IUndoableCommand"/> instances on their collections.
/// </summary>
public partial class MainWindow
{
    private readonly UndoRedoManager _undo = new();
    private bool _undoWired;

    /// <summary>Connect the undo manager to menu refresh + dirty marking and wire each field once.</summary>
    private void WireUndo()
    {
        if (_undoWired) return;
        _undoWired = true;

        _undo.StateChanged += (_, _) => RefreshUndoMenu();
        // Command-based edits flow through the manager; mark dirty on undo state changes too so the
        // title bar reflects undo/redo.
        _undo.StateChanged += (_, _) => MarkDirtyFromUndo();
        RefreshUndoMenu();

        // Whole-field undo for the plain text fields (#2231). Getter/setter read & write the control
        // directly because the control is the source of truth (no bound VM in Fence).
        WireFieldUndo(StoreNameBox, "edit name");
        WireFieldUndo(StoreTagBox, "edit tag");
        WireFieldUndo(SellMarkupBox, "edit sell markup");
        WireFieldUndo(BuyMarkdownBox, "edit buy markdown");
        WireFieldUndo(IdentifyPriceBox, "edit identify price");
        WireFieldUndo(OnOpenStoreBox, "edit OnOpenStore");
        WireFieldUndo(OnStoreClosedBox, "edit OnStoreClosed");
        WireFieldUndo(MaxBuyPriceBox, "edit max buy price");
        WireFieldUndo(StoreGoldBox, "edit store gold");
        WireFieldUndo(BlackMarketMarkdownBox, "edit black-market markdown");
        WireFieldUndo(CommentBox, "edit comment");

        // Flag checkboxes (#2231).
        WireFlagUndo(BlackMarketCheck, "toggle Buy Stolen Goods");
        WireFlagUndo(MaxBuyPriceCheck, "toggle Max Buy Price");
        WireFlagUndo(LimitedGoldCheck, "toggle Limited Gold");
    }

    /// <summary>Mark dirty from an undo/redo state change. MarkDirty is guarded against load/read-only
    /// and a null file path, so this is a no-op for the unsaved-no-file case.</summary>
    private void MarkDirtyFromUndo() => _documentState.MarkDirty();

    /// <summary>
    /// Record and apply a programmatic change to a wired text field (e.g. script browse/clear) as one
    /// undo step. Used where code sets a TextBox's value directly rather than the user typing — those
    /// paths bypass the focus-based field recording, so they push their own command. If the field has a
    /// pending focused edit, commit it first so the programmatic change records from the live value.
    /// </summary>
    private void RecordProgrammaticFieldEdit(TextBox? box, string newValue, string label)
    {
        if (box == null) return;
        CommitFocusedFieldEdit();

        var oldValue = box.Text ?? string.Empty;
        if (oldValue == newValue)
        {
            box.Text = newValue; // keep UI consistent even when unchanged
            return;
        }

        _undo.Execute(new RecordedFieldEditCommand<string>(oldValue, newValue,
            v => box.Text = v, label));

        // Keep the focus baseline aligned if this box is the active edit, so a later focus-loss
        // doesn't record the programmatic change a second time.
        if (_activeFieldEdit == box) _activeFieldBaseline = newValue;
    }

    private void OnUndoClick(object? sender, RoutedEventArgs e) => _undo.Undo();

    private void OnRedoClick(object? sender, RoutedEventArgs e) => _undo.Redo();

    // --- Whole-field text undo (#2231) ---
    //
    // A text field becomes one undo step on focus-loss/Enter: snapshot the value on GotFocus, and on
    // LostFocus record a RecordedFieldEditCommand reverting the whole value if it changed. We read the
    // TextBox's live Text as both baseline and new value, so binding/UpdateSource timing is irrelevant.

    /// <summary>The field whose edit is currently in progress (focused), or null.</summary>
    private TextBox? _activeFieldEdit;
    private string _activeFieldBaseline = string.Empty;
    private TextBox? _activeFieldBox;
    private string _activeFieldLabel = string.Empty;

    /// <summary>
    /// Wire a plain TextBox for whole-field undo. The control is the source of truth, so undo/redo
    /// drives its <c>Text</c> directly; the existing TextChanged → MarkDirty handler keeps the dirty
    /// flag in step. Undo runs only on focus-loss/Enter, so driving Text while unfocused never
    /// re-records.
    /// </summary>
    private void WireFieldUndo(TextBox? box, string label)
    {
        if (box == null) return;

        box.GotFocus += (_, _) =>
        {
            _activeFieldEdit = box;
            _activeFieldBaseline = box.Text ?? string.Empty;
            _activeFieldBox = box;
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
        if (_activeFieldEdit != box || _activeFieldBox == null) return;

        var newValue = box.Text ?? string.Empty;
        var baseline = _activeFieldBaseline;
        var target = _activeFieldBox;
        var label = _activeFieldLabel;

        // Clear active state first so re-entrancy (setter → focus changes) can't double-record.
        _activeFieldEdit = null;
        _activeFieldBox = null;

        if (newValue == baseline) return; // no change → nothing to record

        // Setter drives the control directly (the control is the model). Do() re-applies newValue
        // (idempotent, correct redo); Undo restores the whole baseline value.
        _undo.Execute(new RecordedFieldEditCommand<string>(baseline, newValue,
            v => target.Text = v, label));

        // Re-arm the baseline so a further edit in the same focus session records from here.
        if (box.IsFocused)
        {
            _activeFieldEdit = box;
            _activeFieldBaseline = newValue;
            _activeFieldBox = box;
            _activeFieldLabel = label;
        }
    }

    // --- Inline ResRef grid-cell edit undo (#2255) ---

    private StoreItemViewModel? _editingResRefItem;
    private string _editingResRefBaseline = string.Empty;

    /// <summary>Capture the editable cell's baseline value before the user edits it, so CellEditEnding
    /// can record the whole-value change. Only the ResRef column is editable.</summary>
    private void OnStoreCellPreparingForEdit(object? sender, DataGridPreparingCellForEditEventArgs e)
    {
        if (e.Row?.DataContext is StoreItemViewModel item)
        {
            _editingResRefItem = item;
            _editingResRefBaseline = item.ResRef ?? string.Empty;
        }
    }

    /// <summary>Record the inline ResRef edit as one undo step once the cell commits. The grid's TwoWay
    /// binding has already written the new value to the item, so SetResRefCommand.Do() re-applies it
    /// (idempotent) and Undo restores the baseline.</summary>
    private void OnStoreCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit) { _editingResRefItem = null; return; }
        if (_editingResRefItem == null) return;

        var item = _editingResRefItem;
        var baseline = _editingResRefBaseline;
        _editingResRefItem = null;

        // The edited TextBox value is the source; read it so we record even before the binding flushes.
        var newValue = (e.EditingElement as TextBox)?.Text ?? item.ResRef ?? string.Empty;
        if (newValue == baseline) return;

        _undo.Execute(new SetResRefCommand(item, baseline, newValue));
    }

    /// <summary>True while undo/redo drives a flag setter, so the resulting IsCheckedChanged is not
    /// re-recorded as a new command (#2231).</summary>
    private bool _suppressFlagUndo;

    /// <summary>
    /// Wire a flag CheckBox for undo. Track the checkbox's own last-known state as the undo baseline
    /// and drive <c>IsChecked</c> directly on undo/redo. The existing IsCheckedChanged → MarkDirty
    /// handler keeps the dirty flag in step.
    /// </summary>
    private void WireFlagUndo(CheckBox? box, string label)
    {
        if (box == null) return;

        bool baseline = box.IsChecked == true;

        box.IsCheckedChanged += (_, _) =>
        {
            bool current = box.IsChecked == true;

            if (_documentState.IsLoading || _suppressFlagUndo)
            {
                baseline = current; // keep baseline aligned with programmatic/load-time changes
                return;
            }
            if (current == baseline) return; // no real change

            var oldValue = baseline;
            baseline = current;

            _undo.Execute(new RecordedFieldEditCommand<bool>(oldValue, current, v =>
            {
                _suppressFlagUndo = true;
                try
                {
                    box.IsChecked = v;
                    baseline = v;
                }
                finally { _suppressFlagUndo = false; }
            }, label));
        };
    }

    // --- Buy restrictions undo (#2255) ---
    //
    // Buy restrictions are interrelated (mode + selected item-type set), so undo treats the whole
    // state as one unit (BuyRestrictionsSnapshot). The host tracks the last-committed snapshot; each
    // buy-restriction handler calls RecordBuyRestrictionsChange after mutating, which records a
    // Relay command restoring the prior snapshot and reapplying the new one.

    /// <summary>The buy-restrictions state as of the last recorded change (undo baseline).</summary>
    private BuyRestrictionsSnapshot? _lastBuyRestrictions;

    /// <summary>True while undo/redo reapplies a buy-restrictions snapshot, so the resulting
    /// radio/checkbox events don't record a new command.</summary>
    private bool _suppressBuyRestrictionsUndo;

    /// <summary>Read the current buy mode from the radio buttons.</summary>
    private BuyMode CurrentBuyMode()
    {
        if (WillOnlyBuyRadio.IsChecked == true) return BuyMode.WillOnlyBuy;
        if (WillNotBuyRadio.IsChecked == true) return BuyMode.WillNotBuy;
        return BuyMode.All;
    }

    /// <summary>Capture the current buy-restrictions state as the baseline (called after load so the
    /// first user change records against the loaded state, not an empty one).</summary>
    private void ResetBuyRestrictionsBaseline()
        => _lastBuyRestrictions = BuyRestrictionsSnapshot.Capture(CurrentBuyMode(), SelectableBaseItemTypes);

    /// <summary>Drive the radio buttons to match a snapshot's mode (used on undo/redo apply).</summary>
    private void ApplyBuyMode(BuyMode mode)
    {
        BuyAllRadio.IsChecked = mode == BuyMode.All;
        WillOnlyBuyRadio.IsChecked = mode == BuyMode.WillOnlyBuy;
        WillNotBuyRadio.IsChecked = mode == BuyMode.WillNotBuy;
    }

    /// <summary>
    /// Record a buy-restrictions change as one undo step. The mutation has already been applied to the
    /// controls/VMs; this captures the new state, records a Relay command (undo restores the prior
    /// snapshot, do reapplies the new one), and advances the baseline. Suppressed while loading or
    /// while undo/redo is itself reapplying a snapshot.
    /// </summary>
    private void RecordBuyRestrictionsChange()
    {
        if (_documentState.IsLoading || _suppressBuyRestrictionsUndo) return;

        var before = _lastBuyRestrictions;
        var after = BuyRestrictionsSnapshot.Capture(CurrentBuyMode(), SelectableBaseItemTypes);
        if (before != null && before.Equals(after)) return; // no real change

        _lastBuyRestrictions = after;

        _undo.Execute(new RelayUndoableCommand(
            () => { ApplyBuyRestrictionsSnapshot(after); },
            () => { if (before != null) ApplyBuyRestrictionsSnapshot(before); },
            "change buy restrictions"));
    }

    private void ApplyBuyRestrictionsSnapshot(BuyRestrictionsSnapshot snapshot)
    {
        _suppressBuyRestrictionsUndo = true;
        try
        {
            ApplyBuyMode(snapshot.Mode);
            snapshot.ApplyTo(SelectableBaseItemTypes);
            _lastBuyRestrictions = snapshot;
        }
        finally { _suppressBuyRestrictionsUndo = false; }
    }

    /// <summary>
    /// Sync the Edit-menu Undo/Redo items to the manager state — enablement plus the
    /// "_Undo {description}" hint. InputGesture on a MenuItem only renders hint text in Avalonia;
    /// the actual accelerator is dispatched from <see cref="OnWindowKeyDown"/>.
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
