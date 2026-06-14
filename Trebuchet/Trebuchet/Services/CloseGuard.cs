namespace RadoubLauncher.Services;

/// <summary>
/// The user's choice from the save-on-exit confirmation prompt.
/// </summary>
public enum ClosePromptResult
{
    Save,
    Discard,
    Cancel
}

/// <summary>
/// What the window-close path should do.
/// </summary>
public enum CloseAction
{
    /// <summary>Continue closing the window.</summary>
    Proceed,

    /// <summary>Persist unsaved edits, then close.</summary>
    SaveThenProceed,

    /// <summary>Abort the close; keep the window open.</summary>
    Abort
}

/// <summary>
/// Result of evaluating whether a close needs a save prompt.
/// </summary>
public readonly record struct CloseDecision(bool NeedsPrompt, CloseAction Action);

/// <summary>
/// Pure decision logic for the save-on-exit guard (#2453). Kept free of Avalonia
/// so the close behavior can be unit-tested without driving the UI.
/// </summary>
public static class CloseGuard
{
    /// <summary>
    /// Decide whether closing should proceed immediately or prompt the user first.
    /// </summary>
    public static CloseDecision Evaluate(bool hasUnsavedChanges)
        => hasUnsavedChanges
            ? new CloseDecision(NeedsPrompt: true, Action: CloseAction.Abort)
            : new CloseDecision(NeedsPrompt: false, Action: CloseAction.Proceed);

    /// <summary>
    /// Map the user's prompt choice onto the close action to take.
    /// </summary>
    public static CloseAction Resolve(ClosePromptResult result) => result switch
    {
        ClosePromptResult.Save => CloseAction.SaveThenProceed,
        ClosePromptResult.Discard => CloseAction.Proceed,
        _ => CloseAction.Abort
    };
}
