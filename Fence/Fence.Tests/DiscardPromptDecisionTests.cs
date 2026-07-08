using MerchantEditor.Services;

namespace Fence.Tests;

/// <summary>
/// Tests for the pure discard-decision helper (#2516). Fence's File→New / File→Open
/// must gate on a save/discard/cancel prompt when the current store has unsaved edits.
/// </summary>
public class DiscardPromptDecisionTests
{
    [Fact]
    public void Evaluate_NotDirty_ReturnsProceedNoPrompt()
    {
        var action = DiscardDecision.Evaluate(isDirty: false, promptResult: null);

        Assert.Equal(DiscardAction.ProceedNoPrompt, action);
    }

    [Fact]
    public void Evaluate_Dirty_Save_ReturnsSave()
    {
        var action = DiscardDecision.Evaluate(isDirty: true, promptResult: SavePromptResult.Save);

        Assert.Equal(DiscardAction.Save, action);
    }

    [Fact]
    public void Evaluate_Dirty_DontSave_ReturnsProceed()
    {
        var action = DiscardDecision.Evaluate(isDirty: true, promptResult: SavePromptResult.DontSave);

        Assert.Equal(DiscardAction.Proceed, action);
    }

    [Fact]
    public void Evaluate_Dirty_Cancel_ReturnsAbort()
    {
        var action = DiscardDecision.Evaluate(isDirty: true, promptResult: SavePromptResult.Cancel);

        Assert.Equal(DiscardAction.Abort, action);
    }

    [Fact]
    public void Evaluate_Dirty_NullPrompt_ReturnsAbort()
    {
        var action = DiscardDecision.Evaluate(isDirty: true, promptResult: null);

        Assert.Equal(DiscardAction.Abort, action);
    }
}
