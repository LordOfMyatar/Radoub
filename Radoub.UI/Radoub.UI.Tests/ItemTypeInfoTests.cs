using Radoub.UI.Models;
using Xunit;

namespace Radoub.UI.Tests;

public class ItemTypeInfoTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var info = new ItemTypeInfo(5, "Longsword", "longsword");

        Assert.Equal(5, info.BaseItemIndex);
        Assert.Equal("Longsword", info.Name);
        Assert.Equal("longsword", info.Label);
    }

    [Fact]
    public void IsAllTypes_FalseForNormalType()
    {
        var info = new ItemTypeInfo(5, "Longsword", "longsword");

        Assert.False(info.IsAllTypes);
    }

    [Fact]
    public void AllTypes_HasNegativeIndex()
    {
        Assert.Equal(-1, ItemTypeInfo.AllTypes.BaseItemIndex);
    }

    [Fact]
    public void AllTypes_IsAllTypesReturnsTrue()
    {
        Assert.True(ItemTypeInfo.AllTypes.IsAllTypes);
    }

    [Fact]
    public void AllTypes_HasCorrectName()
    {
        Assert.Equal("All Types", ItemTypeInfo.AllTypes.Name);
    }

    [Fact]
    public void ToString_ReturnsName()
    {
        var info = new ItemTypeInfo(5, "Longsword", "longsword");

        Assert.Equal("Longsword", info.ToString());
    }
}
