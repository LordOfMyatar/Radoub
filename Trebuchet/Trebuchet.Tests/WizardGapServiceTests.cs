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
    public void Decide_FirstRun_UnsetRequiredGap_ShowsWelcome()
    {
        var gaps = new[] { Gap("gamePath", satisfied: false) };

        var decision = WizardGapService.Decide(gaps, acknowledgedKeys: Array.Empty<string>(), hasRunBefore: false);

        Assert.True(decision.ShouldShow);
        Assert.Equal(WizardMode.Welcome, decision.Mode);
        Assert.Equal(new[] { "gamePath" }, decision.GapStepKeys);
    }

    [Fact]
    public void Decide_AllSatisfied_FirstRun_DoesNotShow()
    {
        var gaps = new[] { Gap("gamePath", satisfied: true) };

        var decision = WizardGapService.Decide(gaps, acknowledgedKeys: Array.Empty<string>(), hasRunBefore: false);

        Assert.False(decision.ShouldShow);
        Assert.Equal(WizardMode.None, decision.Mode);
    }

    [Fact]
    public void Decide_GapWithGoodDefault_NotForced_DoesNotShow()
    {
        // A setting with a good default is never a forced gap even when "unset".
        var gaps = new[] { Gap("theme", satisfied: false, hasGoodDefault: true) };

        var decision = WizardGapService.Decide(gaps, acknowledgedKeys: Array.Empty<string>(), hasRunBefore: false);

        Assert.False(decision.ShouldShow);
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
    public void Decide_FirstRun_MultipleGaps_SurfacesAllUnsatisfied()
    {
        var gaps = new[]
        {
            Gap("gamePath", satisfied: false),
            Gap("anotherRequired", satisfied: false),
            Gap("theme", satisfied: false, hasGoodDefault: true), // not forced
        };

        var decision = WizardGapService.Decide(gaps, acknowledgedKeys: Array.Empty<string>(), hasRunBefore: false);

        Assert.True(decision.ShouldShow);
        Assert.Equal(WizardMode.Welcome, decision.Mode);
        Assert.Equal(new[] { "gamePath", "anotherRequired" }, decision.GapStepKeys);
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
