using Quartermaster.Views.Panels;
using Radoub.Formats.Utc;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for metamagic functionality: level cost calculation, effective level computation,
/// feat detection, and memorized spell metamagic flag handling.
/// </summary>
public class MetamagicTests
{
    // --- GetMetamagicLevelCost tests ---

    [Fact]
    public void GetMetamagicLevelCost_NoMetamagic_ReturnsZero()
    {
        Assert.Equal(0, SpellsPanel.GetMetamagicLevelCost(0x00));
    }

    [Theory]
    [InlineData(0x01, 2)]  // Empower
    [InlineData(0x02, 1)]  // Extend
    [InlineData(0x04, 3)]  // Maximize
    [InlineData(0x08, 4)]  // Quicken
    [InlineData(0x10, 1)]  // Silent
    [InlineData(0x20, 1)]  // Still
    public void GetMetamagicLevelCost_SingleFlag_ReturnsCorrectCost(byte flag, int expectedCost)
    {
        Assert.Equal(expectedCost, SpellsPanel.GetMetamagicLevelCost(flag));
    }

    [Fact]
    public void GetMetamagicLevelCost_EmpowerAndExtend_ReturnsCombinedCost()
    {
        // Empower (2) + Extend (1) = 3
        Assert.Equal(3, SpellsPanel.GetMetamagicLevelCost(0x01 | 0x02));
    }

    [Fact]
    public void GetMetamagicLevelCost_AllFlags_ReturnsTotalCost()
    {
        // Empower(2) + Extend(1) + Maximize(3) + Quicken(4) + Silent(1) + Still(1) = 12
        byte allFlags = 0x01 | 0x02 | 0x04 | 0x08 | 0x10 | 0x20;
        Assert.Equal(12, SpellsPanel.GetMetamagicLevelCost(allFlags));
    }

    // --- GetEffectiveSpellLevel tests ---

    [Fact]
    public void GetEffectiveSpellLevel_NoMetamagic_ReturnsBaseLevel()
    {
        Assert.Equal(3, SpellsPanel.GetEffectiveSpellLevel(3, 0x00));
    }

    [Theory]
    [InlineData(3, 0x01, 5)]  // Fireball (3) + Empower (+2) = 5
    [InlineData(3, 0x02, 4)]  // Fireball (3) + Extend (+1) = 4
    [InlineData(3, 0x04, 6)]  // Fireball (3) + Maximize (+3) = 6
    [InlineData(1, 0x08, 5)]  // Magic Missile (1) + Quicken (+4) = 5
    [InlineData(0, 0x10, 1)]  // Cantrip (0) + Silent (+1) = 1
    [InlineData(9, 0x20, 10)] // Level 9 + Still (+1) = 10 (over max)
    public void GetEffectiveSpellLevel_WithMetamagic_ReturnsAdjustedLevel(
        int baseLevel, byte metamagic, int expectedLevel)
    {
        Assert.Equal(expectedLevel, SpellsPanel.GetEffectiveSpellLevel(baseLevel, metamagic));
    }

    [Fact]
    public void GetEffectiveSpellLevel_CombinedMetamagic_StacksCorrectly()
    {
        // Level 1 + Empower(2) + Extend(1) = 4
        Assert.Equal(4, SpellsPanel.GetEffectiveSpellLevel(1, 0x01 | 0x02));
    }

    // --- GetMetamagicName tests ---

    [Theory]
    [InlineData(0x01, "Empower Spell")]
    [InlineData(0x02, "Extend Spell")]
    [InlineData(0x04, "Maximize Spell")]
    [InlineData(0x08, "Quicken Spell")]
    [InlineData(0x10, "Silent Spell")]
    [InlineData(0x20, "Still Spell")]
    public void GetMetamagicName_KnownFlags_ReturnsCorrectName(byte flag, string expectedName)
    {
        Assert.Equal(expectedName, SpellsPanel.GetMetamagicName(flag));
    }

    [Fact]
    public void GetMetamagicName_ZeroFlag_ReturnsNone()
    {
        Assert.Equal("None", SpellsPanel.GetMetamagicName(0x00));
    }

    // --- Metamagic feat detection from creature FeatList ---

    [Fact]
    public void FeatList_ContainsMetamagicFeatIds_AreDetectable()
    {
        // Standard NWN metamagic feat IDs
        var creature = new UtcFile();
        creature.FeatList.AddRange(new ushort[] { 18, 20, 23 }); // Empower, Maximize, Still

        Assert.Contains((ushort)18, creature.FeatList);
        Assert.Contains((ushort)20, creature.FeatList);
        Assert.Contains((ushort)23, creature.FeatList);
        Assert.DoesNotContain((ushort)19, creature.FeatList); // Extend - not present
        Assert.DoesNotContain((ushort)21, creature.FeatList); // Quicken - not present
        Assert.DoesNotContain((ushort)22, creature.FeatList); // Silent - not present
    }

    [Fact]
    public void FeatList_NoMetamagicFeats_NoneDetected()
    {
        var creature = new UtcFile();
        creature.FeatList.AddRange(new ushort[] { 0, 1, 5, 7, 8 }); // Non-metamagic feats

        var metamagicFeatIds = new ushort[] { 18, 19, 20, 21, 22, 23 };
        foreach (var mmId in metamagicFeatIds)
        {
            Assert.DoesNotContain(mmId, creature.FeatList);
        }
    }

    // --- MemorizedSpell metamagic flag storage ---

    [Fact]
    public void MemorizedSpell_MetamagicFlag_PersistsCorrectly()
    {
        var spell = new MemorizedSpell
        {
            Spell = 100,
            SpellFlags = 0x01,
            SpellMetaMagic = 0x04, // Maximize
            Ready = 1
        };

        Assert.Equal(0x04, spell.SpellMetaMagic);
        Assert.Equal(100, spell.Spell);
    }

    [Fact]
    public void MemorizedSpell_CombinedMetamagicFlags_StoredInSingleByte()
    {
        var spell = new MemorizedSpell
        {
            Spell = 100,
            SpellMetaMagic = 0x01 | 0x02 // Empower + Extend
        };

        Assert.Equal(0x03, spell.SpellMetaMagic);
        Assert.True((spell.SpellMetaMagic & 0x01) != 0); // Has Empower
        Assert.True((spell.SpellMetaMagic & 0x02) != 0); // Has Extend
        Assert.True((spell.SpellMetaMagic & 0x04) == 0); // No Maximize
    }

    [Fact]
    public void KnownSpell_MetamagicFlag_DefaultsToZero()
    {
        var spell = new KnownSpell
        {
            Spell = 100,
            SpellFlags = 0x01
        };

        Assert.Equal(0, spell.SpellMetaMagic);
    }

    // --- Effective level boundary tests ---

    [Fact]
    public void EffectiveLevel_Level9WithEmpower_Exceeds9()
    {
        // Level 9 spell + Empower (+2) = 11, which exceeds max spell level
        int effective = SpellsPanel.GetEffectiveSpellLevel(9, 0x01);
        Assert.Equal(11, effective);
        Assert.True(effective > 9, "Should exceed level 9, making this variant invalid");
    }

    [Fact]
    public void EffectiveLevel_Level7WithExtend_IsValid()
    {
        // Level 7 spell + Extend (+1) = 8, still valid
        int effective = SpellsPanel.GetEffectiveSpellLevel(7, 0x02);
        Assert.Equal(8, effective);
        Assert.True(effective <= 9, "Should be within valid range");
    }

    [Fact]
    public void EffectiveLevel_Level0WithStill_IsValid()
    {
        // Cantrip (0) + Still (+1) = 1
        int effective = SpellsPanel.GetEffectiveSpellLevel(0, 0x20);
        Assert.Equal(1, effective);
    }

    // --- Memorized spell counts with metamagic keys ---

    [Fact]
    public void MemorizedCountKey_SameSpellDifferentMetamagic_AreSeparate()
    {
        var counts = new Dictionary<(int spellId, byte metamagic), int>();

        counts[(100, 0x00)] = 2;  // Base spell memorized 2x
        counts[(100, 0x01)] = 1;  // Empowered variant memorized 1x

        Assert.Equal(2, counts[(100, 0x00)]);
        Assert.Equal(1, counts[(100, 0x01)]);
        Assert.False(counts.ContainsKey((100, 0x04))); // No maximize variant
    }

    [Fact]
    public void MemorizedCountKey_DifferentSpellsSameMetamagic_AreSeparate()
    {
        var counts = new Dictionary<(int spellId, byte metamagic), int>();

        counts[(100, 0x01)] = 1;  // Spell 100 Empowered
        counts[(200, 0x01)] = 3;  // Spell 200 Empowered

        Assert.Equal(1, counts[(100, 0x01)]);
        Assert.Equal(3, counts[(200, 0x01)]);
    }
}
