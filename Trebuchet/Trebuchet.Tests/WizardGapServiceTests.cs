using System;
using System.Collections.Generic;
using System.Linq;
using RadoubLauncher.Services;
using Xunit;

namespace Trebuchet.Tests;

/// <summary>
/// Tests for the wizard gap registry (#1020): decides whether the first-run /
/// welcome-back wizard should fire, in which mode, and which steps are the
/// genuine unfilled gaps. Pure logic — no settings I/O or UI.
/// </summary>
public class WizardGapServiceTests
{
    private static WizardGap Gap(string key, bool satisfied, bool hasGoodDefault = false) =>
        new(key, satisfied, hasGoodDefault);

    [Fact]
    public void Decide_FirstRun_AlwaysShowsWelcome_SurfacingAllSteps()
    {
        // First run always reviews everything once — even an auto-detected game path
        // gets shown for confirmation. GapStepKeys is every registered key.
        var gaps = new[] { Gap("gamePath", satisfied: false) };

        var decision = WizardGapService.Decide(gaps, acknowledgedKeys: Array.Empty<string>(), hasRunBefore: false);

        Assert.True(decision.ShouldShow);
        Assert.Equal(WizardMode.Welcome, decision.Mode);
        Assert.Equal(new[] { "gamePath" }, decision.GapStepKeys);
    }

    [Fact]
    public void Decide_FirstRun_EvenWhenAllSatisfied_StillShowsWelcome()
    {
        // The original bug (#1985 follow-up): auto-detect fills the game path, so a
        // "show only when something is unset" rule never fired on a configured
        // machine. First run is a one-time review, so it must show regardless.
        var gaps = new[] { Gap("gamePath", satisfied: true) };

        var decision = WizardGapService.Decide(gaps, acknowledgedKeys: Array.Empty<string>(), hasRunBefore: false);

        Assert.True(decision.ShouldShow);
        Assert.Equal(WizardMode.Welcome, decision.Mode);
    }

    [Fact]
    public void Decide_FirstRun_WithOnlyGoodDefaultGaps_StillShowsForReview()
    {
        // Logging/backup have good defaults but are part of the one-time review.
        var gaps = new[] { Gap("logging", satisfied: true, hasGoodDefault: true) };

        var decision = WizardGapService.Decide(gaps, acknowledgedKeys: Array.Empty<string>(), hasRunBefore: false);

        Assert.True(decision.ShouldShow);
        Assert.Equal(WizardMode.Welcome, decision.Mode);
    }

    [Fact]
    public void Decide_FirstRun_NoGapsRegistered_DoesNotShow()
    {
        var decision = WizardGapService.Decide(
            Array.Empty<WizardGap>(), acknowledgedKeys: Array.Empty<string>(), hasRunBefore: false);

        Assert.False(decision.ShouldShow);
        Assert.Equal(WizardMode.None, decision.Mode);
    }

    [Fact]
    public void Decide_HasRunBefore_NewUnacknowledgedGap_ShowsWelcomeBack()
    {
        // Configured before (gamePath set + acknowledged), but a new no-default
        // gap appeared that was never acknowledged.
        var gaps = new[]
        {
            Gap("gamePath", satisfied: true),
            Gap("newThing", satisfied: false),
        };

        var decision = WizardGapService.Decide(
            gaps, acknowledgedKeys: new[] { "gamePath" }, hasRunBefore: true);

        Assert.True(decision.ShouldShow);
        Assert.Equal(WizardMode.WelcomeBack, decision.Mode);
        Assert.Equal(new[] { "newThing" }, decision.GapStepKeys);
    }

    [Fact]
    public void Decide_HasRunBefore_AllAcknowledged_DoesNotShow()
    {
        var gaps = new[]
        {
            Gap("gamePath", satisfied: true),
            Gap("newThing", satisfied: false),
        };

        var decision = WizardGapService.Decide(
            gaps, acknowledgedKeys: new[] { "gamePath", "newThing" }, hasRunBefore: true);

        Assert.False(decision.ShouldShow);
        Assert.Equal(WizardMode.None, decision.Mode);
    }

    [Fact]
    public void Decide_HasRunBefore_UnsetButAcknowledgedGap_DoesNotReshow()
    {
        // The user saw the gap, acknowledged it, but chose to leave it unset.
        // We must not nag every launch.
        var gaps = new[] { Gap("gamePath", satisfied: false) };

        var decision = WizardGapService.Decide(
            gaps, acknowledgedKeys: new[] { "gamePath" }, hasRunBefore: true);

        Assert.False(decision.ShouldShow);
    }

    [Fact]
    public void Decide_FirstRun_MultipleGaps_SurfacesAllStepsForReview()
    {
        // First run surfaces every registered step (all reviewable), regardless of
        // satisfaction or whether a step has a good default.
        var gaps = new[]
        {
            Gap("gamePath", satisfied: false),
            Gap("anotherRequired", satisfied: false),
            Gap("theme", satisfied: true, hasGoodDefault: true),
        };

        var decision = WizardGapService.Decide(gaps, acknowledgedKeys: Array.Empty<string>(), hasRunBefore: false);

        Assert.True(decision.ShouldShow);
        Assert.Equal(WizardMode.Welcome, decision.Mode);
        Assert.Equal(new[] { "gamePath", "anotherRequired", "theme" }, decision.GapStepKeys);
    }

    // --- Version-gated re-prompt (#2419) ---

    [Fact]
    public void Decide_HasRunBefore_OlderSetupVersion_ShowsWelcomeBack_AllSteps()
    {
        // User completed setup against an older build; a newer build's review version
        // is higher → re-prompt to review (all steps surfaced, not just forced gaps).
        var gaps = new[]
        {
            Gap("gamePath", satisfied: true),
            Gap("logging", satisfied: true, hasGoodDefault: true),
        };

        var decision = WizardGapService.Decide(
            gaps, acknowledgedKeys: new[] { "gamePath", "logging" }, hasRunBefore: true,
            lastSetupVersion: "1.39", setupReviewVersion: "1.40");

        Assert.True(decision.ShouldShow);
        Assert.Equal(WizardMode.WelcomeBack, decision.Mode);
        Assert.Equal(new[] { "gamePath", "logging" }, decision.GapStepKeys);
    }

    [Fact]
    public void Decide_HasRunBefore_SameSetupVersion_DoesNotShow()
    {
        var gaps = new[] { Gap("gamePath", satisfied: true) };

        var decision = WizardGapService.Decide(
            gaps, acknowledgedKeys: new[] { "gamePath" }, hasRunBefore: true,
            lastSetupVersion: "1.40", setupReviewVersion: "1.40");

        Assert.False(decision.ShouldShow);
        Assert.Equal(WizardMode.None, decision.Mode);
    }

    [Fact]
    public void Decide_HasRunBefore_NewerSetupVersion_DoesNotShow()
    {
        // Defensive: a user on a newer build than the review threshold isn't re-prompted.
        var gaps = new[] { Gap("gamePath", satisfied: true) };

        var decision = WizardGapService.Decide(
            gaps, acknowledgedKeys: new[] { "gamePath" }, hasRunBefore: true,
            lastSetupVersion: "1.41", setupReviewVersion: "1.40");

        Assert.False(decision.ShouldShow);
    }

    [Fact]
    public void Decide_HasRunBefore_EmptyLastSetupVersion_TreatedAsOlder_ShowsWelcomeBack()
    {
        // A user who ran the pre-version-gate wizard has no LastSetupVersion. Treat
        // empty as older than any real review version so they get the new review once.
        var gaps = new[] { Gap("gamePath", satisfied: true) };

        var decision = WizardGapService.Decide(
            gaps, acknowledgedKeys: new[] { "gamePath" }, hasRunBefore: true,
            lastSetupVersion: "", setupReviewVersion: "1.40");

        Assert.True(decision.ShouldShow);
        Assert.Equal(WizardMode.WelcomeBack, decision.Mode);
    }

    [Fact]
    public void Decide_HasRunBefore_MalformedLastSetupVersion_TreatedAsOlder()
    {
        // Unparseable persisted version → re-prompt (safe) rather than silently suppress.
        var gaps = new[] { Gap("gamePath", satisfied: true) };

        var decision = WizardGapService.Decide(
            gaps, acknowledgedKeys: new[] { "gamePath" }, hasRunBefore: true,
            lastSetupVersion: "garbage", setupReviewVersion: "1.40");

        Assert.True(decision.ShouldShow);
    }

    [Fact]
    public void Decide_HasRunBefore_VersionSuffixStripped_ParsesAlpha()
    {
        // NBGV versions carry suffixes like "1.40.0-alpha". Equal core versions are
        // NOT less, so no re-prompt.
        var gaps = new[] { Gap("gamePath", satisfied: true) };

        var decision = WizardGapService.Decide(
            gaps, acknowledgedKeys: new[] { "gamePath" }, hasRunBefore: true,
            lastSetupVersion: "1.40.0-alpha", setupReviewVersion: "1.40");

        Assert.False(decision.ShouldShow);
    }

    [Fact]
    public void Decide_NoVersionArgs_PreservesLegacyForcedGapBehavior()
    {
        // Calling without version args (defaults null) keeps the original #1020 logic.
        var gaps = new[]
        {
            Gap("gamePath", satisfied: true),
            Gap("newThing", satisfied: false),
        };

        var decision = WizardGapService.Decide(
            gaps, acknowledgedKeys: new[] { "gamePath" }, hasRunBefore: true);

        Assert.True(decision.ShouldShow);
        Assert.Equal(WizardMode.WelcomeBack, decision.Mode);
        Assert.Equal(new[] { "newThing" }, decision.GapStepKeys);
    }

    [Fact]
    public void SetupReviewVersion_IsNotAheadOfShippingVersion_NoPerpetualReprompt()
    {
        // Guard against the regression where SetupReviewVersion was set ahead of the
        // real app version (1.19.x), so a user who completed setup at the current build
        // was re-prompted on every launch (#2419). A setup completed at the current
        // version must NOT be considered older than the review threshold.
        const string shippingVersion = "1.19.69-alpha";

        Assert.False(WizardGapService.VersionLess(shippingVersion, WizardGapService.SetupReviewVersion));
    }

    [Fact]
    public void Decide_CompletedAtCurrentVersion_DoesNotReshow()
    {
        var gaps = new[] { Gap("gamePath", satisfied: true) };

        var decision = WizardGapService.Decide(
            gaps, acknowledgedKeys: new[] { "gamePath" }, hasRunBefore: true,
            lastSetupVersion: "1.19.69-alpha", setupReviewVersion: WizardGapService.SetupReviewVersion);

        Assert.False(decision.ShouldShow);
    }

    [Fact]
    public void AllKeys_ReturnsEveryRegisteredKey_ForAcknowledgement()
    {
        // When the wizard completes it acknowledges ALL registered gap keys, so a
        // setting the user deliberately left unset is not re-surfaced next launch.
        var gaps = new[]
        {
            Gap("gamePath", satisfied: false),
            Gap("theme", satisfied: false, hasGoodDefault: true),
        };

        var keys = WizardGapService.AllKeys(gaps);

        Assert.Equal(new[] { "gamePath", "theme" }, keys);
    }
}
