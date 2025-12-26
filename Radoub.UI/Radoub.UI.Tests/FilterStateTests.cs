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

    [Fact]
    public void Defaults_ShowCustomTrue()
    {
        var state = new FilterState();

        Assert.True(state.ShowCustom);
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
            ShowCustom = false,
            SearchText = "sword",
            SelectedBaseItemIndex = 5
        };

        Assert.False(state.ShowStandard);
        Assert.False(state.ShowCustom);
        Assert.Equal("sword", state.SearchText);
        Assert.Equal(5, state.SelectedBaseItemIndex);
    }
}
