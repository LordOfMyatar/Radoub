using Radoub.Formats.Services;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Tests for the IGameDataService rules accessors added for #2481:
/// GetXpForLevel, GetClassHitDie, GetSkillCount, GetEpicLevelThreshold.
/// These default-interface methods source NWN rules from 2DA, falling back to
/// stock-game constants when the table is missing. Uses MockGameDataService with
/// includeSampleData:false so each test seeds only the rows it asserts on.
/// </summary>
public class GameDataRulesTests
{
    private static IGameDataService Empty() => new MockGameDataService(includeSampleData: false);

    // --- GetXpForLevel ---

    [Fact]
    public void GetXpForLevel_ReadsExpTable()
    {
        var mock = new MockGameDataService(includeSampleData: false);
        // exptable row 0 = level 1, XP column. Use non-stock values to prove it reads the table.
        mock.Set2DAValue("exptable", 0, "XP", "0");     // level 1
        mock.Set2DAValue("exptable", 1, "XP", "500");   // level 2
        mock.Set2DAValue("exptable", 2, "XP", "1500");  // level 3
        IGameDataService svc = mock;

        Assert.Equal(0u, svc.GetXpForLevel(1));
        Assert.Equal(500u, svc.GetXpForLevel(2));
        Assert.Equal(1500u, svc.GetXpForLevel(3));
    }

    [Fact]
    public void GetXpForLevel_NoTable_FallsBackToStockFormula()
    {
        IGameDataService svc = Empty();
        // Stock formula: (N-1)*N/2*1000. Level 3 → 2*3/2*1000 = 3000.
        Assert.Equal(3000u, svc.GetXpForLevel(3));
        Assert.Equal(0u, svc.GetXpForLevel(1));
    }

    // --- GetClassHitDie ---

    [Fact]
    public void GetClassHitDie_ReadsClassesTable()
    {
        var mock = new MockGameDataService(includeSampleData: false);
        mock.Set2DAValue("classes", 0, "HitDie", "12"); // Barbarian d12
        mock.Set2DAValue("classes", 1, "HitDie", "4");  // Wizard d4
        IGameDataService svc = mock;

        Assert.Equal(12, svc.GetClassHitDie(0));
        Assert.Equal(4, svc.GetClassHitDie(1));
    }

    [Fact]
    public void GetClassHitDie_NoTable_ReturnsZero()
    {
        IGameDataService svc = Empty();
        Assert.Equal(0, svc.GetClassHitDie(0));
    }

    // --- GetSkillCount ---

    [Fact]
    public void GetSkillCount_ReturnsSkillsTableRowCount()
    {
        var mock = new MockGameDataService(includeSampleData: false);
        for (int i = 0; i < 40; i++) mock.Set2DAValue("skills", i, "Label", $"Skill{i}");
        IGameDataService svc = mock;

        Assert.Equal(40, svc.GetSkillCount()); // PRC/custom content adds skills past stock 28
    }

    [Fact]
    public void GetSkillCount_NoTable_FallsBackTo28()
    {
        IGameDataService svc = Empty();
        Assert.Equal(28, svc.GetSkillCount());
    }

    // --- GetEpicLevelThreshold ---

    [Fact]
    public void GetEpicLevelThreshold_DefaultsTo21()
    {
        IGameDataService svc = Empty();
        Assert.Equal(21, svc.GetEpicLevelThreshold());
    }
}
