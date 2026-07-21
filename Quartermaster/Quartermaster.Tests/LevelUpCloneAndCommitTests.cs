using Quartermaster.Services;
using Radoub.Formats.Bic;
using Radoub.Formats.Utc;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Pins the clone-and-commit contract the Level Up wizard depends on (#2572, #2573).
///
/// The wizard edits a clone and writes the caller's creature only when the apply
/// succeeds. These tests exercise that lifecycle against the real level-up service
/// without instantiating the Avalonia window: clone, mutate, then either discard
/// (cancel, close, error) or commit (finish).
/// </summary>
public class LevelUpCloneAndCommitTests
{
    [Fact]
    public void DiscardedClone_LeavesLiveCreatureUntouched()
    {
        // Models cancelling or closing the wizard after edits.
        var live = CreateCreature();
        var working = CreatureCloning.Clone(live);

        working.Str = 18;
        working.HitPoints = 99;
        LevelUpApplicationService.ApplyClassLevel(working, 0);

        Assert.Equal(14, live.Str);
        Assert.Equal(8, live.HitPoints);
        Assert.Equal(1, live.ClassList[0].ClassLevel);
    }

    [Fact]
    public void DiscardedClone_AfterAbilityIncrements_LeavesLiveCreatureUntouched()
    {
        // #2573: tentative increments are projected onto the working copy so feat
        // prereqs see them. Closing without finishing must not leak them.
        var live = CreateCreature();
        var working = CreatureCloning.Clone(live);

        working.Str = (byte)(working.Str + 2);
        working.Con = (byte)(working.Con + 1);

        Assert.Equal(14, live.Str);
        Assert.Equal(14, live.Con);
    }

    [Fact]
    public void CommittedClone_PublishesEveryChangedField()
    {
        var live = CreateCreature();
        var working = CreatureCloning.Clone(live);

        working.Str = 18;
        working.HitPoints = 42;
        working.FortBonus = 7;
        LevelUpApplicationService.ApplyClassLevel(working, 0);

        live.CopyFrom(working);

        Assert.Equal(18, live.Str);
        Assert.Equal(42, live.HitPoints);
        Assert.Equal(7, live.FortBonus);
        Assert.Equal(2, live.ClassList[0].ClassLevel);
    }

    [Fact]
    public void CommittedClone_PublishesHpAndSaves()
    {
        // #2572: the old rollback restored neither, so a partial commit left them
        // inflated. They must round-trip through the commit intact.
        var live = CreateCreature();
        var working = CreatureCloning.Clone(live);

        working.HitPoints = 55;
        working.MaxHitPoints = 55;
        working.CurrentHitPoints = 55;
        working.FortBonus = 4;
        working.RefBonus = 3;
        working.WillBonus = 2;

        live.CopyFrom(working);

        Assert.Equal(55, live.HitPoints);
        Assert.Equal(55, live.MaxHitPoints);
        Assert.Equal(55, live.CurrentHitPoints);
        Assert.Equal(4, live.FortBonus);
        Assert.Equal(3, live.RefBonus);
        Assert.Equal(2, live.WillBonus);
    }

    [Fact]
    public void FailedApply_LeavesLiveCreatureUntouched()
    {
        // Models a throw partway through apply: HP and saves are already written to
        // the clone, then a later step fails and the clone is discarded uncommitted.
        var live = CreateCreature();
        var working = CreatureCloning.Clone(live);

        try
        {
            LevelUpApplicationService.ApplyHitPoints(working, 20);
            working.FortBonus = 9;
            throw new InvalidOperationException("simulated failure after HP/saves");
        }
        catch (InvalidOperationException)
        {
            // Wizard discards the clone; no rollback call.
        }

        Assert.Equal(8, live.HitPoints);
        Assert.Equal(0, live.FortBonus);
    }

    [Fact]
    public void Commit_PreservesLiveCreatureInstance()
    {
        // Panels hold the live creature by reference, so the commit must mutate in
        // place rather than swap the object.
        var live = CreateCreature();
        var heldByPanel = live;
        var working = CreatureCloning.Clone(live);
        working.Str = 18;

        live.CopyFrom(working);

        Assert.Same(heldByPanel, live);
        Assert.Equal(18, heldByPanel.Str);
    }

    [Fact]
    public void BicCreature_SurvivesCloneAndCommitWithPlayerFields()
    {
        // The wizard levels BIC files too. DeepCopy would downcast and drop these
        // (#2698); CreatureCloning.Clone preserves the runtime type.
        var live = CreateBic();
        live.Gold = 5000u;
        live.Experience = 15000u;
        live.Age = 30;

        var working = CreatureCloning.Clone(live);
        Assert.IsType<BicFile>(working);

        working.Str = 18;
        live.CopyFrom(working);

        Assert.Equal(18, live.Str);
        Assert.Equal(5000u, live.Gold);
        Assert.Equal(15000u, live.Experience);
        Assert.Equal(30, live.Age);
    }

    [Fact]
    public void BicCreature_DiscardedClone_LeavesPlayerFieldsUntouched()
    {
        var live = CreateBic();
        live.Gold = 5000u;

        var working = (BicFile)CreatureCloning.Clone(live);
        working.Gold = 1u;
        working.Str = 18;

        Assert.Equal(5000u, live.Gold);
        Assert.Equal(14, live.Str);
    }

    private static UtcFile CreateCreature()
    {
        var creature = new UtcFile
        {
            Str = 14, Dex = 14, Con = 14, Int = 14, Wis = 14, Cha = 14,
            Race = 6,
            HitPoints = 8,
            MaxHitPoints = 8,
            CurrentHitPoints = 8
        };
        creature.FirstName.SetString(0, "TestCreature");
        creature.ClassList.Add(new CreatureClass { Class = 0, ClassLevel = 1 });
        for (int i = 0; i < 28; i++) creature.SkillList.Add(0);
        return creature;
    }

    private static BicFile CreateBic()
    {
        var bic = new BicFile
        {
            Str = 14, Dex = 14, Con = 14, Int = 14, Wis = 14, Cha = 14,
            Race = 6,
            HitPoints = 8,
            MaxHitPoints = 8,
            CurrentHitPoints = 8,
            Age = 25
        };
        bic.FirstName.SetString(0, "TestPC");
        bic.ClassList.Add(new CreatureClass { Class = 0, ClassLevel = 1 });
        for (int i = 0; i < 28; i++) bic.SkillList.Add(0);
        return bic;
    }
}
