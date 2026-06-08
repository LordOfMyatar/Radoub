using Radoub.UI.Utils;
using Xunit;

namespace Radoub.UI.Tests.Utils;

/// <summary>Tests for the shared palette base-item exclusion list (#2411).</summary>
public class ItemPaletteExclusionsTests
{
    [Theory]
    [InlineData(69)]  // Creature Bite
    [InlineData(70)]  // Creature Claw
    [InlineData(71)]  // Creature Gore
    [InlineData(72)]  // Creature Slashing
    [InlineData(73)]  // Creature Piercing/Bludgeoning
    [InlineData(255)] // Invalid marker
    public void IsExcluded_CreatureWeaponsAndMarker_True(int baseItemType)
    {
        Assert.True(ItemPaletteExclusions.IsExcluded(baseItemType));
    }

    [Theory]
    [InlineData(0)]  // Short Sword
    [InlineData(2)]  // Bastard Sword
    [InlineData(68)] // (last normal-ish weapon before creature weapons)
    public void IsExcluded_NormalItems_False(int baseItemType)
    {
        Assert.False(ItemPaletteExclusions.IsExcluded(baseItemType));
    }
}
