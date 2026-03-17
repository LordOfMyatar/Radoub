using Quartermaster.Services;
using Radoub.TestUtilities.Builders;
using Xunit;

namespace Quartermaster.Tests;

public partial class FeatServiceAdvancedTests
{
    #region OR-Required Feat Chains

    [Fact]
    public void CheckPrereqs_OrRequired_HasBoth_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(10, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var feats = new HashSet<ushort> { 0, 11 }; // Has both Alertness AND Dodge

        var result = _featService.CheckFeatPrerequisites(
            creature, 40, feats, CalculateBab, GetClassName);

        Assert.True(result.AllMet);
        Assert.Equal(2, result.OrRequiredFeatsMet.Count);
        Assert.All(result.OrRequiredFeatsMet, item => Assert.True(item.Met));
    }

    [Fact]
    public void CheckPrereqs_OrRequired_5Options_HasMiddleOne_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(10, 10, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var feats = new HashSet<ushort> { 7 }; // Only has Cleave (OrReqFeat2)

        var result = _featService.CheckFeatPrerequisites(
            creature, 41, feats, CalculateBab, GetClassName);

        Assert.True(result.AllMet);
        Assert.Equal(5, result.OrRequiredFeatsMet.Count);
    }

    [Fact]
    public void CheckPrereqs_OrRequired_5Options_HasNone_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(10, 10, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var feats = new HashSet<ushort>(); // Has none of the 5

        var result = _featService.CheckFeatPrerequisites(
            creature, 41, feats, CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        Assert.All(result.OrRequiredFeatsMet, item => Assert.False(item.Met));
    }

    [Fact]
    public void CheckPrereqs_MixedAndOr_HasAndButNotOr_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var feats = new HashSet<ushort> { 10 }; // Has Power Attack but not Dodge or Alertness

        var result = _featService.CheckFeatPrerequisites(
            creature, 42, feats, CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        // AND requirement met
        Assert.Single(result.RequiredFeatsMet);
        Assert.True(result.RequiredFeatsMet[0].Met);
        // OR requirement not met
        Assert.All(result.OrRequiredFeatsMet, item => Assert.False(item.Met));
    }

    [Fact]
    public void CheckPrereqs_MixedAndOr_HasOrButNotAnd_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var feats = new HashSet<ushort> { 0 }; // Has Alertness (OR) but not Power Attack (AND)

        var result = _featService.CheckFeatPrerequisites(
            creature, 42, feats, CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        Assert.Single(result.RequiredFeatsMet);
        Assert.False(result.RequiredFeatsMet[0].Met); // Power Attack missing
    }

    [Fact]
    public void CheckPrereqs_MixedAndOr_HasBothAndAndOr_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var feats = new HashSet<ushort> { 10, 11 }; // Power Attack + Dodge

        var result = _featService.CheckFeatPrerequisites(
            creature, 42, feats, CalculateBab, GetClassName);

        Assert.True(result.AllMet);
    }

    [Fact]
    public void CheckPrereqs_TwoAndPrereqs_HasBoth_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var feats = new HashSet<ushort> { 10, 11 }; // Power Attack + Dodge

        var result = _featService.CheckFeatPrerequisites(
            creature, 43, feats, CalculateBab, GetClassName);

        Assert.True(result.AllMet);
        Assert.Equal(2, result.RequiredFeatsMet.Count);
        Assert.All(result.RequiredFeatsMet, item => Assert.True(item.Met));
    }

    [Fact]
    public void CheckPrereqs_TwoAndPrereqs_HasOnlyFirst_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var feats = new HashSet<ushort> { 10 }; // Only Power Attack, missing Dodge

        var result = _featService.CheckFeatPrerequisites(
            creature, 43, feats, CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        Assert.True(result.RequiredFeatsMet[0].Met);   // Power Attack: met
        Assert.False(result.RequiredFeatsMet[1].Met);   // Dodge: not met
    }

    [Fact]
    public void GetFeatPrerequisites_OrRequired_ParsesAll5()
    {
        var prereqs = _featService.GetFeatPrerequisites(41);
        Assert.Equal(5, prereqs.OrRequiredFeats.Count);
        Assert.Contains(0, prereqs.OrRequiredFeats);
        Assert.Contains(5, prereqs.OrRequiredFeats);
        Assert.Contains(7, prereqs.OrRequiredFeats);
        Assert.Contains(10, prereqs.OrRequiredFeats);
        Assert.Contains(11, prereqs.OrRequiredFeats);
    }

    [Fact]
    public void GetFeatPrerequisites_MixedAndOr_ParsesBothTypes()
    {
        var prereqs = _featService.GetFeatPrerequisites(42);
        Assert.Single(prereqs.RequiredFeats);
        Assert.Contains(10, prereqs.RequiredFeats);
        Assert.Equal(2, prereqs.OrRequiredFeats.Count);
        Assert.Contains(11, prereqs.OrRequiredFeats);
        Assert.Contains(0, prereqs.OrRequiredFeats);
    }

    #endregion

    #region Epic Feat Validation

    [Fact]
    public void CheckPrereqs_EpicFeat_Level21_MeetsBab_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10)
            .WithClass(4, 21) // Fighter 21
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 50, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.True(result.AllMet);
    }

    [Fact]
    public void CheckPrereqs_EpicFeat_Level20_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10)
            .WithClass(4, 20) // Fighter 20 — not epic
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 50, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        Assert.Contains(result.OtherRequirements, r => r.Description == "Epic (level 21+)" && !r.Met!.Value);
    }

    [Fact]
    public void CheckPrereqs_EpicFeat_MulticlassLevel21_AllMet()
    {
        // Fighter 15 + Wizard 6 = total 21 (epic)
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10)
            .WithClass(4, 15) // Fighter
            .WithClass(10, 6)  // Wizard
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 50, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.True(result.AllMet);
    }

    [Fact]
    public void CheckPrereqs_EpicWithAbilityAndFeat_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(26, 10, 10, 10, 10, 10) // STR 26 >= 25
            .WithClass(4, 21)
            .Build();

        var feats = new HashSet<ushort> { 10 }; // Has Power Attack

        var result = _featService.CheckFeatPrerequisites(
            creature, 51, feats, CalculateBab, GetClassName);

        Assert.True(result.AllMet);
    }

    [Fact]
    public void CheckPrereqs_EpicWithAbilityAndFeat_LowStr_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(20, 10, 10, 10, 10, 10) // STR 20 < 25
            .WithClass(4, 21)
            .Build();

        var feats = new HashSet<ushort> { 10 }; // Has Power Attack

        var result = _featService.CheckFeatPrerequisites(
            creature, 51, feats, CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        Assert.Contains(result.AbilityRequirements, r => r.Description == "STR 25+" && !r.Met);
    }

    [Fact]
    public void CheckPrereqs_EpicFeat_MetsBabButNotLevel_NotMet()
    {
        // High BAB from some source, but level too low
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10)
            .WithClass(4, 10) // Fighter 10, BAB=10 (>5) but not epic
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 50, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        // BAB met, epic not met
        Assert.Contains(result.OtherRequirements, r => r.Description == "BAB 5+" && r.Met!.Value);
        Assert.Contains(result.OtherRequirements, r => r.Description == "Epic (level 21+)" && !r.Met!.Value);
    }

    [Fact]
    public void GetFeatPrerequisites_NonEpic_RequiresEpicFalse()
    {
        var prereqs = _featService.GetFeatPrerequisites(52);
        Assert.False(prereqs.RequiresEpic);
    }

    #endregion

    #region Spell Level Prerequisites

    [Fact]
    public void CheckPrereqs_SpellLevel_WizardLevel5_CanCastLevel3_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(10, 10, 10, 16, 10, 10) // INT 16 for Wizard
            .WithClass(10, 5) // Wizard 5
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 60, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.True(result.AllMet);
        Assert.Contains(result.OtherRequirements, r => r.Description == "Cast level 3 spells" && r.Met!.Value);
    }

    [Fact]
    public void CheckPrereqs_SpellLevel_WizardLevel3_CannotCastLevel3_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(10, 10, 10, 16, 10, 10)
            .WithClass(10, 3) // Wizard 3: has level 2 but not level 3
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 60, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        Assert.Contains(result.OtherRequirements, r => r.Description == "Cast level 3 spells" && !r.Met!.Value);
    }

    [Fact]
    public void CheckPrereqs_SpellLevel_FighterOnly_NoCasting_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10)
            .WithClass(4, 10) // Fighter — no spell gain table
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 60, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        Assert.Contains(result.OtherRequirements, r => r.Description == "Cast level 3 spells" && !r.Met!.Value);
    }

    [Fact]
    public void CheckPrereqs_SpellLevel1_WizardLevel1_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(10, 10, 10, 16, 10, 10)
            .WithClass(10, 1) // Wizard 1: has level 1 spells
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 61, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.True(result.AllMet);
    }

    [Fact]
    public void CheckPrereqs_SpellLevel_MulticlassFighterWizard_WizardGrantsSpells_AllMet()
    {
        // Fighter/Wizard multiclass — Wizard portion grants spell access
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 16, 10, 10)
            .WithClass(4, 5)  // Fighter 5 — no spells
            .WithClass(10, 5) // Wizard 5 — level 3 spells
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 60, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.True(result.AllMet);
    }

    [Fact]
    public void GetFeatPrerequisites_SpellLevel_ParsesCorrectly()
    {
        var prereqs = _featService.GetFeatPrerequisites(60);
        Assert.Equal(3, prereqs.MinSpellLevel);
    }

    #endregion

    #region Dual Skill Requirements

    [Fact]
    public void GetFeatPrerequisites_DualSkill_ParsesBothSkills()
    {
        var prereqs = _featService.GetFeatPrerequisites(70);
        Assert.Equal(2, prereqs.RequiredSkills.Count);
        Assert.Contains(prereqs.RequiredSkills, s => s.SkillId == 3 && s.MinRanks == 5);  // Discipline 5
        Assert.Contains(prereqs.RequiredSkills, s => s.SkillId == 16 && s.MinRanks == 3); // Tumble 3
    }

    [Fact]
    public void CheckPrereqs_DualSkill_BothMet_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 10)
            .Build();
        // Pad skill list to index 16
        while (creature.SkillList.Count <= 16)
            creature.SkillList.Add(0);
        creature.SkillList[3] = 6;   // Discipline: 6 >= 5
        creature.SkillList[16] = 4;  // Tumble: 4 >= 3

        var result = _featService.CheckFeatPrerequisites(
            creature, 70, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.True(result.AllMet);
        Assert.Equal(2, result.SkillRequirements.Count);
        Assert.All(result.SkillRequirements, s => Assert.True(s.Met));
    }

    [Fact]
    public void CheckPrereqs_DualSkill_OneMet_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 10)
            .Build();
        while (creature.SkillList.Count <= 16)
            creature.SkillList.Add(0);
        creature.SkillList[3] = 6;   // Discipline: 6 >= 5 (met)
        creature.SkillList[16] = 1;  // Tumble: 1 < 3 (not met)

        var result = _featService.CheckFeatPrerequisites(
            creature, 70, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        Assert.Single(result.SkillRequirements, s => s.Met);
        Assert.Single(result.SkillRequirements, s => !s.Met);
    }

    [Fact]
    public void CheckPrereqs_DualSkill_NeitherMet_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 10)
            .Build();
        while (creature.SkillList.Count <= 16)
            creature.SkillList.Add(0);
        creature.SkillList[3] = 2;   // Discipline: 2 < 5
        creature.SkillList[16] = 1;  // Tumble: 1 < 3

        var result = _featService.CheckFeatPrerequisites(
            creature, 70, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        Assert.All(result.SkillRequirements, s => Assert.False(s.Met));
    }

    [Fact]
    public void CheckPrereqs_SkillBeyondSkillList_TreatedAsZero()
    {
        // Creature has no skills at all — skill index beyond list count
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 10)
            .Build();
        // Don't add any skills — SkillList is empty

        var result = _featService.CheckFeatPrerequisites(
            creature, 70, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        Assert.Equal(2, result.SkillRequirements.Count);
        Assert.All(result.SkillRequirements, s => Assert.False(s.Met));
    }

    #endregion
}
