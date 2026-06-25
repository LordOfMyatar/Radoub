using Radoub.UI.Models;
using Xunit;

namespace Radoub.UI.Tests;

public class ItemTypeInfoTests
{
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
}
