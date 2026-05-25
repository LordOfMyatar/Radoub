namespace ItemEditor.Services;

/// <summary>
/// Pure decision helper for whether a Subtype/Value/Param ComboBox SelectionChanged
/// should auto-apply during property editing (#2226). Auto-apply fires when the
/// user is in edit mode (editingPropertyIndex >= 0) and the change isn't a
/// programmatic pre-select (suppressAutoApply false).
/// </summary>
public static class EditAutoApplyDecider
{
    public static bool ShouldAutoApply(int editingPropertyIndex, bool suppressAutoApply)
    {
        if (suppressAutoApply) return false;
        return editingPropertyIndex >= 0;
    }
}
