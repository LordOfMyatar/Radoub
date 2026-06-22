using Radoub.TestUtilities.Mocks;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests.Services;

/// <summary>
/// Tests for data-driven robe body-part suppression (#2582). Which body parts a robe hides is
/// read from <c>parts_robe.2da</c> <c>HIDE*</c> columns — matching the Aurora engine (rollnw
/// <c>preview_scene.cpp</c> <c>robe_hides_body_part</c>) — instead of a hardcoded part set. This
/// fixes cloak-only robes (e.g. <c>robe116</c>, which only hides shoulders) rendering with no
/// body, and subsumes the #2398 Dana arm carve-out from the authoritative source.
/// </summary>
public class RobePartSuppressionTests
{
    // Real parts_robe.2da hide columns, verified against the game data:
    //   robe 5 (Dana): CHEST,PELVIS,BELT,SHOL,SHOR,LEGL,LEGR,SHINL,SHINR  (NOT arms/hands)
    //   robe 116 (cloak): SHOL,SHOR only
    //   robe 186: full body incl. BICEPL/R, FOREL/R, HANDL/R
    private static MockGameDataService GameWith(int robeRow, params string[] hiddenColumns)
    {
        var game = new MockGameDataService(includeSampleData: false);
        // Every HIDE* column the suppressor may query defaults to "0" (not hidden); set the
        // requested ones to "1".
        foreach (var col in AllHideColumns)
            game.Set2DAValue("parts_robe", robeRow, col, "0");
        foreach (var col in hiddenColumns)
            game.Set2DAValue("parts_robe", robeRow, col, "1");
        return game;
    }

    private static readonly string[] AllHideColumns =
    {
        "HIDECHEST", "HIDEPELVIS", "HIDEBELT", "HIDENECK", "HIDEHEAD",
        "HIDEBICEPL", "HIDEBICEPR", "HIDEFOREL", "HIDEFORER", "HIDEHANDL", "HIDEHANDR",
        "HIDESHOL", "HIDESHOR", "HIDELEGL", "HIDELEGR", "HIDESHINL", "HIDESHINR",
        "HIDEFOOTL", "HIDEFOOTR",
    };

    [Fact]
    public void Robe5_Dana_HidesTorsoAndLegs_NotArms()
    {
        var game = GameWith(5,
            "HIDECHEST", "HIDEPELVIS", "HIDEBELT", "HIDESHOL", "HIDESHOR",
            "HIDELEGL", "HIDELEGR", "HIDESHINL", "HIDESHINR");

        // Torso/legs hidden
        Assert.True(RobePartSuppression.IsSuppressedByRobe("chest", 5, game));
        Assert.True(RobePartSuppression.IsSuppressedByRobe("pelvis", 5, game));
        Assert.True(RobePartSuppression.IsSuppressedByRobe("legl", 5, game));
        Assert.True(RobePartSuppression.IsSuppressedByRobe("shinr", 5, game));
        // Arms/hands KEPT (the #2398 Dana fix, now from the 2DA)
        Assert.False(RobePartSuppression.IsSuppressedByRobe("bicepl", 5, game));
        Assert.False(RobePartSuppression.IsSuppressedByRobe("forel", 5, game));
        Assert.False(RobePartSuppression.IsSuppressedByRobe("handl", 5, game));
        Assert.False(RobePartSuppression.IsSuppressedByRobe("handr", 5, game));
    }

    [Fact]
    public void Robe116_Cloak_HidesShouldersOnly_BodyRenders()
    {
        var game = GameWith(116, "HIDESHOL", "HIDESHOR");

        Assert.True(RobePartSuppression.IsSuppressedByRobe("shol", 116, game));
        Assert.True(RobePartSuppression.IsSuppressedByRobe("shor", 116, game));
        // The body must NOT be suppressed — this is the robe116 bug.
        Assert.False(RobePartSuppression.IsSuppressedByRobe("chest", 116, game));
        Assert.False(RobePartSuppression.IsSuppressedByRobe("pelvis", 116, game));
        Assert.False(RobePartSuppression.IsSuppressedByRobe("legl", 116, game));
        Assert.False(RobePartSuppression.IsSuppressedByRobe("bicepl", 116, game));
    }

    [Fact]
    public void Robe186_HidesFullBodyInclArms()
    {
        var game = GameWith(186,
            "HIDECHEST", "HIDEPELVIS", "HIDEBELT", "HIDEBICEPL", "HIDEBICEPR",
            "HIDEFOREL", "HIDEFORER", "HIDEHANDL", "HIDEHANDR", "HIDESHOL", "HIDESHOR",
            "HIDELEGL", "HIDELEGR", "HIDESHINL", "HIDESHINR");

        Assert.True(RobePartSuppression.IsSuppressedByRobe("chest", 186, game));
        Assert.True(RobePartSuppression.IsSuppressedByRobe("bicepl", 186, game));
        Assert.True(RobePartSuppression.IsSuppressedByRobe("handr", 186, game));
        // head/neck/feet never hidden
        Assert.False(RobePartSuppression.IsSuppressedByRobe("head", 186, game));
        Assert.False(RobePartSuppression.IsSuppressedByRobe("footl", 186, game));
    }

    [Fact]
    public void NoRobe_SuppressesNothing()
    {
        var game = GameWith(0 /* unused */);
        Assert.False(RobePartSuppression.IsSuppressedByRobe("chest", 0, game));
        Assert.False(RobePartSuppression.IsSuppressedByRobe("pelvis", 0, game));
    }

    [Fact]
    public void HeadNeckFeet_NeverHaveHideColumnsSet_NotSuppressed()
    {
        // Even a full-body robe (186) never hides head/neck/feet in the 2DA.
        var game = GameWith(186, "HIDECHEST"); // only chest set
        Assert.False(RobePartSuppression.IsSuppressedByRobe("head", 186, game));
        Assert.False(RobePartSuppression.IsSuppressedByRobe("neck", 186, game));
        Assert.False(RobePartSuppression.IsSuppressedByRobe("footr", 186, game));
        Assert.False(RobePartSuppression.IsSuppressedByRobe("robe", 186, game)); // the robe itself
    }

    [Fact]
    public void IsCaseInsensitive_OnPartToken()
    {
        var game = GameWith(186, "HIDECHEST");
        Assert.True(RobePartSuppression.IsSuppressedByRobe("CHEST", 186, game));
    }

    [Fact]
    public void MissingColumnOrRow_DefaultsToNotHidden()
    {
        // A robe row with no HIDE columns at all (sparse/custom 2DA) hides nothing.
        var game = new MockGameDataService(includeSampleData: false);
        Assert.False(RobePartSuppression.IsSuppressedByRobe("chest", 42, game));
    }
}
