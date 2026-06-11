using System.Collections.Generic;
using System.Linq;

namespace RadoubLauncher.Services;

/// <summary>
/// Decides whether the first-run / welcome-back configuration wizard (#1020)
/// should appear, in which mode, and which steps are the genuine unfilled gaps.
///
/// A "forced gap" is a setting that is required, has no good default, and is not
/// yet satisfied. The wizard exists to fill those — settings that have good
/// defaults are shown for review but never force the wizard open.
///
/// To avoid nagging, completing the wizard acknowledges every registered gap key
/// (see <see cref="AllKeys"/>); an acknowledged gap is not re-surfaced even if the
/// user deliberately left it unset.
/// </summary>
public static class WizardGapService
{
    public static WizardDecision Decide(
        IEnumerable<WizardGap> gaps, IEnumerable<string> acknowledgedKeys, bool hasRunBefore)
    {
        var acknowledged = new HashSet<string>(acknowledgedKeys);

        // Forced gaps: required (no good default), unsatisfied, and not yet acknowledged.
        var forced = gaps
            .Where(g => !g.HasGoodDefault && !g.IsSatisfied && !acknowledged.Contains(g.Key))
            .Select(g => g.Key)
            .ToList();

        if (forced.Count == 0)
            return new WizardDecision(false, WizardMode.None, forced);

        var mode = hasRunBefore ? WizardMode.WelcomeBack : WizardMode.Welcome;
        return new WizardDecision(true, mode, forced);
    }

    /// <summary>Every registered gap key — acknowledged on wizard completion.</summary>
    public static IReadOnlyList<string> AllKeys(IEnumerable<WizardGap> gaps) =>
        gaps.Select(g => g.Key).ToList();
}

/// <summary>
/// A single configurable setting the wizard can surface.
/// </summary>
/// <param name="Key">Stable identifier, persisted in the acknowledged-gaps record.</param>
/// <param name="IsSatisfied">Whether the setting currently has a usable value.</param>
/// <param name="HasGoodDefault">Whether a sensible default exists — if so, this gap never forces the wizard open.</param>
public sealed record WizardGap(string Key, bool IsSatisfied, bool HasGoodDefault = false);

public enum WizardMode
{
    None,
    Welcome,
    WelcomeBack,
}

/// <summary>The wizard launch decision.</summary>
public sealed record WizardDecision(bool ShouldShow, WizardMode Mode, IReadOnlyList<string> GapStepKeys);
