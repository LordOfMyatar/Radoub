using Radoub.Formats.Utc;
using Xunit;

namespace Radoub.TestUtilities.Assertions;

/// <summary>
/// Assertion helpers for comparing creature structures.
/// </summary>
public static class CreatureAssertions
{
    /// <summary>
    /// Assert that two creatures have equal identity fields.
    /// </summary>
    public static void AssertIdentityEqual(UtcFile expected, UtcFile actual)
    {
        GffAssertions.AssertEqual(expected.FirstName, actual.FirstName, "FirstName");
        GffAssertions.AssertEqual(expected.LastName, actual.LastName, "LastName");
        Assert.Equal(expected.Tag, actual.Tag);
        Assert.Equal(expected.TemplateResRef, actual.TemplateResRef);
    }

    /// <summary>
    /// Assert that two creatures have equal basic stats.
    /// </summary>
    public static void AssertBasicStatsEqual(UtcFile expected, UtcFile actual)
    {
        Assert.Equal(expected.Race, actual.Race);
        Assert.Equal(expected.Gender, actual.Gender);
        Assert.Equal(expected.Str, actual.Str);
        Assert.Equal(expected.Dex, actual.Dex);
        Assert.Equal(expected.Con, actual.Con);
        Assert.Equal(expected.Int, actual.Int);
        Assert.Equal(expected.Wis, actual.Wis);
        Assert.Equal(expected.Cha, actual.Cha);
    }

    /// <summary>
    /// Assert that two creatures have equal class lists.
    /// </summary>
    public static void AssertClassesEqual(UtcFile expected, UtcFile actual)
    {
        Assert.Equal(expected.ClassList.Count, actual.ClassList.Count);

        for (int i = 0; i < expected.ClassList.Count; i++)
        {
            Assert.Equal(expected.ClassList[i].Class, actual.ClassList[i].Class);
            Assert.Equal(expected.ClassList[i].ClassLevel, actual.ClassList[i].ClassLevel);
        }
    }

    /// <summary>
    /// Assert that a creature has specific ability scores.
    /// </summary>
    public static void AssertAbilities(UtcFile creature, byte str, byte dex, byte con, byte intel, byte wis, byte cha)
    {
        Assert.Equal(str, creature.Str);
        Assert.Equal(dex, creature.Dex);
        Assert.Equal(con, creature.Con);
        Assert.Equal(intel, creature.Int);
        Assert.Equal(wis, creature.Wis);
        Assert.Equal(cha, creature.Cha);
    }

    /// <summary>
    /// Assert that a creature has expected class levels.
    /// </summary>
    public static void AssertHasClass(UtcFile creature, int classIndex, short minLevel)
    {
        var classEntry = creature.ClassList.FirstOrDefault(c => c.Class == classIndex);
        Assert.NotNull(classEntry);
        Assert.True(classEntry!.ClassLevel >= minLevel,
            $"Expected class {classIndex} to have at least level {minLevel}, but was {classEntry.ClassLevel}");
    }

    /// <summary>
    /// Assert that a creature's total level equals expected.
    /// </summary>
    public static void AssertTotalLevel(UtcFile creature, int expectedLevel)
    {
        var totalLevel = creature.ClassList.Sum(c => c.ClassLevel);
        Assert.Equal(expectedLevel, totalLevel);
    }

    /// <summary>
    /// Assert that a creature has a specific feat.
    /// </summary>
    public static void AssertHasFeat(UtcFile creature, ushort featIndex)
    {
        Assert.Contains(featIndex, creature.FeatList);
    }

    /// <summary>
    /// Assert that a creature has specific equipment.
    /// </summary>
    public static void AssertHasEquipment(UtcFile creature, int slot, string itemResRef)
    {
        var equipped = creature.EquipItemList.FirstOrDefault(e => e.Slot == slot);
        Assert.NotNull(equipped);
        Assert.Equal(itemResRef, equipped!.EquipRes);
    }

    /// <summary>
    /// Assert that a creature has an item in inventory.
    /// </summary>
    public static void AssertHasInventoryItem(UtcFile creature, string itemResRef)
    {
        Assert.True(creature.ItemList.Any(i => i.InventoryRes == itemResRef),
            $"Expected creature to have item '{itemResRef}' in inventory");
    }
}
