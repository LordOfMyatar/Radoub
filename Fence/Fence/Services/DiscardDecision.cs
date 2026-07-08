namespace MerchantEditor.Services;

public enum SavePromptResult { Save, DontSave, Cancel }
public enum DiscardAction { ProceedNoPrompt, Proceed, Save, Abort }

public static class DiscardDecision
{
    /// <summary>Pure decision for the discard-changes flow (#2516). Not dirty → proceed with
    /// no prompt; dirty → map the user's prompt choice to an action.</summary>
    public static DiscardAction Evaluate(bool isDirty, SavePromptResult? promptResult)
    {
        if (!isDirty) return DiscardAction.ProceedNoPrompt;
        return promptResult switch
        {
            SavePromptResult.Save => DiscardAction.Save,
            SavePromptResult.DontSave => DiscardAction.Proceed,
            _ => DiscardAction.Abort,
        };
    }
}
