namespace Quartermaster.Services;

/// <summary>
/// Controls how strictly character creation and level-up rules are enforced.
/// Named after NWN alignment flavors for thematic fun.
/// Simplified to two tiers in #1882 — the previous TN/Warning tier allowed
/// everything CE allows, so it was merged into None.
/// </summary>
public enum ValidationLevel
{
    /// <summary>
    /// Chaotic Evil: No validation. Anything goes — pick any feats, assign skills freely,
    /// ignore prerequisites. For power users and custom content creators.
    /// </summary>
    None = 0,

    /// <summary>
    /// Lawful Good: Enforce ELC (Enforceable Legal Character) rules. Block invalid selections,
    /// enforce prerequisites, match NWN server ELC checks.
    /// Value intentionally 2 to preserve compatibility with settings written before #1882
    /// when the removed Warning tier occupied value 1.
    /// </summary>
    Strict = 2
}

/// <summary>
/// Maps between ValidationLevel enum values and the two-item wizard ComboBox index
/// (ComboBox: index 0 = None, index 1 = Strict). Needed because Strict = 2 skips 1
/// to preserve settings compatibility with the removed Warning tier (#1882).
/// </summary>
public static class ValidationLevelComboBoxMap
{
    public static int ToComboBoxIndex(ValidationLevel level) =>
        level == ValidationLevel.Strict ? 1 : 0;

    public static ValidationLevel FromComboBoxIndex(int index) =>
        index == 1 ? ValidationLevel.Strict : ValidationLevel.None;
}
