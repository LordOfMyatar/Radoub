using Radoub.UI.Models;
using Xunit;

namespace Radoub.UI.Tests;

public class FilterStateTests
{
    [Fact]
    public void Defaults_ShowStandardTrue()
    {
        var state = new FilterState();

        Assert.True(state.ShowStandard);
    }

    // #1995 — the binary ShowCustom is replaced by three per-source toggles.
    // Defaults: Standard/HAK/Module visible, Override hidden.

    [Fact]
    public void Defaults_ShowHakAndModuleTrue_OverrideFalse()
    {
        var state = new FilterState();

        Assert.True(state.ShowHak);
        Assert.True(state.ShowModule);
        Assert.False(state.ShowOverride);
    }

    [Fact]
    public void Defaults_SearchTextNull()
    {
        var state = new FilterState();

        Assert.Null(state.SearchText);
    }

    [Fact]
    public void Defaults_SelectedBaseItemIndexNull()
    {
        var state = new FilterState();

        Assert.Null(state.SelectedBaseItemIndex);
    }

    [Fact]
    public void CanSetAllProperties()
    {
        var state = new FilterState
        {
            ShowStandard = false,
            ShowOverride = true,
            ShowHak = false,
            ShowModule = false,
            SearchText = "sword",
            SelectedBaseItemIndex = 5
        };

        Assert.False(state.ShowStandard);
        Assert.True(state.ShowOverride);
        Assert.False(state.ShowHak);
        Assert.False(state.ShowModule);
        Assert.Equal("sword", state.SearchText);
        Assert.Equal(5, state.SelectedBaseItemIndex);
    }

    // ---- Legacy migration (old persisted state had a single ShowCustom bool) ----

    [Fact]
    public void MigrateLegacy_LegacyCustomTrue_SeedsAllThreeCustomTogglesOn()
    {
        // An old file with ShowCustom=true should keep showing custom content.
        var state = new FilterState { LegacyShowCustom = true };

        state.MigrateLegacy();

        Assert.True(state.ShowOverride);
        Assert.True(state.ShowHak);
        Assert.True(state.ShowModule);
        Assert.Null(state.LegacyShowCustom); // consumed
    }

    [Fact]
    public void MigrateLegacy_LegacyCustomFalse_SeedsAllThreeCustomTogglesOff()
    {
        var state = new FilterState { LegacyShowCustom = false };

        state.MigrateLegacy();

        Assert.False(state.ShowOverride);
        Assert.False(state.ShowHak);
        Assert.False(state.ShowModule);
        Assert.Null(state.LegacyShowCustom);
    }

    [Fact]
    public void MigrateLegacy_NoLegacyValue_LeavesNewDefaultsUntouched()
    {
        // A fresh / already-migrated file has no legacy value; new defaults stand.
        var state = new FilterState();

        state.MigrateLegacy();

        Assert.True(state.ShowStandard);
        Assert.False(state.ShowOverride);
        Assert.True(state.ShowHak);
        Assert.True(state.ShowModule);
    }
}
