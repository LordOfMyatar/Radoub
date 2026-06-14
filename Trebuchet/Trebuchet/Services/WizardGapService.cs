using System;
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
/// <item><b>Welcome-back</b> (has run before): shows when either (a) the setup was last
/// completed against a build older than <paramref name="setupReviewVersion"/> — a newer
/// build added settings worth reviewing (#2419) — or (b) a <i>forced gap</i> (required,
/// no good default, unsatisfied, unacknowledged) has appeared since last run. The version
/// gate surfaces every step (a full review); the forced-gap path surfaces just the gaps.</item>
/// </list>
///
/// Completing the wizard acknowledges every registered gap key (see
/// <see cref="AllKeys"/>), so a setting the user deliberately left unset is not
/// re-surfaced.
/// </summary>
public static class WizardGapService
{
    /// <summary>
    /// Build version the setup screen last meaningfully changed at (#2419). Bump this
    /// deliberately when adding a setting worth re-surfacing; users whose
    /// <c>LastSetupVersion</c> is older are re-prompted once to review. Must stay at or
    /// below the current app version, or every launch re-prompts. Trebuchet ships this
    /// under the 1.19.x line (see version.json), so the baseline is "1.19".
    /// </summary>
    public const string SetupReviewVersion = "1.19";

    public static WizardDecision Decide(
        IEnumerable<WizardGap> gaps, IEnumerable<string> acknowledgedKeys, bool hasRunBefore,
        string? lastSetupVersion = null, string? setupReviewVersion = null)
    {
        var gapList = gaps.ToList();

        if (!hasRunBefore)
        {
            // First run: one-time review of everything. Show if anything is registered.
            if (gapList.Count == 0)
                return new WizardDecision(false, WizardMode.None, gapList.Select(g => g.Key).ToList());

            return new WizardDecision(true, WizardMode.Welcome, gapList.Select(g => g.Key).ToList());
        }

        // Has run before, version gate (#2419): a newer build added reviewable settings.
        // Only applies when both version inputs are supplied (legacy callers pass neither).
        if (setupReviewVersion != null && VersionLess(lastSetupVersion, setupReviewVersion))
        {
            return new WizardDecision(true, WizardMode.WelcomeBack, gapList.Select(g => g.Key).ToList());
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

    /// <summary>
    /// True when <paramref name="lastSetupVersion"/> is older than
    /// <paramref name="reviewVersion"/>. A null/empty/malformed last version counts as
    /// older than any real review version (safe: re-prompts rather than silently
    /// suppressing). NBGV suffixes (e.g. "-alpha") are stripped before parsing.
    /// </summary>
    internal static bool VersionLess(string? lastSetupVersion, string reviewVersion)
    {
        if (!TryParseVersion(reviewVersion, out var review))
            return false; // unparseable threshold → never force a review

        return !TryParseVersion(lastSetupVersion, out var last) || last < review;
    }

    private static bool TryParseVersion(string? raw, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        // Strip any pre-release/build suffix: "1.40.0-alpha" → "1.40.0".
        var core = raw.Split('-', '+')[0].Trim();
        return Version.TryParse(core, out var parsed) && (version = parsed) != null;
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
