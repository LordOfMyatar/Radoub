using Radoub.UI.Services;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for PltColorIndices — pure data construction with 10 color channels.
/// </summary>
public class PltColorIndicesTests
{
    [Fact]
    public void DefaultConstructor_AllChannelsZero()
    {
        var colors = new PltColorIndices();

        Assert.Equal(0, colors.Skin);
        Assert.Equal(0, colors.Hair);
        Assert.Equal(0, colors.Metal1);
        Assert.Equal(0, colors.Metal2);
        Assert.Equal(0, colors.Cloth1);
        Assert.Equal(0, colors.Cloth2);
        Assert.Equal(0, colors.Leather1);
        Assert.Equal(0, colors.Leather2);
        Assert.Equal(0, colors.Tattoo1);
        Assert.Equal(0, colors.Tattoo2);
    }

    [Fact]
    public void FromCreatureAndArmor_AllChannelsMappedCorrectly()
    {
        var colors = PltColorIndices.FromCreatureAndArmor(
            skinColor: 10, hairColor: 20, tattoo1: 30, tattoo2: 40,
            metal1: 50, metal2: 60, cloth1: 70, cloth2: 80,
            leather1: 90, leather2: 100);

        Assert.Equal(10, colors.Skin);
        Assert.Equal(20, colors.Hair);
        Assert.Equal(30, colors.Tattoo1);
        Assert.Equal(40, colors.Tattoo2);
        Assert.Equal(50, colors.Metal1);
        Assert.Equal(60, colors.Metal2);
        Assert.Equal(70, colors.Cloth1);
        Assert.Equal(80, colors.Cloth2);
        Assert.Equal(90, colors.Leather1);
        Assert.Equal(100, colors.Leather2);
    }

    [Fact]
    public void FromCreatureAndArmor_DefaultArmorColors_AreZero()
    {
        var colors = PltColorIndices.FromCreatureAndArmor(
            skinColor: 5, hairColor: 10, tattoo1: 15, tattoo2: 20);

        Assert.Equal(5, colors.Skin);
        Assert.Equal(10, colors.Hair);
        Assert.Equal(15, colors.Tattoo1);
        Assert.Equal(20, colors.Tattoo2);
        Assert.Equal(0, colors.Metal1);
        Assert.Equal(0, colors.Metal2);
        Assert.Equal(0, colors.Cloth1);
        Assert.Equal(0, colors.Cloth2);
        Assert.Equal(0, colors.Leather1);
        Assert.Equal(0, colors.Leather2);
    }

    [Fact]
    public void FromCreatureAndArmor_EdgeCase_ZeroValues()
    {
        var colors = PltColorIndices.FromCreatureAndArmor(0, 0, 0, 0);

        Assert.Equal(0, colors.Skin);
        Assert.Equal(0, colors.Hair);
        Assert.Equal(0, colors.Tattoo1);
        Assert.Equal(0, colors.Tattoo2);
    }

    [Fact]
    public void FromCreatureAndArmor_EdgeCase_MaxByteValues()
    {
        var colors = PltColorIndices.FromCreatureAndArmor(
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255);

        Assert.Equal(255, colors.Skin);
        Assert.Equal(255, colors.Hair);
        Assert.Equal(255, colors.Metal1);
        Assert.Equal(255, colors.Metal2);
        Assert.Equal(255, colors.Cloth1);
        Assert.Equal(255, colors.Cloth2);
        Assert.Equal(255, colors.Leather1);
        Assert.Equal(255, colors.Leather2);
        Assert.Equal(255, colors.Tattoo1);
        Assert.Equal(255, colors.Tattoo2);
    }
}
