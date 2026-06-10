using System.Linq;
using PlaceableEditor.Services;
using PlaceableEditor.ViewModels;
using Radoub.Formats.Mdl;
using Xunit;

namespace PlaceableEditor.Tests.Services;

/// <summary>
/// State→animation resolution for the placeable preview (#2431). The available states for a model
/// are Default (always) plus any of Open/Closed/Destroyed/Activated/Deactivated whose required MDL
/// animation (open/close/dead/on/off — BioWare Door/Placeable spec Table 4.1.2) is present.
/// </summary>
public class PlaceableStateResolverTests
{
    private static MdlModel ModelWith(params string[] animationNames)
    {
        var model = new MdlModel();
        foreach (var name in animationNames)
            model.Animations.Add(new MdlAnimation { Name = name, Length = 1.0f });
        return model;
    }

    [Fact]
    public void ModelWithNoAnimations_OnlyDefault()
    {
        var states = PlaceableStateResolver.AvailableStates(ModelWith());
        Assert.Equal(new byte[] { 0 }, states.Select(s => s.Value).ToArray());
    }

    [Fact]
    public void NullModel_OnlyDefault()
    {
        var states = PlaceableStateResolver.AvailableStates(null);
        Assert.Single(states);
        Assert.Equal(0, states[0].Value);
    }

    [Fact]
    public void OpenCloseAnimations_AddOpenAndClosedStates()
    {
        var states = PlaceableStateResolver.AvailableStates(ModelWith("open", "close"));
        Assert.Equal(new byte[] { 0, 1, 2 }, states.Select(s => s.Value).ToArray());
    }

    [Fact]
    public void AllStateAnimations_AllSixStates()
    {
        var states = PlaceableStateResolver.AvailableStates(ModelWith("open", "close", "dead", "on", "off"));
        Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5 }, states.Select(s => s.Value).ToArray());
    }

    [Fact]
    public void AnimationMatchIsCaseInsensitive()
    {
        var states = PlaceableStateResolver.AvailableStates(ModelWith("DEAD"));
        Assert.Contains(states, s => s.Value == 3); // Destroyed
    }

    [Fact]
    public void UnrelatedAnimations_Ignored()
    {
        var states = PlaceableStateResolver.AvailableStates(ModelWith("walk", "idle", "pause1"));
        Assert.Equal(new byte[] { 0 }, states.Select(s => s.Value).ToArray());
    }

    [Theory]
    [InlineData(1, "open")]
    [InlineData(2, "close")]
    [InlineData(3, "dead")]
    [InlineData(4, "on")]
    [InlineData(5, "off")]
    public void AnimationNameForState_MatchesSpec(byte state, string expected)
    {
        Assert.Equal(expected, PlaceableStateResolver.AnimationNameForState(state));
    }

    [Fact]
    public void AnimationNameForDefault_IsNull()
    {
        Assert.Null(PlaceableStateResolver.AnimationNameForState(0));
    }

    [Fact]
    public void FindAnimation_ReturnsMatchingMdlAnimation()
    {
        var model = ModelWith("open", "close");
        var anim = PlaceableStateResolver.FindAnimation(model, 1); // Open → "open"
        Assert.NotNull(anim);
        Assert.Equal("open", anim!.Name);
    }

    [Fact]
    public void FindAnimation_DefaultState_ReturnsNull()
    {
        var model = ModelWith("open");
        Assert.Null(PlaceableStateResolver.FindAnimation(model, 0));
    }

    [Fact]
    public void FindAnimation_MissingAnimation_ReturnsNull()
    {
        var model = ModelWith("open");
        Assert.Null(PlaceableStateResolver.FindAnimation(model, 3)); // no "dead"
    }
}
