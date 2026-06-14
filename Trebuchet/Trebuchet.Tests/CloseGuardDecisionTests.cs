using RadoubLauncher.Services;

namespace Trebuchet.Tests;

/// <summary>
/// Tests for CloseGuard — the pure helper that decides what the window-close
/// path should do given the editor dirty state and (when prompted) the user's
/// Save / Discard / Cancel choice. Isolated from Avalonia so the save-on-exit
/// guard (#2453) is unit-testable without FlaUI.
/// </summary>
public class CloseGuardDecisionTests
{
    [Fact]
    public void NoUnsavedChanges_ProceedsWithoutPrompt()
    {
        var decision = CloseGuard.Evaluate(hasUnsavedChanges: false);

        Assert.False(decision.NeedsPrompt);
        Assert.Equal(CloseAction.Proceed, decision.Action);
    }

    [Fact]
    public void UnsavedChanges_RequiresPrompt()
    {
        var decision = CloseGuard.Evaluate(hasUnsavedChanges: true);

        Assert.True(decision.NeedsPrompt);
    }

    [Fact]
    public void UserChoosesSave_SavesThenCloses()
    {
        var action = CloseGuard.Resolve(ClosePromptResult.Save);

        Assert.Equal(CloseAction.SaveThenProceed, action);
    }

    [Fact]
    public void UserChoosesDiscard_ProceedsWithoutSaving()
    {
        var action = CloseGuard.Resolve(ClosePromptResult.Discard);

        Assert.Equal(CloseAction.Proceed, action);
    }

    [Fact]
    public void UserChoosesCancel_AbortsClose()
    {
        var action = CloseGuard.Resolve(ClosePromptResult.Cancel);

        Assert.Equal(CloseAction.Abort, action);
    }
}
