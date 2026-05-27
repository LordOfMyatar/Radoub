using Radoub.Formats.Bic;
using Radoub.Formats.Utc;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Tests for CreatureCloning helper used by Quartermaster's Down-Level and
/// "save level 1 copy" flows. Verifies that cloning a BIC preserves player-only
/// fields (Age, Gold, Experience, QBList, LvlStatList, ReputationList) and that
/// extension-based save dispatch writes BIC bytes for .bic paths.
///
/// Regression coverage for #2249.
/// </summary>
public class CreatureCloneTests
{
    [Fact]
    public void Clone_BicSource_ReturnsBicFileInstance()
    {
        var bic = CreateTestBic();

        var clone = CreatureCloning.Clone(bic);

        Assert.IsType<BicFile>(clone);
    }

    [Fact]
    public void Clone_BicSource_PreservesAge()
    {
        var bic = CreateTestBic();
        bic.Age = 37;

        var clone = (BicFile)CreatureCloning.Clone(bic);

        Assert.Equal(37, clone.Age);
    }

    [Fact]
    public void Clone_BicSource_PreservesGold()
    {
        var bic = CreateTestBic();
        bic.Gold = 12345u;

        var clone = (BicFile)CreatureCloning.Clone(bic);

        Assert.Equal(12345u, clone.Gold);
    }

    [Fact]
    public void Clone_BicSource_PreservesExperience()
    {
        var bic = CreateTestBic();
        bic.Experience = 90000u;

        var clone = (BicFile)CreatureCloning.Clone(bic);

        Assert.Equal(90000u, clone.Experience);
    }

    [Fact]
    public void Clone_BicSource_PreservesQuickBar()
    {
        var bic = CreateTestBic();
        bic.QBList.Clear();
        for (int i = 0; i < 36; i++)
            bic.QBList.Add(new QuickBarSlot { ObjectType = QuickBarObjectType.Empty });
        bic.QBList[5] = new QuickBarSlot
        {
            ObjectType = QuickBarObjectType.Spell,
            INTParam1 = 42,
            MultiClass = 1
        };

        var clone = (BicFile)CreatureCloning.Clone(bic);

        Assert.Equal(36, clone.QBList.Count);
        Assert.Equal(QuickBarObjectType.Spell, clone.QBList[5].ObjectType);
        Assert.Equal(42, clone.QBList[5].INTParam1);
        Assert.Equal(1, clone.QBList[5].MultiClass);
    }

    [Fact]
    public void Clone_BicSource_PreservesLvlStatList()
    {
        var bic = CreateTestBic();
        bic.LvlStatList.Add(new LevelStatEntry
        {
            LvlStatClass = 4,
            LvlStatHitDie = 8,
            EpicLevel = 0,
            SkillPoints = 3
        });

        var clone = (BicFile)CreatureCloning.Clone(bic);

        Assert.Single(clone.LvlStatList);
        Assert.Equal((byte)4, clone.LvlStatList[0].LvlStatClass);
        Assert.Equal((byte)8, clone.LvlStatList[0].LvlStatHitDie);
        Assert.Equal((short)3, clone.LvlStatList[0].SkillPoints);
    }

    [Fact]
    public void Clone_BicSource_PreservesReputationList()
    {
        var bic = CreateTestBic();
        bic.ReputationList.Add(50);
        bic.ReputationList.Add(75);

        var clone = (BicFile)CreatureCloning.Clone(bic);

        Assert.Equal(2, clone.ReputationList.Count);
        Assert.Equal(50, clone.ReputationList[0]);
        Assert.Equal(75, clone.ReputationList[1]);
    }

    [Fact]
    public void Clone_BicSource_IsDeepCopy()
    {
        var bic = CreateTestBic();
        bic.Gold = 100u;

        var clone = (BicFile)CreatureCloning.Clone(bic);
        clone.Gold = 999u;
        clone.ClassList[0].ClassLevel = 99;

        Assert.Equal(100u, bic.Gold);
        Assert.Equal(1, bic.ClassList[0].ClassLevel);
    }

    [Fact]
    public void Clone_UtcSource_ReturnsUtcFile()
    {
        var utc = CreateTestUtc();

        var clone = CreatureCloning.Clone(utc);

        Assert.IsType<UtcFile>(clone);
        Assert.False(clone is BicFile);
    }

    [Fact]
    public void Clone_UtcSource_RoundTripPreservesAbilityScores()
    {
        var utc = CreateTestUtc();
        utc.Str = 18;
        utc.Dex = 14;

        var clone = CreatureCloning.Clone(utc);

        Assert.Equal(18, clone.Str);
        Assert.Equal(14, clone.Dex);
    }

    [Fact]
    public void Save_BicExtension_WritesBicFileType()
    {
        var bic = CreateTestBic();
        var path = Path.Combine(Path.GetTempPath(), $"qm_test_{Guid.NewGuid():N}.bic");

        try
        {
            CreatureCloning.Save(bic, path);
            var reloaded = BicReader.Read(path);

            Assert.Equal("BIC ", reloaded.FileType);
            Assert.True(reloaded.IsPC);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Save_BicExtension_PreservesBicFieldsThroughDisk()
    {
        var bic = CreateTestBic();
        bic.Age = 42;
        bic.Gold = 555u;
        bic.Experience = 6000u;
        var path = Path.Combine(Path.GetTempPath(), $"qm_test_{Guid.NewGuid():N}.bic");

        try
        {
            CreatureCloning.Save(bic, path);
            var reloaded = BicReader.Read(path);

            Assert.Equal(42, reloaded.Age);
            Assert.Equal(555u, reloaded.Gold);
            Assert.Equal(6000u, reloaded.Experience);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Save_UtcExtension_WritesUtcFileType()
    {
        var utc = CreateTestUtc();
        var path = Path.Combine(Path.GetTempPath(), $"qm_test_{Guid.NewGuid():N}.utc");

        try
        {
            CreatureCloning.Save(utc, path);
            var reloaded = UtcReader.Read(File.ReadAllBytes(path));

            Assert.Equal("UTC ", reloaded.FileType);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Save_BicUpcastAsUtcReference_StillWritesBicForBicExtension()
    {
        // Quartermaster holds the current creature as UtcFile? but it may actually
        // be a BicFile. Save must dispatch on runtime type + extension, not the
        // compile-time reference.
        UtcFile reference = CreateTestBic();
        ((BicFile)reference).Gold = 222u;
        var path = Path.Combine(Path.GetTempPath(), $"qm_test_{Guid.NewGuid():N}.bic");

        try
        {
            CreatureCloning.Save(reference, path);
            var reloaded = BicReader.Read(path);

            Assert.Equal("BIC ", reloaded.FileType);
            Assert.Equal(222u, reloaded.Gold);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static BicFile CreateTestBic()
    {
        var bic = new BicFile
        {
            Str = 10, Dex = 10, Con = 10, Int = 10, Wis = 10, Cha = 10,
            Race = 6,
            Gender = 0,
            HitPoints = 8,
            MaxHitPoints = 8,
            CurrentHitPoints = 8,
            Age = 25,
            Experience = 0,
            Gold = 0
        };
        bic.FirstName.SetString(0, "TestPC");
        bic.ClassList.Add(new CreatureClass { Class = 0, ClassLevel = 1 });
        for (int i = 0; i < 28; i++) bic.SkillList.Add(0);
        return bic;
    }

    private static UtcFile CreateTestUtc()
    {
        var utc = new UtcFile
        {
            Str = 10, Dex = 10, Con = 10, Int = 10, Wis = 10, Cha = 10,
            Race = 6,
            Gender = 0,
            HitPoints = 8,
            MaxHitPoints = 8,
            CurrentHitPoints = 8
        };
        utc.FirstName.SetString(0, "TestNPC");
        utc.ClassList.Add(new CreatureClass { Class = 0, ClassLevel = 1 });
        for (int i = 0; i < 28; i++) utc.SkillList.Add(0);
        return utc;
    }
}
