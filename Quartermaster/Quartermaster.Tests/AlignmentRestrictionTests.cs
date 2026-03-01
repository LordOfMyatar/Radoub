using Quartermaster.Services;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for alignment restriction parsing and validation.
/// Verifies that classes.2da AlignRestrict/AlignRstrctType/InvertRestrict
/// columns are correctly parsed (including 0x hex prefix) and enforced.
/// Mock data matches NWN classes.2da values exactly.
/// </summary>
public class AlignmentRestrictionTests
{
    private readonly ClassService _classService;

    public AlignmentRestrictionTests()
    {
        var mockGameData = new MockGameDataService(includeSampleData: true);
        var displayService = new CreatureDisplayService(mockGameData);
        _classService = displayService.Classes;
    }

    #region Hex Parsing — 0x prefix must be stripped

    [Fact]
    public void Monk_HasAlignmentRestriction()
    {
        var metadata = _classService.GetClassMetadata(5);
        Assert.NotNull(metadata.AlignmentRestriction);
    }

    [Fact]
    public void Monk_RestrictionMask_Is0x05()
    {
        // 0x05 = neutral(0x01) + chaotic(0x04) prohibited
        var metadata = _classService.GetClassMetadata(5);
        Assert.Equal(0x05, metadata.AlignmentRestriction!.RestrictionMask);
    }

    [Fact]
    public void Monk_NotInverted()
    {
        var metadata = _classService.GetClassMetadata(5);
        Assert.False(metadata.AlignmentRestriction!.Inverted);
    }

    [Fact]
    public void Barbarian_HasAlignmentRestriction()
    {
        var metadata = _classService.GetClassMetadata(0);
        Assert.NotNull(metadata.AlignmentRestriction);
    }

    [Fact]
    public void Barbarian_NotInverted()
    {
        // Barbarian: mask=0x02 (lawful), invert=0 → lawful is PROHIBITED
        var metadata = _classService.GetClassMetadata(0);
        Assert.False(metadata.AlignmentRestriction!.Inverted);
    }

    [Fact]
    public void Paladin_RestrictionMask_Is0x15()
    {
        // 0x15 = neutral(0x01) + chaotic(0x04) + evil(0x10) prohibited
        var metadata = _classService.GetClassMetadata(6);
        Assert.NotNull(metadata.AlignmentRestriction);
        Assert.Equal(0x15, metadata.AlignmentRestriction!.RestrictionMask);
    }

    [Fact]
    public void Paladin_RestrictionType_IsBothAxes()
    {
        var metadata = _classService.GetClassMetadata(6);
        Assert.Equal(0x03, metadata.AlignmentRestriction!.RestrictionType);
    }

    [Fact]
    public void Fighter_HasNoAlignmentRestriction()
    {
        // Fighter: 0x00 mask → no restriction created
        var metadata = _classService.GetClassMetadata(4);
        Assert.Null(metadata.AlignmentRestriction);
    }

    [Fact]
    public void Cleric_HasNoAlignmentRestriction()
    {
        // Cleric: 0x00 mask → no restriction created
        var metadata = _classService.GetClassMetadata(2);
        Assert.Null(metadata.AlignmentRestriction);
    }

    [Fact]
    public void Druid_RestrictionMask_IsNeutral()
    {
        // Druid: mask=0x01 (neutral), type=0x03, invert=1 → neutral REQUIRED on at least one axis
        var metadata = _classService.GetClassMetadata(3);
        Assert.NotNull(metadata.AlignmentRestriction);
        Assert.Equal(0x01, metadata.AlignmentRestriction!.RestrictionMask);
        Assert.True(metadata.AlignmentRestriction.Inverted);
    }

    #endregion

    #region Monk — must be Lawful (mask=0x05 N+C prohibited, type=0x01 LC axis, invert=0)

    [Theory]
    [InlineData(100, 100, true)]  // LG
    [InlineData(50, 100, true)]   // LN
    [InlineData(0, 100, true)]    // LE
    [InlineData(100, 50, false)]  // NG
    [InlineData(50, 50, false)]   // TN
    [InlineData(0, 50, false)]    // NE
    [InlineData(100, 0, false)]   // CG
    [InlineData(50, 0, false)]    // CN
    [InlineData(0, 0, false)]     // CE
    public void Monk_AllowsOnlyLawfulAlignments(byte goodEvil, byte lawChaos, bool expected)
    {
        var metadata = _classService.GetClassMetadata(5);
        bool allowed = IsAlignmentAllowed(metadata.AlignmentRestriction!, goodEvil, lawChaos);
        Assert.Equal(expected, allowed);
    }

    #endregion

    #region Barbarian — cannot be Lawful (mask=0x02 lawful prohibited, type=0x01 LC axis, invert=0)

    [Theory]
    [InlineData(100, 100, false)] // LG
    [InlineData(50, 100, false)]  // LN
    [InlineData(0, 100, false)]   // LE
    [InlineData(100, 50, true)]   // NG
    [InlineData(50, 50, true)]    // TN
    [InlineData(0, 50, true)]     // NE
    [InlineData(100, 0, true)]    // CG
    [InlineData(50, 0, true)]     // CN
    [InlineData(0, 0, true)]      // CE
    public void Barbarian_BlocksLawfulAlignments(byte goodEvil, byte lawChaos, bool expected)
    {
        var metadata = _classService.GetClassMetadata(0);
        bool allowed = IsAlignmentAllowed(metadata.AlignmentRestriction!, goodEvil, lawChaos);
        Assert.Equal(expected, allowed);
    }

    #endregion

    #region Paladin — must be Lawful Good (mask=0x15 N+C+E prohibited, type=0x03 both, invert=0)

    [Theory]
    [InlineData(100, 100, true)]  // LG — only valid alignment
    [InlineData(100, 50, false)]  // NG — neutral on LC axis → prohibited
    [InlineData(100, 0, false)]   // CG — chaotic → prohibited
    [InlineData(50, 100, false)]  // LN — neutral on GE axis → prohibited
    [InlineData(50, 50, false)]   // TN — neutral on both → prohibited
    [InlineData(50, 0, false)]    // CN — chaotic + neutral → prohibited
    [InlineData(0, 100, false)]   // LE — evil → prohibited
    [InlineData(0, 50, false)]    // NE — evil + neutral → prohibited
    [InlineData(0, 0, false)]     // CE — chaotic + evil → prohibited
    public void Paladin_AllowsOnlyLawfulGood(byte goodEvil, byte lawChaos, bool expected)
    {
        var metadata = _classService.GetClassMetadata(6);
        bool allowed = IsAlignmentAllowed(metadata.AlignmentRestriction!, goodEvil, lawChaos);
        Assert.Equal(expected, allowed);
    }

    #endregion

    #region Druid — must have Neutral on at least one axis (mask=0x01 neutral, type=0x03, invert=1)

    [Theory]
    [InlineData(100, 50, true)]   // NG — neutral on LC
    [InlineData(50, 100, true)]   // LN — neutral on GE
    [InlineData(50, 50, true)]    // TN — neutral on both
    [InlineData(50, 0, true)]     // CN — neutral on GE
    [InlineData(0, 50, true)]     // NE — neutral on LC
    [InlineData(100, 100, false)] // LG — no neutral
    [InlineData(100, 0, false)]   // CG — no neutral
    [InlineData(0, 100, false)]   // LE — no neutral
    [InlineData(0, 0, false)]     // CE — no neutral
    public void Druid_RequiresNeutralAxis(byte goodEvil, byte lawChaos, bool expected)
    {
        var metadata = _classService.GetClassMetadata(3);
        bool allowed = IsAlignmentAllowed(metadata.AlignmentRestriction!, goodEvil, lawChaos);
        Assert.Equal(expected, allowed);
    }

    #endregion

    #region Bard — cannot be Lawful (same restriction as Barbarian)

    [Theory]
    [InlineData(100, 100, false)] // LG — blocked
    [InlineData(100, 50, true)]   // NG — allowed
    [InlineData(100, 0, true)]    // CG — allowed
    [InlineData(50, 100, false)]  // LN — blocked
    [InlineData(50, 50, true)]    // TN — allowed
    [InlineData(0, 0, true)]      // CE — allowed
    public void Bard_BlocksLawfulAlignments(byte goodEvil, byte lawChaos, bool expected)
    {
        var metadata = _classService.GetClassMetadata(1);
        bool allowed = IsAlignmentAllowed(metadata.AlignmentRestriction!, goodEvil, lawChaos);
        Assert.Equal(expected, allowed);
    }

    #endregion

    /// <summary>
    /// Mirror of NewCharacterWizardWindow.IsAlignmentAllowed for testability.
    /// Must stay in sync with the NCW implementation.
    /// </summary>
    private static bool IsAlignmentAllowed(AlignmentRestriction restriction, byte goodEvil, byte lawChaos)
    {
        bool isLawful = lawChaos > 70;
        bool isChaotic = lawChaos < 30;
        bool isNeutralLC = !isLawful && !isChaotic;

        bool isGood = goodEvil > 70;
        bool isEvil = goodEvil < 30;
        bool isNeutralGE = !isGood && !isEvil;

        int mask = restriction.RestrictionMask;
        int type = restriction.RestrictionType;

        bool maskHasLawful = (mask & 0x02) != 0;
        bool maskHasChaotic = (mask & 0x04) != 0;
        bool maskHasGood = (mask & 0x08) != 0;
        bool maskHasEvil = (mask & 0x10) != 0;
        bool maskHasNeutral = (mask & 0x01) != 0;

        bool matches;
        if (type == 0x01)
        {
            matches = (maskHasLawful && isLawful) || (maskHasChaotic && isChaotic)
                || (maskHasNeutral && isNeutralLC);
        }
        else if (type == 0x02)
        {
            matches = (maskHasGood && isGood) || (maskHasEvil && isEvil)
                || (maskHasNeutral && isNeutralGE);
        }
        else if (type == 0x03)
        {
            bool lcMatch = (maskHasLawful && isLawful) || (maskHasChaotic && isChaotic);
            bool geMatch = (maskHasGood && isGood) || (maskHasEvil && isEvil);
            bool neutralMatch = maskHasNeutral && (isNeutralLC || isNeutralGE);
            matches = lcMatch || geMatch || neutralMatch;
        }
        else
        {
            int alignBits = 0;
            if (isGood) alignBits |= 0x08;
            if (isEvil) alignBits |= 0x10;
            if (isLawful) alignBits |= 0x02;
            if (isChaotic) alignBits |= 0x04;
            if (isNeutralLC || isNeutralGE) alignBits |= 0x01;
            matches = (alignBits & mask) != 0;
        }

        // Invert=0: mask = prohibited → block if matches
        // Invert=1: mask = required → allow if matches
        return restriction.Inverted ? matches : !matches;
    }
}
