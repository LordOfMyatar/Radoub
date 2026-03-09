namespace Quartermaster.Services;

/// <summary>
/// Controls how strictly character creation and level-up rules are enforced.
/// Named after NWN alignment flavors for thematic fun.
/// </summary>
public enum ValidationLevel
{
    /// <summary>
    /// Chaotic Evil: No validation. Anything goes — pick any feats, assign skills freely,
    /// ignore prerequisites. For power users and custom content creators.
    /// </summary>
    None = 0,

    /// <summary>
    /// True Neutral: Warn about rule violations but allow them. Show visual indicators
    /// (yellow highlights, warning icons) when selections break rules, but don't block progression.
    /// This is the default.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Lawful Good: Enforce ELC (Enforceable Legal Character) rules. Block invalid selections,
    /// enforce prerequisites, match NWN server ELC checks.
    /// </summary>
    Strict = 2
}
