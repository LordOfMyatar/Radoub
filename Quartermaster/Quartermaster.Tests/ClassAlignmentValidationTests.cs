using Quartermaster.Services;
using Radoub.Formats.Utc;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests that ClassService.GetAvailableClasses correctly marks alignment-restricted
/// classes based on the creature's actual alignment values.
///
/// These tests exercise the REAL ClassService.CheckAlignmentRestriction method
/// (not the test-local mirror in AlignmentRestrictionTests) to catch scale/inversion bugs.
///
/// UtcFile alignment scale:
///   GoodEvil: 0 = Evil, 50 = Neutral, 100 = Good
///   LawfulChaotic: 0 = Chaotic, 50 = Neutral, 100 = Lawful
/// </summary>
public class ClassAlignmentValidationTests
{
    private readonly ClassService _classService;

    public ClassAlignmentValidationTests()
    {
        var mockGameData = new MockGameDataService(includeSampleData: true);
        var displayService = new CreatureDisplayService(mockGameData);
        _classService = displayService.Classes;
    }

    private UtcFile CreateCreature(byte goodEvil, byte lawChaos)
    {
        var creature = new UtcFile
        {
            GoodEvil = goodEvil,
            LawfulChaotic = lawChaos
        };
        // Need at least one class for GetAvailableClasses
        creature.ClassList.Add(new CreatureClass { Class = 4, ClassLevel = 1 }); // Fighter (no alignment restriction)
        return creature;
    }

    private AvailableClass? GetClass(UtcFile creature, int classId)
    {
        var classes = _classService.GetAvailableClasses(creature, includeUnqualified: true);
        return classes.FirstOrDefault(c => c.ClassId == classId);
    }

    #region Monk — requires Lawful (mask=0x05 N+C prohibited, type=0x01, invert=0)

    [Fact]
    public void Monk_LawfulGoodCreature_IsQualified()
    {
        var creature = CreateCreature(goodEvil: 100, lawChaos: 100); // LG
        var monk = GetClass(creature, 5);
        Assert.NotNull(monk);
        Assert.Equal(ClassQualification.Qualified, monk!.Qualification);
        Assert.True(monk.CanSelect);
    }

    [Fact]
    public void Monk_ChaoticEvilCreature_IsAlignmentRestricted()
    {
        var creature = CreateCreature(goodEvil: 0, lawChaos: 0); // CE
        var monk = GetClass(creature, 5);
        Assert.NotNull(monk);
        Assert.Equal(ClassQualification.AlignmentRestricted, monk!.Qualification);
        Assert.False(monk.CanSelect);
    }

    [Fact]
    public void Monk_TrueNeutralCreature_IsAlignmentRestricted()
    {
        var creature = CreateCreature(goodEvil: 50, lawChaos: 50); // TN
        var monk = GetClass(creature, 5);
        Assert.NotNull(monk);
        Assert.Equal(ClassQualification.AlignmentRestricted, monk!.Qualification);
        Assert.False(monk.CanSelect);
    }

    [Fact]
    public void Monk_LawfulNeutralCreature_IsQualified()
    {
        var creature = CreateCreature(goodEvil: 50, lawChaos: 100); // LN
        var monk = GetClass(creature, 5);
        Assert.NotNull(monk);
        Assert.Equal(ClassQualification.Qualified, monk!.Qualification);
    }

    [Fact]
    public void Monk_ChaoticGoodCreature_IsAlignmentRestricted()
    {
        var creature = CreateCreature(goodEvil: 100, lawChaos: 0); // CG
        var monk = GetClass(creature, 5);
        Assert.NotNull(monk);
        Assert.Equal(ClassQualification.AlignmentRestricted, monk!.Qualification);
    }

    #endregion

    #region Barbarian — cannot be Lawful (mask=0x02, type=0x01, invert=0)

    [Fact]
    public void Barbarian_ChaoticEvilCreature_IsQualified()
    {
        var creature = CreateCreature(goodEvil: 0, lawChaos: 0); // CE
        var barbarian = GetClass(creature, 0);
        Assert.NotNull(barbarian);
        Assert.Equal(ClassQualification.Qualified, barbarian!.Qualification);
        Assert.True(barbarian.CanSelect);
    }

    [Fact]
    public void Barbarian_LawfulGoodCreature_IsAlignmentRestricted()
    {
        var creature = CreateCreature(goodEvil: 100, lawChaos: 100); // LG
        var barbarian = GetClass(creature, 0);
        Assert.NotNull(barbarian);
        Assert.Equal(ClassQualification.AlignmentRestricted, barbarian!.Qualification);
        Assert.False(barbarian.CanSelect);
    }

    [Fact]
    public void Barbarian_TrueNeutralCreature_IsQualified()
    {
        var creature = CreateCreature(goodEvil: 50, lawChaos: 50); // TN
        var barbarian = GetClass(creature, 0);
        Assert.NotNull(barbarian);
        Assert.Equal(ClassQualification.Qualified, barbarian!.Qualification);
    }

    #endregion

    #region Paladin — requires Lawful Good (mask=0x15, type=0x03, invert=0)

    [Fact]
    public void Paladin_LawfulGoodCreature_IsQualified()
    {
        var creature = CreateCreature(goodEvil: 100, lawChaos: 100); // LG
        var paladin = GetClass(creature, 6);
        Assert.NotNull(paladin);
        Assert.Equal(ClassQualification.Qualified, paladin!.Qualification);
        Assert.True(paladin.CanSelect);
    }

    [Fact]
    public void Paladin_ChaoticEvilCreature_IsAlignmentRestricted()
    {
        var creature = CreateCreature(goodEvil: 0, lawChaos: 0); // CE
        var paladin = GetClass(creature, 6);
        Assert.NotNull(paladin);
        Assert.Equal(ClassQualification.AlignmentRestricted, paladin!.Qualification);
        Assert.False(paladin.CanSelect);
    }

    [Fact]
    public void Paladin_TrueNeutralCreature_IsAlignmentRestricted()
    {
        var creature = CreateCreature(goodEvil: 50, lawChaos: 50); // TN
        var paladin = GetClass(creature, 6);
        Assert.NotNull(paladin);
        Assert.Equal(ClassQualification.AlignmentRestricted, paladin!.Qualification);
    }

    [Fact]
    public void Paladin_LawfulEvilCreature_IsAlignmentRestricted()
    {
        var creature = CreateCreature(goodEvil: 0, lawChaos: 100); // LE
        var paladin = GetClass(creature, 6);
        Assert.NotNull(paladin);
        Assert.Equal(ClassQualification.AlignmentRestricted, paladin!.Qualification);
    }

    [Fact]
    public void Paladin_NeutralGoodCreature_IsAlignmentRestricted()
    {
        var creature = CreateCreature(goodEvil: 100, lawChaos: 50); // NG
        var paladin = GetClass(creature, 6);
        Assert.NotNull(paladin);
        Assert.Equal(ClassQualification.AlignmentRestricted, paladin!.Qualification);
    }

    #endregion

    #region Druid — requires Neutral on at least one axis (mask=0x01, type=0x03, invert=1)

    [Fact]
    public void Druid_TrueNeutralCreature_IsQualified()
    {
        var creature = CreateCreature(goodEvil: 50, lawChaos: 50); // TN
        var druid = GetClass(creature, 3);
        Assert.NotNull(druid);
        Assert.Equal(ClassQualification.Qualified, druid!.Qualification);
    }

    [Fact]
    public void Druid_NeutralGoodCreature_IsQualified()
    {
        var creature = CreateCreature(goodEvil: 100, lawChaos: 50); // NG
        var druid = GetClass(creature, 3);
        Assert.NotNull(druid);
        Assert.Equal(ClassQualification.Qualified, druid!.Qualification);
    }

    [Fact]
    public void Druid_LawfulGoodCreature_IsAlignmentRestricted()
    {
        var creature = CreateCreature(goodEvil: 100, lawChaos: 100); // LG — no neutral axis
        var druid = GetClass(creature, 3);
        Assert.NotNull(druid);
        Assert.Equal(ClassQualification.AlignmentRestricted, druid!.Qualification);
    }

    [Fact]
    public void Druid_ChaoticEvilCreature_IsAlignmentRestricted()
    {
        var creature = CreateCreature(goodEvil: 0, lawChaos: 0); // CE — no neutral axis
        var druid = GetClass(creature, 3);
        Assert.NotNull(druid);
        Assert.Equal(ClassQualification.AlignmentRestricted, druid!.Qualification);
    }

    #endregion

    #region Fighter — no alignment restriction

    [Theory]
    [InlineData(100, 100)] // LG
    [InlineData(0, 0)]     // CE
    [InlineData(50, 50)]   // TN
    public void Fighter_AnyAlignment_IsQualified(byte goodEvil, byte lawChaos)
    {
        var creature = CreateCreature(goodEvil, lawChaos);
        var fighter = GetClass(creature, 4);
        Assert.NotNull(fighter);
        // Fighter is the creature's existing class, so should be Qualified
        Assert.Equal(ClassQualification.Qualified, fighter!.Qualification);
    }

    #endregion

    #region Bard — cannot be Lawful (mask=0x02, type=0x01, invert=0)

    [Fact]
    public void Bard_ChaoticGoodCreature_IsQualified()
    {
        var creature = CreateCreature(goodEvil: 100, lawChaos: 0); // CG
        var bard = GetClass(creature, 1);
        Assert.NotNull(bard);
        Assert.Equal(ClassQualification.Qualified, bard!.Qualification);
    }

    [Fact]
    public void Bard_LawfulNeutralCreature_IsAlignmentRestricted()
    {
        var creature = CreateCreature(goodEvil: 50, lawChaos: 100); // LN
        var bard = GetClass(creature, 1);
        Assert.NotNull(bard);
        Assert.Equal(ClassQualification.AlignmentRestricted, bard!.Qualification);
    }

    #endregion

    #region Description text validation

    [Fact]
    public void Monk_RestrictionDescription_SaysCannotBeNeutralOrChaotic()
    {
        // Monk: invert=0, mask has Neutral+Chaotic → "Cannot be: Neutral or Chaotic"
        var creature = CreateCreature(goodEvil: 50, lawChaos: 50); // TN — triggers restriction
        var monk = GetClass(creature, 5);
        Assert.NotNull(monk?.PrerequisiteResult);
        var otherReqs = monk!.PrerequisiteResult!.OtherRequirements;
        Assert.Single(otherReqs);
        Assert.Contains("Cannot be", otherReqs[0].Description);
    }

    [Fact]
    public void Druid_RestrictionDescription_SaysMustBeNeutral()
    {
        // Druid: invert=1, mask has Neutral → "Must be: Neutral"
        var creature = CreateCreature(goodEvil: 100, lawChaos: 100); // LG — triggers restriction
        var druid = GetClass(creature, 3);
        Assert.NotNull(druid?.PrerequisiteResult);
        var otherReqs = druid!.PrerequisiteResult!.OtherRequirements;
        Assert.Single(otherReqs);
        Assert.Contains("Must be", otherReqs[0].Description);
    }

    #endregion
}
