using Quartermaster.Services;
using Radoub.Formats.Uti;
using Radoub.TestUtilities.Builders;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for CreatureDisplayService.Combat — BAB, saves, equipment attack bonus,
/// combat stats breakdown, and attack sequences.
///
/// Tests are written against desired NWN rules behavior:
/// - BAB: Full (+1/lvl), 3/4 (floor(lvl*0.75)), 1/2 (floor(lvl/2))
/// - Saves: Good = 2 + floor(lvl/2), Poor = floor(lvl/3)
/// - Enhancement bonuses don't stack (highest wins), attack bonuses stack
/// - Multiclass: sum each class's BAB/saves independently
/// - APR: min(4, 1 + floor((BAB-1)/5)) for BAB >= 1; epic BAB doesn't add APR
///
/// Per #1654 TDD philosophy: tests describe desired behavior per NWN rules.
/// The 2DA path is the "correct" path; fallback estimates are tested for reasonableness.
/// </summary>
public class CombatCalculationTests
{
    #region Test Infrastructure

    /// <summary>
    /// Creates a MockGameDataService configured with standard NWN BAB tables.
    /// classes.2da points each class at its AttackBonusTable, and those tables
    /// contain the actual BAB values per level.
    /// </summary>
    private static (MockGameDataService mock, CreatureDisplayService service) CreateServiceWith2DABabData()
    {
        var mock = new MockGameDataService(includeSampleData: true);

        // Full BAB classes: Barbarian(0), Fighter(4), Paladin(6), Ranger(7)
        foreach (var classId in new[] { 0, 4, 6, 7 })
        {
            mock.Set2DAValue("classes", classId, "AttackBonusTable", "cls_atk_1");
        }

        // 3/4 BAB classes: Bard(1), Cleric(2), Druid(3), Monk(5), Rogue(8)
        foreach (var classId in new[] { 1, 2, 3, 5, 8 })
        {
            mock.Set2DAValue("classes", classId, "AttackBonusTable", "cls_atk_3");
        }

        // 1/2 BAB classes: Sorcerer(9), Wizard(10)
        foreach (var classId in new[] { 9, 10 })
        {
            mock.Set2DAValue("classes", classId, "AttackBonusTable", "cls_atk_2");
        }

        // Prestige full BAB: Arcane Archer(13), Blackguard(15), CoT(16), Weapon Master(17), Dwarven Defender(20), PDK(27)
        foreach (var classId in new[] { 13, 15, 16, 17, 20, 27 })
        {
            mock.Set2DAValue("classes", classId, "AttackBonusTable", "cls_atk_1");
        }

        // Prestige 3/4 BAB: Shadowdancer(11), Harper Scout(12), Assassin(14), Shifter(19), Dragon Disciple(21)
        foreach (var classId in new[] { 11, 12, 14, 19, 21 })
        {
            mock.Set2DAValue("classes", classId, "AttackBonusTable", "cls_atk_3");
        }

        // Prestige 1/2 BAB: Pale Master(18)
        mock.Set2DAValue("classes", 18, "AttackBonusTable", "cls_atk_2");

        // Full BAB table (cls_atk_1): BAB = level
        for (int level = 1; level <= 40; level++)
        {
            mock.Set2DAValue("cls_atk_1", level - 1, "BAB", level.ToString());
        }

        // 3/4 BAB table (cls_atk_3): BAB = floor(level * 3/4)
        for (int level = 1; level <= 40; level++)
        {
            int bab = level * 3 / 4;
            mock.Set2DAValue("cls_atk_3", level - 1, "BAB", bab.ToString());
        }

        // 1/2 BAB table (cls_atk_2): BAB = floor(level / 2)
        for (int level = 1; level <= 40; level++)
        {
            int bab = level / 2;
            mock.Set2DAValue("cls_atk_2", level - 1, "BAB", bab.ToString());
        }

        var service = new CreatureDisplayService(mock);
        return (mock, service);
    }

    /// <summary>
    /// Creates a MockGameDataService configured with standard NWN save tables.
    /// </summary>
    private static (MockGameDataService mock, CreatureDisplayService service) CreateServiceWith2DASaveData()
    {
        var mock = new MockGameDataService(includeSampleData: true);

        // Map each class to its save table
        // Fighter(4), Barbarian(0): Fort good, Ref/Will poor
        // Rogue(8): Ref good, Fort/Will poor
        // Wizard(10), Sorcerer(9): Will good, Fort/Ref poor
        // Monk(5): All good
        // Cleric(2), Druid(3): Fort/Will good, Ref poor
        // Bard(1): Ref/Will good, Fort poor
        // Paladin(6): Fort good, Ref/Will poor
        // Ranger(7): Fort/Ref good, Will poor

        // We'll use descriptive table names to make intent clear
        var classConfigs = new (int classId, string table, bool fortGood, bool refGood, bool willGood)[]
        {
            (0, "cls_savthr_barb", true, false, false),    // Barbarian
            (1, "cls_savthr_bard", false, true, true),     // Bard
            (2, "cls_savthr_cler", true, false, true),     // Cleric
            (3, "cls_savthr_dru", true, false, true),      // Druid
            (4, "cls_savthr_figh", true, false, false),    // Fighter
            (5, "cls_savthr_monk", true, true, true),      // Monk
            (6, "cls_savthr_pal", true, false, false),     // Paladin
            (7, "cls_savthr_rang", true, true, false),     // Ranger
            (8, "cls_savthr_rog", false, true, false),     // Rogue
            (9, "cls_savthr_sorc", false, false, true),    // Sorcerer
            (10, "cls_savthr_wiz", false, false, true),    // Wizard
        };

        foreach (var (classId, table, fortGood, refGood, willGood) in classConfigs)
        {
            mock.Set2DAValue("classes", classId, "SavingThrowTable", table);

            for (int level = 1; level <= 20; level++)
            {
                int goodSave = 2 + level / 2;
                int poorSave = level / 3;

                mock.Set2DAValue(table, level - 1, "FortSave", (fortGood ? goodSave : poorSave).ToString());
                mock.Set2DAValue(table, level - 1, "RefSave", (refGood ? goodSave : poorSave).ToString());
                mock.Set2DAValue(table, level - 1, "WillSave", (willGood ? goodSave : poorSave).ToString());
            }
        }

        var service = new CreatureDisplayService(mock);
        return (mock, service);
    }

    #endregion

    #region BAB via 2DA — Full Progression

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 5)]
    [InlineData(10, 10)]
    [InlineData(20, 20)]
    public void GetClassBab_FullBab_FromTable_ReturnsLevelEqualsBAB(int level, int expectedBab)
    {
        // Full BAB: Fighter gets +1 per level
        var (_, service) = CreateServiceWith2DABabData();
        Assert.Equal(expectedBab, service.GetClassBab(4, level)); // Fighter
    }

    [Theory]
    [InlineData(1, 0)]   // floor(1 * 0.75) = 0
    [InlineData(2, 1)]   // floor(2 * 0.75) = 1
    [InlineData(4, 3)]   // floor(4 * 0.75) = 3
    [InlineData(5, 3)]   // floor(5 * 0.75) = 3  (NOT 4 — this is the key 3/4 pattern)
    [InlineData(8, 6)]   // floor(8 * 0.75) = 6
    [InlineData(10, 7)]  // floor(10 * 0.75) = 7
    [InlineData(13, 9)]  // floor(13 * 0.75) = 9
    [InlineData(20, 15)] // floor(20 * 0.75) = 15
    public void GetClassBab_ThreeQuarterBab_FromTable_ReturnsCorrectValues(int level, int expectedBab)
    {
        // 3/4 BAB: Cleric, Rogue, Bard, Druid, Monk
        var (_, service) = CreateServiceWith2DABabData();
        Assert.Equal(expectedBab, service.GetClassBab(2, level)); // Cleric
    }

    [Theory]
    [InlineData(1, 0)]   // floor(1 / 2) = 0
    [InlineData(2, 1)]   // floor(2 / 2) = 1
    [InlineData(3, 1)]   // floor(3 / 2) = 1
    [InlineData(5, 2)]   // floor(5 / 2) = 2
    [InlineData(10, 5)]  // floor(10 / 2) = 5
    [InlineData(20, 10)] // floor(20 / 2) = 10
    public void GetClassBab_HalfBab_FromTable_ReturnsCorrectValues(int level, int expectedBab)
    {
        // 1/2 BAB: Wizard, Sorcerer
        var (_, service) = CreateServiceWith2DABabData();
        Assert.Equal(expectedBab, service.GetClassBab(10, level)); // Wizard
    }

    #endregion

    #region BAB via 2DA — All Full BAB Classes

    [Theory]
    [InlineData(0, "Barbarian")]
    [InlineData(4, "Fighter")]
    [InlineData(6, "Paladin")]
    [InlineData(7, "Ranger")]
    public void GetClassBab_BaseFullBabClasses_Level10Returns10(int classId, string _)
    {
        var (_, service) = CreateServiceWith2DABabData();
        Assert.Equal(10, service.GetClassBab(classId, 10));
    }

    [Theory]
    [InlineData(1, "Bard")]
    [InlineData(2, "Cleric")]
    [InlineData(3, "Druid")]
    [InlineData(5, "Monk")]
    [InlineData(8, "Rogue")]
    public void GetClassBab_BaseThreeQuarterBabClasses_Level10Returns7(int classId, string _)
    {
        var (_, service) = CreateServiceWith2DABabData();
        Assert.Equal(7, service.GetClassBab(classId, 10));
    }

    [Theory]
    [InlineData(9, "Sorcerer")]
    [InlineData(10, "Wizard")]
    public void GetClassBab_BaseHalfBabClasses_Level10Returns5(int classId, string _)
    {
        var (_, service) = CreateServiceWith2DABabData();
        Assert.Equal(5, service.GetClassBab(classId, 10));
    }

    #endregion

    #region BAB via 2DA — Prestige Classes

    [Theory]
    [InlineData(13, "Arcane Archer")]
    [InlineData(15, "Blackguard")]
    [InlineData(16, "Champion of Torm")]
    [InlineData(17, "Weapon Master")]
    [InlineData(20, "Dwarven Defender")]
    [InlineData(27, "Purple Dragon Knight")]
    public void GetClassBab_PrestigeFullBab_Level10Returns10(int classId, string _)
    {
        var (_, service) = CreateServiceWith2DABabData();
        Assert.Equal(10, service.GetClassBab(classId, 10));
    }

    [Theory]
    [InlineData(11, "Shadowdancer")]
    [InlineData(12, "Harper Scout")]
    [InlineData(14, "Assassin")]
    [InlineData(19, "Shifter")]
    [InlineData(21, "Dragon Disciple")]
    public void GetClassBab_PrestigeThreeQuarterBab_Level10Returns7(int classId, string _)
    {
        var (_, service) = CreateServiceWith2DABabData();
        Assert.Equal(7, service.GetClassBab(classId, 10));
    }

    [Fact]
    public void GetClassBab_PaleMaster_Level10Returns5()
    {
        // Pale Master is the only 1/2 BAB prestige class
        var (_, service) = CreateServiceWith2DABabData();
        Assert.Equal(5, service.GetClassBab(18, 10));
    }

    #endregion

    #region BAB — Multiclass via 2DA

    [Fact]
    public void CalculateBAB_FighterRogue_SumsIndependently()
    {
        // Fighter 10 / Rogue 10: BAB = 10 + 7 = 17
        var (_, service) = CreateServiceWith2DABabData();
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 10)
            .WithClass(CommonClass.Rogue, 10)
            .Build();

        Assert.Equal(17, service.CalculateBaseAttackBonus(creature));
    }

    [Fact]
    public void CalculateBAB_FighterWizard_SumsIndependently()
    {
        // Fighter 5 / Wizard 5: BAB = 5 + 2 = 7
        var (_, service) = CreateServiceWith2DABabData();
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 5)
            .WithClass(CommonClass.Wizard, 5)
            .Build();

        Assert.Equal(7, service.CalculateBaseAttackBonus(creature));
    }

    [Fact]
    public void CalculateBAB_ThreeClass_SumsAll()
    {
        // Fighter 6 / Wizard 4 / Rogue 4: BAB = 6 + 2 + 3 = 11
        var (_, service) = CreateServiceWith2DABabData();
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 6)
            .WithClass(CommonClass.Wizard, 4)
            .WithClass(CommonClass.Rogue, 4)
            .Build();

        Assert.Equal(11, service.CalculateBaseAttackBonus(creature));
    }

    [Fact]
    public void CalculateBAB_Cleric20_Returns15()
    {
        // Cleric 20: 3/4 BAB = floor(20 * 0.75) = 15
        var (_, service) = CreateServiceWith2DABabData();
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Cleric, 20)
            .Build();

        Assert.Equal(15, service.CalculateBaseAttackBonus(creature));
    }

    #endregion

    #region BAB — Edge Cases

    [Fact]
    public void GetClassBab_ZeroLevel_ReturnsZero()
    {
        var (_, service) = CreateServiceWith2DABabData();
        Assert.Equal(0, service.GetClassBab(4, 0));
    }

    [Fact]
    public void GetClassBab_NegativeLevel_ReturnsZero()
    {
        var (_, service) = CreateServiceWith2DABabData();
        Assert.Equal(0, service.GetClassBab(4, -1));
    }

    [Fact]
    public void CalculateBAB_EmptyClassList_ReturnsZero()
    {
        var (_, service) = CreateServiceWith2DABabData();
        var creature = new CreatureBuilder().Build();

        Assert.Equal(0, service.CalculateBaseAttackBonus(creature));
    }

    #endregion

    #region BAB — Fallback Estimates (no 2DA data)

    [Theory]
    [InlineData(0, 10, 10)]  // Barbarian: full
    [InlineData(4, 10, 10)]  // Fighter: full
    [InlineData(6, 10, 10)]  // Paladin: full
    [InlineData(7, 10, 10)]  // Ranger: full
    public void EstimateBab_FullBabClasses_ReturnsReasonableValues(int classId, int level, int expectedBab)
    {
        // With no 2DA data, fallback should still give correct full BAB
        var mock = new MockGameDataService(includeSampleData: false);
        var service = new CreatureDisplayService(mock);
        Assert.Equal(expectedBab, service.GetClassBab(classId, level));
    }

    [Theory]
    [InlineData(2, 10)]  // Cleric: 3/4
    [InlineData(8, 10)]  // Rogue: 3/4
    [InlineData(5, 10)]  // Monk: 3/4
    public void EstimateBab_ThreeQuarterBabClasses_ReturnsApproximation(int classId, int level)
    {
        var mock = new MockGameDataService(includeSampleData: false);
        var service = new CreatureDisplayService(mock);
        var bab = service.GetClassBab(classId, level);

        // floor(10 * 0.75) = 7; (int)(10 * 0.75) = 7 — both approaches give 7
        // Allow small tolerance since this is a fallback approximation
        Assert.InRange(bab, 6, 8);
    }

    [Theory]
    [InlineData(9, 10)]   // Sorcerer: 1/2
    [InlineData(10, 10)]  // Wizard: 1/2
    public void EstimateBab_HalfBabClasses_ReturnsApproximation(int classId, int level)
    {
        var mock = new MockGameDataService(includeSampleData: false);
        var service = new CreatureDisplayService(mock);
        var bab = service.GetClassBab(classId, level);

        // floor(10 / 2) = 5; (int)(10 * 0.5) = 5
        Assert.InRange(bab, 4, 6);
    }

    #endregion

    #region Saving Throws via 2DA

    [Theory]
    [InlineData(1, 2, 0, 0)]   // Level 1: Good=2, Poor=0
    [InlineData(5, 4, 1, 1)]   // Level 5: Good=4, Poor=1
    [InlineData(10, 7, 3, 3)]  // Level 10: Good=7, Poor=3
    [InlineData(20, 12, 6, 6)] // Level 20: Good=12, Poor=6
    public void GetClassSaves_Fighter_FortGoodRefWillPoor(int level, int expectedFort, int expectedRef, int expectedWill)
    {
        var (_, service) = CreateServiceWith2DASaveData();
        var saves = service.GetClassSaves(4, level); // Fighter

        Assert.Equal(expectedFort, saves.Fortitude);
        Assert.Equal(expectedRef, saves.Reflex);
        Assert.Equal(expectedWill, saves.Will);
    }

    [Theory]
    [InlineData(1, 0, 2, 0)]   // Level 1
    [InlineData(5, 1, 4, 1)]   // Level 5
    [InlineData(10, 3, 7, 3)]  // Level 10
    [InlineData(20, 6, 12, 6)] // Level 20
    public void GetClassSaves_Rogue_RefGoodFortWillPoor(int level, int expectedFort, int expectedRef, int expectedWill)
    {
        var (_, service) = CreateServiceWith2DASaveData();
        var saves = service.GetClassSaves(8, level); // Rogue

        Assert.Equal(expectedFort, saves.Fortitude);
        Assert.Equal(expectedRef, saves.Reflex);
        Assert.Equal(expectedWill, saves.Will);
    }

    [Theory]
    [InlineData(1, 0, 0, 2)]   // Level 1
    [InlineData(10, 3, 3, 7)]  // Level 10
    [InlineData(20, 6, 6, 12)] // Level 20
    public void GetClassSaves_Wizard_WillGoodFortRefPoor(int level, int expectedFort, int expectedRef, int expectedWill)
    {
        var (_, service) = CreateServiceWith2DASaveData();
        var saves = service.GetClassSaves(10, level); // Wizard

        Assert.Equal(expectedFort, saves.Fortitude);
        Assert.Equal(expectedRef, saves.Reflex);
        Assert.Equal(expectedWill, saves.Will);
    }

    [Theory]
    [InlineData(1, 2, 2, 2)]    // Level 1: all good
    [InlineData(10, 7, 7, 7)]   // Level 10: all good
    [InlineData(20, 12, 12, 12)] // Level 20: all good
    public void GetClassSaves_Monk_AllGood(int level, int expectedFort, int expectedRef, int expectedWill)
    {
        var (_, service) = CreateServiceWith2DASaveData();
        var saves = service.GetClassSaves(5, level); // Monk

        Assert.Equal(expectedFort, saves.Fortitude);
        Assert.Equal(expectedRef, saves.Reflex);
        Assert.Equal(expectedWill, saves.Will);
    }

    [Theory]
    [InlineData(1, 2, 0, 2)]    // Level 1
    [InlineData(10, 7, 3, 7)]   // Level 10
    [InlineData(20, 12, 6, 12)] // Level 20
    public void GetClassSaves_Cleric_FortWillGoodRefPoor(int level, int expectedFort, int expectedRef, int expectedWill)
    {
        var (_, service) = CreateServiceWith2DASaveData();
        var saves = service.GetClassSaves(2, level); // Cleric

        Assert.Equal(expectedFort, saves.Fortitude);
        Assert.Equal(expectedRef, saves.Reflex);
        Assert.Equal(expectedWill, saves.Will);
    }

    [Theory]
    [InlineData(1, 0, 2, 2)]    // Level 1
    [InlineData(10, 3, 7, 7)]   // Level 10
    public void GetClassSaves_Bard_RefWillGoodFortPoor(int level, int expectedFort, int expectedRef, int expectedWill)
    {
        var (_, service) = CreateServiceWith2DASaveData();
        var saves = service.GetClassSaves(1, level); // Bard

        Assert.Equal(expectedFort, saves.Fortitude);
        Assert.Equal(expectedRef, saves.Reflex);
        Assert.Equal(expectedWill, saves.Will);
    }

    [Theory]
    [InlineData(1, 2, 2, 0)]    // Level 1
    [InlineData(10, 7, 7, 3)]   // Level 10
    public void GetClassSaves_Ranger_FortRefGoodWillPoor(int level, int expectedFort, int expectedRef, int expectedWill)
    {
        var (_, service) = CreateServiceWith2DASaveData();
        var saves = service.GetClassSaves(7, level); // Ranger

        Assert.Equal(expectedFort, saves.Fortitude);
        Assert.Equal(expectedRef, saves.Reflex);
        Assert.Equal(expectedWill, saves.Will);
    }

    #endregion

    #region Saving Throws — Multiclass via 2DA

    [Fact]
    public void CalculateBaseSavingThrows_FighterWizard_SumsIndependently()
    {
        // Fighter 5 / Wizard 5
        // Fighter: Fort=4(good), Ref=1(poor), Will=1(poor)
        // Wizard:  Fort=1(poor), Ref=1(poor), Will=4(good)
        // Total:   Fort=5, Ref=2, Will=5
        var (_, service) = CreateServiceWith2DASaveData();
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 5)
            .WithClass(CommonClass.Wizard, 5)
            .Build();

        var saves = service.CalculateBaseSavingThrows(creature);

        Assert.Equal(5, saves.Fortitude);
        Assert.Equal(2, saves.Reflex);
        Assert.Equal(5, saves.Will);
    }

    [Fact]
    public void CalculateBaseSavingThrows_FighterRogue_SumsIndependently()
    {
        // Fighter 10 / Rogue 10
        // Fighter: Fort=7(good), Ref=3(poor), Will=3(poor)
        // Rogue:   Fort=3(poor), Ref=7(good), Will=3(poor)
        // Total:   Fort=10, Ref=10, Will=6
        var (_, service) = CreateServiceWith2DASaveData();
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 10)
            .WithClass(CommonClass.Rogue, 10)
            .Build();

        var saves = service.CalculateBaseSavingThrows(creature);

        Assert.Equal(10, saves.Fortitude);
        Assert.Equal(10, saves.Reflex);
        Assert.Equal(6, saves.Will);
    }

    [Fact]
    public void CalculateBaseSavingThrows_ThreeClass_SumsAll()
    {
        // Fighter 5 / Rogue 5 / Wizard 5
        // Fighter: Fort=4, Ref=1, Will=1
        // Rogue:   Fort=1, Ref=4, Will=1
        // Wizard:  Fort=1, Ref=1, Will=4
        // Total:   Fort=6, Ref=6, Will=6
        var (_, service) = CreateServiceWith2DASaveData();
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 5)
            .WithClass(CommonClass.Rogue, 5)
            .WithClass(CommonClass.Wizard, 5)
            .Build();

        var saves = service.CalculateBaseSavingThrows(creature);

        Assert.Equal(6, saves.Fortitude);
        Assert.Equal(6, saves.Reflex);
        Assert.Equal(6, saves.Will);
    }

    #endregion

    #region Saving Throws — Edge Cases

    [Fact]
    public void GetClassSaves_ZeroLevel_ReturnsAllZero()
    {
        var (_, service) = CreateServiceWith2DASaveData();
        var saves = service.GetClassSaves(4, 0);

        Assert.Equal(0, saves.Fortitude);
        Assert.Equal(0, saves.Reflex);
        Assert.Equal(0, saves.Will);
    }

    [Fact]
    public void CalculateBaseSavingThrows_EmptyClassList_ReturnsAllZero()
    {
        var (_, service) = CreateServiceWith2DASaveData();
        var creature = new CreatureBuilder().Build();

        var saves = service.CalculateBaseSavingThrows(creature);

        Assert.Equal(0, saves.Fortitude);
        Assert.Equal(0, saves.Reflex);
        Assert.Equal(0, saves.Will);
    }

    #endregion

    #region Equipment Attack Bonus

    [Fact]
    public void CalculateEquipmentAttackBonus_SingleEnhancementBonus_ReturnsIt()
    {
        var weapon = new UtiFile();
        weapon.Properties.Add(new ItemProperty
        {
            PropertyName = 6,   // Enhancement Bonus
            CostValue = 3       // +3
        });

        var (_, service) = CreateServiceWith2DABabData();
        var result = service.CalculateEquipmentAttackBonus(new[] { weapon });

        Assert.Equal(3, result);
    }

    [Fact]
    public void CalculateEquipmentAttackBonus_MultipleEnhancements_HighestWins()
    {
        // NWN rule: Enhancement bonuses don't stack — only highest applies
        var weapon1 = new UtiFile();
        weapon1.Properties.Add(new ItemProperty
        {
            PropertyName = 6,   // Enhancement Bonus
            CostValue = 5       // +5
        });

        var weapon2 = new UtiFile();
        weapon2.Properties.Add(new ItemProperty
        {
            PropertyName = 6,   // Enhancement Bonus
            CostValue = 2       // +2
        });

        var (_, service) = CreateServiceWith2DABabData();
        var result = service.CalculateEquipmentAttackBonus(new[] { weapon1, weapon2 });

        Assert.Equal(5, result); // Highest only, not 5+2
    }

    [Fact]
    public void CalculateEquipmentAttackBonus_AttackBonuses_Stack()
    {
        // NWN rule: Attack bonuses from different items stack additively
        var item1 = new UtiFile();
        item1.Properties.Add(new ItemProperty
        {
            PropertyName = 56,  // Attack Bonus
            CostValue = 2       // +2
        });

        var item2 = new UtiFile();
        item2.Properties.Add(new ItemProperty
        {
            PropertyName = 56,  // Attack Bonus
            CostValue = 3       // +3
        });

        var (_, service) = CreateServiceWith2DABabData();
        var result = service.CalculateEquipmentAttackBonus(new[] { item1, item2 });

        Assert.Equal(5, result); // 2 + 3 = 5
    }

    [Fact]
    public void CalculateEquipmentAttackBonus_EnhancementPlusAttackBonus_BothApply()
    {
        // Enhancement and Attack Bonus stack with each other
        var weapon = new UtiFile();
        weapon.Properties.Add(new ItemProperty
        {
            PropertyName = 6,   // Enhancement Bonus
            CostValue = 3       // +3
        });

        var gloves = new UtiFile();
        gloves.Properties.Add(new ItemProperty
        {
            PropertyName = 56,  // Attack Bonus
            CostValue = 2       // +2
        });

        var (_, service) = CreateServiceWith2DABabData();
        var result = service.CalculateEquipmentAttackBonus(new[] { weapon, gloves });

        Assert.Equal(5, result); // Enhancement 3 + Attack 2 = 5
    }

    [Fact]
    public void CalculateEquipmentAttackBonus_NullItems_Skipped()
    {
        var weapon = new UtiFile();
        weapon.Properties.Add(new ItemProperty
        {
            PropertyName = 6,
            CostValue = 2
        });

        var (_, service) = CreateServiceWith2DABabData();
        var result = service.CalculateEquipmentAttackBonus(new UtiFile?[] { null, weapon, null });

        Assert.Equal(2, result);
    }

    [Fact]
    public void CalculateEquipmentAttackBonus_NoRelevantProperties_ReturnsZero()
    {
        // Item with properties that aren't Enhancement or Attack Bonus
        var item = new UtiFile();
        item.Properties.Add(new ItemProperty
        {
            PropertyName = 1,   // Some other property (Ability Bonus)
            CostValue = 4
        });

        var (_, service) = CreateServiceWith2DABabData();
        var result = service.CalculateEquipmentAttackBonus(new[] { item });

        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculateEquipmentAttackBonus_EmptyList_ReturnsZero()
    {
        var (_, service) = CreateServiceWith2DABabData();
        var result = service.CalculateEquipmentAttackBonus(Array.Empty<UtiFile?>());

        Assert.Equal(0, result);
    }

    #endregion

    #region BuildAttackSequence

    [Theory]
    [InlineData(1, 1, "+1")]
    [InlineData(5, 1, "+5")]
    [InlineData(6, 2, "+6/+1")]
    [InlineData(10, 2, "+10/+5")]
    [InlineData(11, 3, "+11/+6/+1")]
    [InlineData(15, 3, "+15/+10/+5")]
    [InlineData(16, 4, "+16/+11/+6/+1")]
    [InlineData(20, 4, "+20/+15/+10/+5")]
    public void BuildAttackSequence_StandardProgression_CorrectFormat(int bab, int apr, string expected)
    {
        Assert.Equal(expected, CreatureDisplayService.BuildAttackSequence(bab, apr));
    }

    [Fact]
    public void BuildAttackSequence_SingleAttack_NoSlashes()
    {
        Assert.Equal("+3", CreatureDisplayService.BuildAttackSequence(3, 1));
    }

    [Fact]
    public void BuildAttackSequence_NegativeIteratives_ShowsNegativeValues()
    {
        // BAB +6 with 4 attacks (hypothetical): +6/+1/-4/-9
        Assert.Equal("+6/+1/-4/-9", CreatureDisplayService.BuildAttackSequence(6, 4));
    }

    [Fact]
    public void BuildAttackSequence_ZeroBab_ShowsPlusZero()
    {
        Assert.Equal("+0", CreatureDisplayService.BuildAttackSequence(0, 1));
    }

    #endregion

    #region CalculateCombatStats — Integrated

    [Fact]
    public void CalculateCombatStats_Fighter20_FullBreakdown()
    {
        var (_, service) = CreateServiceWith2DABabData();
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 20)
            .Build();

        var stats = service.CalculateCombatStats(creature);

        Assert.Equal(20, stats.BaseBab);
        Assert.Equal(0, stats.EquipmentBonus);
        Assert.Equal(20, stats.TotalBab);
        Assert.Equal(4, stats.AttacksPerRound);
        Assert.Equal("+20/+15/+10/+5", stats.AttackSequence);
    }

    [Fact]
    public void CalculateCombatStats_Wizard10_LowBab()
    {
        var (_, service) = CreateServiceWith2DABabData();
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Wizard, 10)
            .Build();

        var stats = service.CalculateCombatStats(creature);

        Assert.Equal(5, stats.BaseBab);
        Assert.Equal(0, stats.EquipmentBonus);
        Assert.Equal(5, stats.TotalBab);
        Assert.Equal(1, stats.AttacksPerRound);
        Assert.Equal("+5", stats.AttackSequence);
    }

    [Fact]
    public void CalculateCombatStats_WithEquipment_AddsToTotal()
    {
        var (_, service) = CreateServiceWith2DABabData();
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 10)
            .Build();

        var weapon = new UtiFile();
        weapon.Properties.Add(new ItemProperty
        {
            PropertyName = 6,   // Enhancement
            CostValue = 3       // +3
        });

        var stats = service.CalculateCombatStats(creature, new[] { weapon });

        Assert.Equal(10, stats.BaseBab);
        Assert.Equal(3, stats.EquipmentBonus);
        Assert.Equal(13, stats.TotalBab);
        Assert.Equal(3, stats.AttacksPerRound);
        Assert.Equal("+13/+8/+3", stats.AttackSequence);
    }

    [Fact]
    public void CalculateCombatStats_NullEquipment_NoBonus()
    {
        var (_, service) = CreateServiceWith2DABabData();
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 5)
            .Build();

        var stats = service.CalculateCombatStats(creature, null);

        Assert.Equal(5, stats.BaseBab);
        Assert.Equal(0, stats.EquipmentBonus);
        Assert.Equal(5, stats.TotalBab);
    }

    [Fact]
    public void CalculateCombatStats_Cleric20_ThreeAttacks()
    {
        // Cleric 20: BAB 15 -> 3 attacks (+15/+10/+5)
        var (_, service) = CreateServiceWith2DABabData();
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Cleric, 20)
            .Build();

        var stats = service.CalculateCombatStats(creature);

        Assert.Equal(15, stats.BaseBab);
        Assert.Equal(3, stats.AttacksPerRound);
        Assert.Equal("+15/+10/+5", stats.AttackSequence);
    }

    [Fact]
    public void CalculateCombatStats_FighterRogue_MulticlassBAB()
    {
        // Fighter 10 / Rogue 10: BAB 10 + 7 = 17 -> 4 attacks
        var (_, service) = CreateServiceWith2DABabData();
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 10)
            .WithClass(CommonClass.Rogue, 10)
            .Build();

        var stats = service.CalculateCombatStats(creature);

        Assert.Equal(17, stats.BaseBab);
        Assert.Equal(4, stats.AttacksPerRound);
        Assert.Equal("+17/+12/+7/+2", stats.AttackSequence);
    }

    #endregion

    #region CalculateCombatStats — Epic APR

    [Fact]
    public void CalculateCombatStats_EpicFighter_NoExtraAttacks()
    {
        // Fighter 30: BAB 30, but epic BAB doesn't add APR
        // Epic BAB = 1 + (30 - 21) / 2 = 5
        // Effective BAB for APR = 30 - 5 = 25 -> still 4 attacks
        var (_, service) = CreateServiceWith2DABabData();
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 30)
            .Build();

        var stats = service.CalculateCombatStats(creature);

        Assert.Equal(30, stats.BaseBab);
        Assert.Equal(4, stats.AttacksPerRound);
        // Attack sequence uses total BAB (not effective), starting from 30
        Assert.Equal("+30/+25/+20/+15", stats.AttackSequence);
    }

    [Fact]
    public void CalculateCombatStats_EpicWizard_ReducedAPR()
    {
        // Wizard 30: BAB = floor(30/2) = 15
        // Epic BAB = 1 + (30 - 21) / 2 = 5
        // Effective BAB for APR = 15 - 5 = 10 -> 2 attacks
        var (_, service) = CreateServiceWith2DABabData();
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Wizard, 30)
            .Build();

        var stats = service.CalculateCombatStats(creature);

        Assert.Equal(15, stats.BaseBab);
        Assert.Equal(2, stats.AttacksPerRound);
    }

    #endregion

    #region CalculateCombatStats — Edge Cases

    [Fact]
    public void CalculateCombatStats_EmptyCreature_SafeDefaults()
    {
        var (_, service) = CreateServiceWith2DABabData();
        var creature = new CreatureBuilder().Build();

        var stats = service.CalculateCombatStats(creature);

        Assert.Equal(0, stats.BaseBab);
        Assert.Equal(0, stats.EquipmentBonus);
        Assert.Equal(0, stats.TotalBab);
        Assert.Equal(1, stats.AttacksPerRound); // Minimum 1 attack
    }

    #endregion
}
