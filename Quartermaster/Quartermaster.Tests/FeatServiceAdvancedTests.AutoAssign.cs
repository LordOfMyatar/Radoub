using Quartermaster.Services;
using Radoub.TestUtilities.Builders;
using Xunit;

namespace Quartermaster.Tests;

public partial class FeatServiceAdvancedTests
{
    #region AutoAssignFeats

    [Fact]
    public void AutoAssignFeats_NoPackage_AssignsAlphabetically()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var currentFeats = new HashSet<int>();

        var assigned = _featService.AutoAssignFeats(
            creature, 4, 255, // No package
            currentFeats, 2,
            null, // No bonus pool restriction
            featId => true); // All prereqs met

        Assert.Equal(2, assigned.Count);
        // Should be alphabetical — Alertness before Blind-Fight before Cleave etc.
        var names = assigned.Select(id => _featService.GetFeatName(id)).ToList();
        Assert.Equal(names.OrderBy(n => n).ToList(), names);
    }

    [Fact]
    public void AutoAssignFeats_WithPackage_PrefersPackageFeats()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var currentFeats = new HashSet<int>();

        var assigned = _featService.AutoAssignFeats(
            creature, 4, 1, // Package 1 prefers: Power Attack, Cleave, Dodge
            currentFeats, 2,
            null,
            featId => true);

        Assert.Equal(2, assigned.Count);
        // Should pick Power Attack first (from package), then Cleave
        Assert.Equal(10, assigned[0]); // Power Attack
        Assert.Equal(7, assigned[1]);  // Cleave
    }

    [Fact]
    public void AutoAssignFeats_SkipsAlreadyOwnedFeats()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var currentFeats = new HashSet<int> { 10 }; // Already has Power Attack

        var assigned = _featService.AutoAssignFeats(
            creature, 4, 1, // Package prefers: Power Attack, Cleave, Dodge
            currentFeats, 2,
            null,
            featId => true);

        Assert.Equal(2, assigned.Count);
        Assert.DoesNotContain(10, assigned); // Skipped Power Attack
        Assert.Equal(7, assigned[0]);   // Cleave (next preferred)
        Assert.Equal(11, assigned[1]);  // Dodge (next preferred)
    }

    [Fact]
    public void AutoAssignFeats_RespectsMaxCount()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var assigned = _featService.AutoAssignFeats(
            creature, 4, 255, new HashSet<int>(), 1, null, _ => true);

        Assert.Single(assigned);
    }

    [Fact]
    public void AutoAssignFeats_RespectsPrereqChecker()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        // Only feat 0 (Alertness) passes prereqs
        var assigned = _featService.AutoAssignFeats(
            creature, 4, 255, new HashSet<int>(), 3,
            null,
            featId => featId == 0);

        Assert.Single(assigned);
        Assert.Equal(0, assigned[0]); // Only Alertness
    }

    [Fact]
    public void AutoAssignFeats_BonusFeatPool_RestrictsToPool()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var bonusPool = new HashSet<int> { 5, 10 }; // Only Blind-Fight and Power Attack

        var assigned = _featService.AutoAssignFeats(
            creature, 4, 255, new HashSet<int>(), 5,
            bonusPool,
            _ => true);

        // Can only pick from pool, even though more feats exist
        Assert.True(assigned.Count <= 2);
        Assert.All(assigned, id => Assert.Contains(id, bonusPool));
    }

    [Fact]
    public void AutoAssignFeats_BonusFeatPool_PackagePrefNotInPool_Skipped()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        // Pool excludes Power Attack (10) which is package's first preference
        var bonusPool = new HashSet<int> { 5, 7, 11 }; // Blind-Fight, Cleave, Dodge

        var assigned = _featService.AutoAssignFeats(
            creature, 4, 1, // Package prefers: Power Attack, Cleave, Dodge
            new HashSet<int>(), 2,
            bonusPool,
            _ => true);

        Assert.Equal(2, assigned.Count);
        Assert.DoesNotContain(10, assigned); // Power Attack not in pool
        Assert.Equal(7, assigned[0]);   // Cleave (next pref in pool)
        Assert.Equal(11, assigned[1]);  // Dodge (next pref in pool)
    }

    [Fact]
    public void AutoAssignFeats_ZeroMaxCount_ReturnsEmpty()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var assigned = _featService.AutoAssignFeats(
            creature, 4, 255, new HashSet<int>(), 0, null, _ => true);

        Assert.Empty(assigned);
    }

    [Fact]
    public void AutoAssignFeats_NoDuplicatesInResults()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var assigned = _featService.AutoAssignFeats(
            creature, 4, 1, new HashSet<int>(), 10, null, _ => true);

        // All assigned feat IDs should be unique
        Assert.Equal(assigned.Count, assigned.Distinct().Count());
    }

    [Fact]
    public void AutoAssignFeats_Fallback_PrefersClassFeatsOverUniversal()
    {
        // Set up: universal feat "AAA_Universal" (alphabetically first)
        // Existing class feats in Fighter table should be preferred over universals
        _mockGameData.Set2DAValue("feat", 80, "LABEL", "AAA_Universal");
        _mockGameData.Set2DAValue("feat", 80, "FEAT", "480");
        _mockGameData.Set2DAValue("feat", 80, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 80, "TOOLSCATEGORIES", "6");
        _mockGameData.SetTlkString(480, "AAA Universal Feat");

        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 1) // Fighter
            .Build();

        // No package (255) — forces fallback
        var assigned = _featService.AutoAssignFeats(
            creature, 4, 255, new HashSet<int>(), 1, null, _ => true);

        // Should prefer a class feat (e.g., Blind-Fight/5) over AAA Universal (80)
        Assert.Single(assigned);
        Assert.NotEqual(80, assigned[0]); // NOT the alphabetically-first universal feat
    }

    #endregion

    #region Multiclass Feat Scenarios

    [Fact]
    public void IsFeatAvailable_InSecondClassTable_Available()
    {
        // Rogue table has Dodge (11), Fighter does not have it as universal
        var creature = new CreatureBuilder()
            .WithClass(4, 5)  // Fighter
            .WithClass(8, 3)  // Rogue (has Dodge in feat table)
            .Build();

        Assert.True(_featService.IsFeatAvailable(creature, 11)); // Dodge in Rogue table
    }

    [Fact]
    public void IsFeatAvailable_NotInAnyClassTable_NotUniversal_Unavailable()
    {
        // Create a non-universal feat not in any class table
        _mockGameData.Set2DAValue("feat", 95, "LABEL", "ClassExclusive");
        _mockGameData.Set2DAValue("feat", 95, "FEAT", "495");
        _mockGameData.Set2DAValue("feat", 95, "ALLCLASSESCANUSE", "0");
        _mockGameData.SetTlkString(495, "Class Exclusive Feat");

        var creature = new CreatureBuilder()
            .WithClass(4, 5)  // Fighter
            .WithClass(8, 3)  // Rogue
            .Build();

        Assert.False(_featService.IsFeatAvailable(creature, 95));
    }

    [Fact]
    public void GetUnavailableFeatIds_Multiclass_UnionOfBothTables()
    {
        var creature = new CreatureBuilder()
            .WithClass(4, 5)  // Fighter
            .WithClass(8, 3)  // Rogue
            .Build();

        // Blind-Fight (5) is in Fighter table, Dodge (11) is in Rogue table
        // Both should be available
        var unavailable = _featService.GetUnavailableFeatIds(creature, new[] { 5, 11 });
        Assert.Empty(unavailable);
    }

    [Fact]
    public void GetCombinedGrantedFeatIds_Multiclass_UnionOfAllClasses()
    {
        // Set up Rogue with an auto-granted feat
        _mockGameData.Set2DAValue("cls_feat_rog", 1, "FeatIndex", "77");
        _mockGameData.Set2DAValue("cls_feat_rog", 1, "List", "3");
        _mockGameData.Set2DAValue("cls_feat_rog", 1, "GrantedOnLevel", "1");

        var creature = new CreatureBuilder()
            .WithRace(6)      // Human
            .WithClass(4, 5)  // Fighter
            .WithClass(8, 3)  // Rogue
            .Build();

        var granted = _featService.GetCombinedGrantedFeatIds(creature);

        // Fighter grants: feat 2 (Armor Prof Heavy), feat 48 (Weapon Spec)
        Assert.Contains(2, granted);
        // Rogue grants: feat 77
        Assert.Contains(77, granted);
        // Human race grants: feat 99
        Assert.Contains(99, granted);
    }

    [Fact]
    public void GetFeatGrantingClass_MulticlassOverlap_ReturnsFirstMatch()
    {
        var creature = new CreatureBuilder()
            .WithClass(4, 5)  // Fighter (has feat 2 as List=3)
            .WithClass(8, 3)  // Rogue
            .Build();

        // Feat 2 (Armor Prof Heavy) is granted by Fighter
        Assert.Equal(4, _featService.GetFeatGrantingClass(creature, 2));
    }

    #endregion

    #region HasPrerequisites Flag

    [Fact]
    public void CheckPrereqs_NoPrereqs_HasPrerequisitesFalse()
    {
        var creature = new CreatureBuilder()
            .WithClass(4, 1)
            .Build();

        // Feat 52: no prerequisites at all
        var result = _featService.CheckFeatPrerequisites(
            creature, 52, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.True(result.AllMet);
        Assert.False(result.HasPrerequisites);
    }

    [Fact]
    public void CheckPrereqs_WithPrereqs_HasPrerequisitesTrue()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        // Feat 10: requires STR 13
        var result = _featService.CheckFeatPrerequisites(
            creature, 10, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.True(result.HasPrerequisites);
    }

    [Fact]
    public void CheckPrereqs_ComplexPrereqs_HasPrerequisitesTrue()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(26, 10, 10, 10, 10, 10)
            .WithClass(4, 21)
            .Build();

        // Feat 51: epic + STR 25 + feat requirement — complex combination
        var result = _featService.CheckFeatPrerequisites(
            creature, 51, new HashSet<ushort> { 10 },
            CalculateBab, GetClassName);

        Assert.True(result.HasPrerequisites);
    }

    #endregion

    #region Expected Feat Count - Multiclass

    [Fact]
    public void GetExpectedFeatCount_MulticlassFighterWizard_SumsClassBonuses()
    {
        var creature = new CreatureBuilder()
            .WithRace(6) // Human
            .WithClass(4, 4)  // Fighter 4: bonus at levels 1, 2, 4 = 3
            .WithClass(10, 5) // Wizard 5: bonus at level 5 = 1
            .Build();

        var result = _featService.GetExpectedFeatCount(creature);
        Assert.Equal(4, result.BaseFeats); // 1 + floor(9/3) = 4
        Assert.Equal(1, result.RacialBonusFeats); // Human
        Assert.Equal(4, result.ClassBonusFeats); // Fighter 3 + Wizard 1
        Assert.Equal(9, result.TotalExpected);
    }

    [Fact]
    public void GetLevelUpFeatCount_MulticlassLevel6_GeneralFeatGranted()
    {
        // Fighter 3 + Rogue 2 = total 5, leveling Rogue to 3 = total 6
        var creature = new CreatureBuilder()
            .WithRace(6)
            .WithClass(4, 3)
            .WithClass(8, 2) // Rogue 2
            .Build();

        var result = _featService.GetLevelUpFeatCount(creature, 8, 3); // Rogue 3
        Assert.Equal(1, result.GeneralFeats); // Total level 6 = general feat
        Assert.Equal(0, result.RacialBonusFeats); // Not level 1
        Assert.Equal(0, result.ClassBonusFeats); // Rogue has no bonus at level 3
        Assert.Equal(1, result.TotalFeats);
    }

    #endregion

    #region Tooltip Formatting

    [Fact]
    public void FeatPrereqResult_GetTooltip_OrRequired_ShowsOneOfSection()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(10, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var feats = new HashSet<ushort> { 0 }; // Has Alertness

        var result = _featService.CheckFeatPrerequisites(
            creature, 40, feats, CalculateBab, GetClassName);

        var tooltip = result.GetTooltip();
        Assert.Contains("One of:", tooltip);
        Assert.Contains("Alertness", tooltip);
        Assert.Contains("Dodge", tooltip);
    }

    [Fact]
    public void FeatPrereqResult_GetTooltip_MultipleReqTypes_IncludesAll()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(26, 10, 10, 10, 10, 10)
            .WithClass(4, 21)
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 51, new HashSet<ushort> { 10 },
            CalculateBab, GetClassName);

        var tooltip = result.GetTooltip();
        Assert.Contains("Prerequisites:", tooltip);
        Assert.Contains("Power Attack", tooltip); // Required feat
        Assert.Contains("STR 25+", tooltip);      // Ability requirement
        Assert.Contains("Epic", tooltip);           // Epic requirement
    }

    #endregion
}
