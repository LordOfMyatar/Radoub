namespace ItemEditor.Services;

/// <summary>
/// Decides whether the model-part ComboBoxes may be rebuilt during a model-part change.
///
/// Composite-weapon part combos are populated with Items; rebuilding a combo
/// (Items.Clear + repopulate) from inside its own SelectionChanged leaves stale selection
/// state that crashes the Avalonia render loop with no catchable exception (#2533). The undo
/// apply-action re-syncs the combos so undo/redo keeps the UI in sync, but that re-sync is
/// only safe — and only necessary — for programmatic value changes. When the user's live
/// selection drives the change the combo already reflects the new value, so the rebuild must
/// be skipped to avoid the re-entrancy.
/// </summary>
public static class ModelPartResyncDecider
{
    /// <summary>
    /// True when the combos may be rebuilt. False while a live ComboBox SelectionChanged is
    /// in progress (the rebuild would re-enter the active control).
    /// </summary>
    public static bool ShouldResyncCombos(bool selectionInProgress) => !selectionInProgress;
}
