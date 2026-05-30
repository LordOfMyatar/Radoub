using System.Collections.Generic;
using Quartermaster.Services;
using Radoub.Formats.Utc;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for MASTERFEAT subtype applicability filtering (#2096).
///
/// In feat.2da, Skill Focus subtypes carry REQSKILL (the governed skill) and are
/// universal (ALLCLASSESCANUSE=1); Spell Focus subtypes carry MINSPELLLVL>0. The
/// subtype picker must drop subtypes the character can't meaningfully take:
///   - Skill Focus (X): only when skill X is a class skill for the character.
///   - Spell Focus (X): only when the character has a spellcasting class.
/// Strict mode additionally requires the subtype's own prereqs to be met.
/// </summary>
public class FeatServiceSubtypeApplicabilityTests
{
    private readonly MockGameDataService _mock;
    private readonly SkillService _skillService;
    private readonly FeatService _featService;

    // Skill IDs
    private const int SkillSpot = 9;        // Fighter class skill (set up below)
    private const int SkillSpellcraft = 19; // NOT a Fighter class skill

    // Feat IDs (Skill Focus subtypes)
    private const int FeatSkillFocusSpot = 173;
    private const int FeatSkillFocusSpellcraft = 174;

    // Feat IDs (Spell Focus subtypes)
    private const int FeatSpellFocusAbj = 35;
    private const int FeatSpellFocusCon = 166;

    // Class IDs
    private const int ClassFighter = 4;  // no SpellGainTable
    private const int ClassWizard = 10;  // has SpellGainTable

    public FeatServiceSubtypeApplicabilityTests()
    {
        _mock = new MockGameDataService(includeSampleData: false);
        SetupData();
        _skillService = new SkillService(_mock);
        _featService = new FeatService(_mock, _skillService, new FeatCacheService());
    }

    private void SetupData()
    {
        // --- Skill Focus subtypes: universal, REQSKILL set ---
        _mock.Set2DAValue("feat", FeatSkillFocusSpot, "LABEL", "SkillFocusSpot");
        _mock.Set2DAValue("feat", FeatSkillFocusSpot, "MASTERFEAT", "4");
        _mock.Set2DAValue("feat", FeatSkillFocusSpot, "ALLCLASSESCANUSE", "1");
        _mock.Set2DAValue("feat", FeatSkillFocusSpot, "REQSKILL", SkillSpot.ToString());

        _mock.Set2DAValue("feat", FeatSkillFocusSpellcraft, "LABEL", "SkillFocusSpellcraft");
        _mock.Set2DAValue("feat", FeatSkillFocusSpellcraft, "MASTERFEAT", "4");
        _mock.Set2DAValue("feat", FeatSkillFocusSpellcraft, "ALLCLASSESCANUSE", "1");
        _mock.Set2DAValue("feat", FeatSkillFocusSpellcraft, "REQSKILL", SkillSpellcraft.ToString());

        // --- Spell Focus subtypes: MINSPELLLVL=1 ---
        _mock.Set2DAValue("feat", FeatSpellFocusAbj, "LABEL", "SpellFocusAbj");
        _mock.Set2DAValue("feat", FeatSpellFocusAbj, "MASTERFEAT", "3");
        _mock.Set2DAValue("feat", FeatSpellFocusAbj, "ALLCLASSESCANUSE", "0");
        _mock.Set2DAValue("feat", FeatSpellFocusAbj, "MINSPELLLVL", "1");

        _mock.Set2DAValue("feat", FeatSpellFocusCon, "LABEL", "SpellFocusCon");
        _mock.Set2DAValue("feat", FeatSpellFocusCon, "MASTERFEAT", "3");
        _mock.Set2DAValue("feat", FeatSpellFocusCon, "ALLCLASSESCANUSE", "0");
        _mock.Set2DAValue("feat", FeatSpellFocusCon, "MINSPELLLVL", "1");

        // --- Fighter: has a skills table where Spot is a class skill, Spellcraft is not ---
        _mock.Set2DAValue("classes", ClassFighter, "SkillsTable", "cls_skill_fight");
        _mock.Set2DAValue("cls_skill_fight", 0, "SkillIndex", SkillSpot.ToString());
        _mock.Set2DAValue("cls_skill_fight", 0, "ClassSkill", "1");
        _mock.Set2DAValue("cls_skill_fight", 1, "SkillIndex", SkillSpellcraft.ToString());
        _mock.Set2DAValue("cls_skill_fight", 1, "ClassSkill", "0"); // cross-class only
        // Fighter has NO SpellGainTable (non-caster)

        // --- Wizard: caster (SpellGainTable set), Spellcraft is a class skill ---
        _mock.Set2DAValue("classes", ClassWizard, "SpellGainTable", "cls_spgn_wiz");
        // SpellGainTable is indexed by (classLevel - 1). Wizard() below is level 4 → row 3.
        // Populate rows 0..3 so the MINSPELLLVL caster prereq check finds a level-1 slot.
        for (int row = 0; row <= 3; row++)
            _mock.Set2DAValue("cls_spgn_wiz", row, "SpellLevel1", "1");
        _mock.Set2DAValue("classes", ClassWizard, "SkillsTable", "cls_skill_wiz");
        _mock.Set2DAValue("cls_skill_wiz", 0, "SkillIndex", SkillSpellcraft.ToString());
        _mock.Set2DAValue("cls_skill_wiz", 0, "ClassSkill", "1");
    }

    private static UtcFile Fighter() =>
        new() { ClassList = { new CreatureClass { Class = ClassFighter, ClassLevel = 4 } } };

    private static UtcFile Wizard() =>
        new() { ClassList = { new CreatureClass { Class = ClassWizard, ClassLevel = 4 } } };

    // --- Skill Focus structural filtering ---

    [Fact]
    public void SkillFocus_ClassSkill_IsApplicableToFighter()
    {
        Assert.True(_featService.IsSubtypeStructurallyApplicable(Fighter(), FeatSkillFocusSpot));
    }

    [Fact]
    public void SkillFocus_NonClassSkill_IsNotApplicableToFighter()
    {
        // Spellcraft is not a Fighter class skill — should be filtered out.
        Assert.False(_featService.IsSubtypeStructurallyApplicable(Fighter(), FeatSkillFocusSpellcraft));
    }

    [Fact]
    public void SkillFocus_ClassSkillForWizard_IsApplicable()
    {
        Assert.True(_featService.IsSubtypeStructurallyApplicable(Wizard(), FeatSkillFocusSpellcraft));
    }

    // --- Spell Focus structural filtering ---

    [Fact]
    public void SpellFocus_NonCaster_IsNotApplicable()
    {
        Assert.False(_featService.IsSubtypeStructurallyApplicable(Fighter(), FeatSpellFocusAbj));
    }

    [Fact]
    public void SpellFocus_Caster_IsApplicable()
    {
        Assert.True(_featService.IsSubtypeStructurallyApplicable(Wizard(), FeatSpellFocusAbj));
    }

    // --- HasCasterClass helper ---

    [Fact]
    public void HasCasterClass_Fighter_False()
    {
        Assert.False(_featService.HasCasterClass(Fighter()));
    }

    [Fact]
    public void HasCasterClass_Wizard_True()
    {
        Assert.True(_featService.HasCasterClass(Wizard()));
    }

    // --- Combined IsSubtypeApplicable (validation-level aware) ---

    [Fact]
    public void IsSubtypeApplicable_CeMode_UsesStructuralOnly()
    {
        // CE mode: structural filter still drops a non-class-skill Skill Focus.
        Assert.False(_featService.IsSubtypeApplicable(
            Fighter(), FeatSkillFocusSpellcraft, ValidationLevel.None,
            new HashSet<ushort>(), _ => 0, _ => ""));

        // ...but keeps a valid one.
        Assert.True(_featService.IsSubtypeApplicable(
            Fighter(), FeatSkillFocusSpot, ValidationLevel.None,
            new HashSet<ushort>(), _ => 0, _ => ""));
    }

    [Fact]
    public void IsSubtypeApplicable_StrictMode_AppliesStructuralAndPrereqs()
    {
        // Spell Focus on a non-caster is barred structurally even in Strict.
        Assert.False(_featService.IsSubtypeApplicable(
            Fighter(), FeatSpellFocusAbj, ValidationLevel.Strict,
            new HashSet<ushort>(), _ => 0, _ => ""));

        // Spell Focus on a caster passes (caster meets MINSPELLLVL prereq).
        Assert.True(_featService.IsSubtypeApplicable(
            Wizard(), FeatSpellFocusAbj, ValidationLevel.Strict,
            new HashSet<ushort>(), _ => 0, _ => ""));
    }
}
