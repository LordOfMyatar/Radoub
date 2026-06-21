using ItemEditor.Services;
using Xunit;

namespace ItemEditor.Tests.Services;

/// <summary>
/// A composite-weapon model-part ComboBox must NOT be rebuilt (Items.Clear + repopulate)
/// while its own SelectionChanged is executing — doing so leaves stale selection state that
/// crashes deep in the Avalonia render loop with no catchable exception (#2533).
///
/// The undo apply-action re-syncs the combos so undo/redo keeps the UI in sync, but that
/// re-sync is only safe (and only needed) when the value changed programmatically. When the
/// user's live selection drives the change, the combo already shows the new value, so the
/// re-sync must be skipped.
/// </summary>
public class ModelPartResyncDeciderTests
{
    [Fact]
    public void ShouldResyncCombos_DuringLiveSelection_ReturnsFalse()
    {
        // User just picked a part in the combo — rebuilding it now is the crash.
        Assert.False(ModelPartResyncDecider.ShouldResyncCombos(selectionInProgress: true));
    }

    [Fact]
    public void ShouldResyncCombos_ProgrammaticChange_ReturnsTrue()
    {
        // Undo/redo changed the value behind the UI — the combo must be re-synced.
        Assert.True(ModelPartResyncDecider.ShouldResyncCombos(selectionInProgress: false));
    }
}
