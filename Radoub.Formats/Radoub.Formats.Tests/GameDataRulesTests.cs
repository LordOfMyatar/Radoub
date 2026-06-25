using Radoub.Formats.Services;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Tests for the IGameDataService rules accessors added for #2481:
/// GetXpForLevel, GetClassHitDie, GetSkillCount, GetEpicLevelThreshold.
/// These default-interface methods source NWN rules from 2DA, falling back to
/// stock-game constants when the table is missing.
/// </summary>
public class GameDataRulesTests
{
    private static TwoDA.TwoDAFile MakeTable(string[] columns, params string?[][] rows)
        => FakeGameDataService.MakeTable(columns, rows);

    // --- GetXpForLevel ---

    [Fact]
    public void GetXpForLevel_ReadsExpTable()
    {
        var fake = new FakeGameDataService();
        // exptable row 0 = level 1, XP column. Use non-stock values to prove it reads the table.
        fake.Add("exptable", MakeTable(new[] { "XP" },
            new[] { "0" },     // level 1
            new[] { "500" },   // level 2
            new[] { "1500" })); // level 3
        IGameDataService svc = fake;

        Assert.Equal(0u, svc.GetXpForLevel(1));
        Assert.Equal(500u, svc.GetXpForLevel(2));
        Assert.Equal(1500u, svc.GetXpForLevel(3));
    }

    [Fact]
    public void GetXpForLevel_NoTable_FallsBackToStockFormula()
    {
        IGameDataService svc = new FakeGameDataService();
        // Stock formula: (N-1)*N/2*1000. Level 3 → 2*3/2*1000 = 3000.
        Assert.Equal(3000u, svc.GetXpForLevel(3));
        Assert.Equal(0u, svc.GetXpForLevel(1));
    }

    // --- GetClassHitDie ---

    [Fact]
    public void GetClassHitDie_ReadsClassesTable()
    {
        var fake = new FakeGameDataService();
        fake.Add("classes", MakeTable(new[] { "Label", "HitDie" },
            new[] { "Barbarian", "12" },
            new[] { "Wizard", "4" }));
        IGameDataService svc = fake;

        Assert.Equal(12, svc.GetClassHitDie(0));
        Assert.Equal(4, svc.GetClassHitDie(1));
    }

    [Fact]
    public void GetClassHitDie_NoTable_ReturnsZero()
    {
        IGameDataService svc = new FakeGameDataService();
        Assert.Equal(0, svc.GetClassHitDie(0));
    }

    // --- GetSkillCount ---

    [Fact]
    public void GetSkillCount_ReturnsSkillsTableRowCount()
    {
        var fake = new FakeGameDataService();
        var rows = new string?[40][];
        for (int i = 0; i < 40; i++) rows[i] = new[] { $"Skill{i}" };
        fake.Add("skills", MakeTable(new[] { "Label" }, rows));
        IGameDataService svc = fake;

        Assert.Equal(40, svc.GetSkillCount()); // PRC/custom content adds skills past stock 28
    }

    [Fact]
    public void GetSkillCount_NoTable_FallsBackTo28()
    {
        IGameDataService svc = new FakeGameDataService();
        Assert.Equal(28, svc.GetSkillCount());
    }

    // --- GetEpicLevelThreshold ---

    [Fact]
    public void GetEpicLevelThreshold_DefaultsTo21()
    {
        IGameDataService svc = new FakeGameDataService();
        Assert.Equal(21, svc.GetEpicLevelThreshold());
    }
}
