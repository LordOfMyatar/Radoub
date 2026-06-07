using Quartermaster.Services;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for robe body-part suppression (#1989). NWN robe models are a single combined
/// mesh covering torso + pelvis + upper legs (and, for long-sleeve robes like CEP Robe
/// #186, shoulders + biceps). The composer must skip those individual parts when a robe
/// is equipped, or they z-fight / leave gaps. Extremities (head, neck, forearms, hands,
/// shins, feet) and the belt continue to load.
/// </summary>
public class RobePartSuppressionTests
{
    // Robe #186 supplies its own torso/pelvis/biceps/thighs/forearms/shins/hands geometry.
    [Theory]
    [InlineData("chest")]
    [InlineData("pelvis")]
    [InlineData("legl")]
    [InlineData("legr")]
    [InlineData("shol")]
    [InlineData("shor")]
    [InlineData("bicepl")]
    [InlineData("bicepr")]
    [InlineData("forel")]
    [InlineData("forer")]
    [InlineData("handl")]
    [InlineData("handr")]
    [InlineData("shinl")]
    [InlineData("shinr")]
    public void RobeActive_SuppressesCoveredParts(string partType)
        => Assert.True(RobePartSuppression.IsSuppressedByRobe(partType, robeActive: true));

    // The robe has no head/neck/foot geometry — those still load. Belt is kept (separate cinch).
    [Theory]
    [InlineData("head")]
    [InlineData("neck")]
    [InlineData("robe")]
    [InlineData("belt")]
    [InlineData("footl")]
    [InlineData("footr")]
    public void RobeActive_PreservesHeadNeckFeet(string partType)
        => Assert.False(RobePartSuppression.IsSuppressedByRobe(partType, robeActive: true));

    [Theory]
    [InlineData("chest")]
    [InlineData("pelvis")]
    [InlineData("legl")]
    [InlineData("shol")]
    public void NoRobe_SuppressesNothing(string partType)
        => Assert.False(RobePartSuppression.IsSuppressedByRobe(partType, robeActive: false));

    [Fact]
    public void IsCaseInsensitive()
        => Assert.True(RobePartSuppression.IsSuppressedByRobe("CHEST", robeActive: true));
}
