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
/// subtype picker drops subtypes the character can't meaningfully take:
///   - Skill Focus (X): only when skill X is AVAILABLE to the character — i.e. it
///     appears in any of their class skill tables (class OR cross-class), or is
///     universal. Skill Focus is allowed for cross-class skills, so only skills no
///     class can train at all (e.g. Use Magic Device for a Fighter) are barred.
///     Multiclass widens availability (#2096 multiclass regression).
///   - Spell Focus (X): only when the character has a spellcasting class.
/// Strict mode additionally requires the subtype's own prereqs to be met.
/// </summary>
public class FeatServiceSubtypeApplicabilityTests
{
    private readonly MockGameDataService _mock;
    private readonly SkillService _skillService;
    private readonly FeatService _featService;

    // Skill IDs
    private const int SkillSpot = 9;          // Fighter class skill
    private const int SkillSpellcraft = 19;   // Fighter cross-class, Sorcerer class skill
    private const int SkillUseMagicDevice = 27; // In NO class table set up here → barred for Fighter

    // Feat IDs (Skill Focus subtypes)
    private const int FeatSkillFocusSpot = 173;
    private const int FeatSkillFocusSpellcraft = 174;
    private const int FeatSkillFocusUmd = 175;

    // Feat IDs (Spell Focus subtypes)
    private const int FeatSpellFocusAbj = 35;

    // Class IDs
    private const int ClassFighter = 4;   // no SpellGainTable (non-caster)
    private const int ClassWizard = 10;   // caster
    private const int ClassSorcerer = 11; // caster; Spellcraft is a class skill

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

        _mock.Set2DAValue("feat", FeatSkillFocusUmd, "LABEL", "SkillFocusUMD");
        _mock.Set2DAValue("feat", FeatSkillFocusUmd, "MASTERFEAT", "4");
        _mock.Set2DAValue("feat", FeatSkillFocusUmd, "ALLCLASSESCANUSE", "1");
        _mock.Set2DAValue("feat", FeatSkillFocusUmd, "REQSKILL", SkillUseMagicDevice.ToString());

        // --- Spell Focus subtype: MINSPELLLVL=1 ---
        _mock.Set2DAValue("feat", FeatSpellFocusAbj, "LABEL", "SpellFocusAbj");
        _mock.Set2DAValue("feat", FeatSpellFocusAbj, "MASTERFEAT", "3");
        _mock.Set2DAValue("feat", FeatSpellFocusAbj, "ALLCLASSESCANUSE", "0");
        _mock.Set2DAValue("feat", FeatSpellFocusAbj, "MINSPELLLVL", "1");

        // --- Fighter skills table: Spot (class), Spellcraft (cross-class). UMD absent. ---
        _mock.Set2DAValue("classes", ClassFighter, "SkillsTable", "cls_skill_fight");
        _mock.Set2DAValue("cls_skill_fight", 0, "SkillIndex", SkillSpot.ToString());
        _mock.Set2DAValue("cls_skill_fight", 0, "ClassSkill", "1");
        _mock.Set2DAValue("cls_skill_fight", 1, "SkillIndex", SkillSpellcraft.ToString());
        _mock.Set2DAValue("cls_skill_fight", 1, "ClassSkill", "0"); // cross-class — still available
        // No UMD row → UMD unavailable to Fighter.

        // --- Wizard: caster, Spellcraft class skill ---
        _mock.Set2DAValue("classes", ClassWizard, "SpellGainTable", "cls_spgn_wiz");
        for (int row = 0; row <= 3; row++)
            _mock.Set2DAValue("cls_spgn_wiz", row, "SpellLevel1", "1");
        _mock.Set2DAValue("classes", ClassWizard, "SkillsTable", "cls_skill_wiz");
        _mock.Set2DAValue("cls_skill_wiz", 0, "SkillIndex", SkillSpellcraft.ToString());
        _mock.Set2DAValue("cls_skill_wiz", 0, "ClassSkill", "1");

        // --- Sorcerer: caster, Spellcraft class skill (for multiclass Fighter+Sorc) ---
        _mock.Set2DAValue("classes", ClassSorcerer, "SpellGainTable", "cls_spgn_sorc");
        for (int row = 0; row <= 3; row++)
            _mock.Set2DAValue("cls_spgn_sorc", row, "SpellLevel1", "1");
        _mock.Set2DAValue("classes", ClassSorcerer, "SkillsTable", "cls_skill_sorc");
        _mock.Set2DAValue("cls_skill_sorc", 0, "SkillIndex", SkillSpellcraft.ToString());
        _mock.Set2DAValue("cls_skill_sorc", 0, "ClassSkill", "1");
    }

    private static UtcFile Fighter() =>
        new() { ClassList = { new CreatureClass { Class = ClassFighter, ClassLevel = 4 } } };

    private static UtcFile Wizard() =>
        new() { ClassList = { new CreatureClass { Class = ClassWizard, ClassLevel = 4 } } };

    private static UtcFile FighterSorcerer() =>
        new()
        {
            ClassList =
            {
                new CreatureClass { Class = ClassFighter, ClassLevel = 4 },
                new CreatureClass { Class = ClassSorcerer, ClassLevel = 1 }
            }
        };

    // --- Skill Focus structural filtering ---

    [Fact]
    public void SkillFocus_ClassSkill_IsApplicableToFighter()
    {
        Assert.True(_featService.IsSubtypeStructurallyApplicable(Fighter(), FeatSkillFocusSpot));
    }

    [Fact]
    public void SkillFocus_CrossClassSkill_IsApplicableToFighter()
    {
        // Spellcraft is cross-class for Fighter (ClassSkill=0) but still trainable → allowed.
        Assert.True(_featService.IsSubtypeStructurallyApplicable(Fighter(), FeatSkillFocusSpellcraft));
    }

    [Fact]
    public void SkillFocus_SkillNoClassCanTrain_IsNotApplicableToFighter()
    {
        // Use Magic Device is in no Fighter skill table → barred.
        Assert.False(_featService.IsSubtypeStructurallyApplicable(Fighter(), FeatSkillFocusUmd));
    }

    [Fact]
    public void SkillFocus_ClassSkillForWizard_IsApplicable()
    {
        Assert.True(_featService.IsSubtypeStructurallyApplicable(Wizard(), FeatSkillFocusSpellcraft));
    }

    // --- Multiclass (#2096 regression: Fighter+Sorc must see Sorc-trainable subtypes) ---

    [Fact]
    public void SkillFocus_MulticlassWidensAvailability()
    {
        // Spellcraft is a Sorcerer class skill; a Fighter/Sorcerer must see Skill Focus (Spellcraft).
        Assert.True(_featService.IsSubtypeStructurallyApplicable(FighterSorcerer(), FeatSkillFocusSpellcraft));
    }

    [Fact]
    public void SpellFocus_MulticlassWithCaster_IsApplicable()
    {
        // Fighter alone can't take Spell Focus, but Fighter/Sorcerer can (Sorc is a caster).
        Assert.True(_featService.IsSubtypeStructurallyApplicable(FighterSorcerer(), FeatSpellFocusAbj));
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

    [Fact]
    public void HasCasterClass_FighterSorcerer_True()
    {
        Assert.True(_featService.HasCasterClass(FighterSorcerer()));
    }

    // --- Combined IsSubtypeApplicable (validation-level aware) ---

    [Fact]
    public void IsSubtypeApplicable_CeMode_UsesStructuralOnly()
    {
        // CE mode: structural filter still drops a skill no class can train.
        Assert.False(_featService.IsSubtypeApplicable(
            Fighter(), FeatSkillFocusUmd, ValidationLevel.None,
            new HashSet<ushort>(), _ => 0, _ => ""));

        // ...but keeps an available one (cross-class is fine).
        Assert.True(_featService.IsSubtypeApplicable(
            Fighter(), FeatSkillFocusSpellcraft, ValidationLevel.None,
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
