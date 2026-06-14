using System.Collections.Generic;
using System.Linq;

namespace RadoubLauncher.Services;

/// <summary>
/// Decides whether the first-run / welcome-back configuration wizard (#1020)
/// should appear, in which mode, and which steps to surface.
///
/// Two distinct triggers:
/// <list type="bullet">
/// <item><b>First run</b> (<paramref name="hasRunBefore"/> false): a one-time review.
/// The wizard always shows if any steps are registered, surfacing every step — even a
/// game path that auto-detect already filled is shown for confirmation. This is the
/// fix for the original bug where auto-detect satisfied the only gap, so a
/// "show only when unset" rule never fired on a configured machine.</item>
/// <item><b>Welcome-back</b> (has run before): shows only when a <i>forced gap</i> —
/// required (no good default), unsatisfied, and not yet acknowledged — has appeared
/// since last run (e.g. a newly-introduced no-default setting). Surfaces just those.</item>
/// </list>
///
/// Completing the wizard acknowledges every registered gap key (see
/// <see cref="AllKeys"/>), so a setting the user deliberately left unset is not
/// re-surfaced.
/// </summary>
public static class WizardGapService
{
    public static WizardDecision Decide(
        IEnumerable<WizardGap> gaps, IEnumerable<string> acknowledgedKeys, bool hasRunBefore)
    {
        var gapList = gaps.ToList();

        if (!hasRunBefore)
        {
            // First run: one-time review of everything. Show if anything is registered.
            if (gapList.Count == 0)
                return new WizardDecision(false, WizardMode.None, gapList.Select(g => g.Key).ToList());

            return new WizardDecision(true, WizardMode.Welcome, gapList.Select(g => g.Key).ToList());
        }

        // Has run before: only a newly-appeared forced gap re-opens the wizard.
        var acknowledged = new HashSet<string>(acknowledgedKeys);
        var forced = gapList
            .Where(g => !g.HasGoodDefault && !g.IsSatisfied && !acknowledged.Contains(g.Key))
            .Select(g => g.Key)
            .ToList();

        return forced.Count == 0
            ? new WizardDecision(false, WizardMode.None, forced)
            : new WizardDecision(true, WizardMode.WelcomeBack, forced);
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
