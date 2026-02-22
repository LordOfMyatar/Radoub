using Radoub.Formats.Bic;
using Radoub.Formats.Gff;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Tests for UTC ↔ BIC conversion (FromUtcFile, ToUtcFile).
/// </summary>
public class BicConversionTests
{
    #region UTC to BIC Conversion Tests

    [Fact]
    public void FromUtcFile_InitializesQuickBarWith36Slots()
    {
        var utc = CreateTestUtcFile();

        var bic = BicFile.FromUtcFile(utc);

        Assert.Equal(36, bic.QBList.Count);
        Assert.All(bic.QBList, slot => Assert.Equal(QuickBarObjectType.Empty, slot.ObjectType));
    }

    [Fact]
    public void FromUtcFile_SetsReasonableAge()
    {
        var utc = CreateTestUtcFile();

        var bic = BicFile.FromUtcFile(utc);

        Assert.True(bic.Age >= 18, "Age should be at least 18 (NWN minimum)");
    }

    [Fact]
    public void FromUtcFile_EnsuresValidHitPoints()
    {
        var utc = CreateTestUtcFile();
        utc.HitPoints = 10;
        utc.MaxHitPoints = 10;
        utc.CurrentHitPoints = 0; // Dead creature

        var bic = BicFile.FromUtcFile(utc);

        Assert.True(bic.CurrentHitPoints > 0, "CurrentHitPoints should be positive for playable character");
        Assert.Equal(bic.MaxHitPoints, bic.CurrentHitPoints);
    }

    [Fact]
    public void FromUtcFile_SetsIsPcTrue()
    {
        var utc = CreateTestUtcFile();
        utc.IsPC = false;

        var bic = BicFile.FromUtcFile(utc);

        Assert.True(bic.IsPC, "IsPC must be true for player characters");
    }

    [Fact]
    public void FromUtcFile_CalculatesExperienceFromLevel()
    {
        var utc = CreateTestUtcFile();
        // Level 5 character: XP = (5-1)*5/2*1000 = 10000
        utc.ClassList[0].ClassLevel = 5;

        var bic = BicFile.FromUtcFile(utc);

        Assert.Equal(10000u, bic.Experience);
    }

    [Fact]
    public void FromUtcFile_CopiesAllBaseProperties()
    {
        var utc = CreateTestUtcFile();
        utc.Str = 18;
        utc.Dex = 14;
        utc.Con = 16;
        utc.Int = 10;
        utc.Wis = 12;
        utc.Cha = 8;
        utc.Race = 1; // Elf
        utc.Gender = 1; // Female
        utc.FirstName.SetString(0, "TestChar");
        utc.Tag = "test_tag";

        var bic = BicFile.FromUtcFile(utc);

        Assert.Equal(18, bic.Str);
        Assert.Equal(14, bic.Dex);
        Assert.Equal(16, bic.Con);
        Assert.Equal(10, bic.Int);
        Assert.Equal(12, bic.Wis);
        Assert.Equal(8, bic.Cha);
        Assert.Equal(1, bic.Race);
        Assert.Equal(1, bic.Gender);
        Assert.Equal("TestChar", bic.FirstName.GetDefault());
        Assert.Equal("test_tag", bic.Tag);
    }

    [Fact]
    public void FromUtcFile_PreservesPortraitString()
    {
        // UTC has Portrait string set - should preserve it
        var utc = CreateTestUtcFile();
        utc.PortraitId = 0;
        utc.Portrait = "po_el_f_03_";

        var bic = BicFile.FromUtcFile(utc);

        Assert.Equal("po_el_f_03_", bic.Portrait);
    }

    [Fact]
    public void FromUtcFile_PreservesPortraitId()
    {
        // UTC has PortraitId set - should preserve it
        var utc = CreateTestUtcFile();
        utc.PortraitId = 42;
        utc.Portrait = "";

        var bic = BicFile.FromUtcFile(utc);

        Assert.Equal(42, bic.PortraitId);
    }

    [Fact]
    public void FromUtcFile_SetsDefaultPortraitWhenBothEmpty()
    {
        // UTC has neither - should set default
        var utc = CreateTestUtcFile();
        utc.PortraitId = 0;
        utc.Portrait = "";

        var bic = BicFile.FromUtcFile(utc);

        Assert.Equal("po_hu_m_99_", bic.Portrait);
    }

    [Fact]
    public void FromUtcFile_RoundTrip_ProducesValidBicFile()
    {
        var utc = CreateTestUtcFile();
        utc.FirstName.SetString(0, "Converted");
        utc.HitPoints = 20;
        utc.MaxHitPoints = 20;
        utc.CurrentHitPoints = 20;

        var bic = BicFile.FromUtcFile(utc);
        var buffer = BicWriter.Write(bic);
        var roundTripped = BicReader.Read(buffer);

        // Verify critical fields survive round-trip
        Assert.Equal("BIC ", roundTripped.FileType);
        Assert.True(roundTripped.IsPC);
        Assert.Equal(36, roundTripped.QBList.Count);
        Assert.Equal("Converted", roundTripped.FirstName.GetDefault());
        Assert.True(roundTripped.CurrentHitPoints > 0);
    }

    private static Utc.UtcFile CreateTestUtcFile()
    {
        var utc = new Utc.UtcFile
        {
            Str = 10,
            Dex = 10,
            Con = 10,
            Int = 10,
            Wis = 10,
            Cha = 10,
            Race = 6, // Human
            Gender = 0,
            HitPoints = 8,
            MaxHitPoints = 8,
            CurrentHitPoints = 8
        };
        utc.FirstName.SetString(0, "Test");
        utc.ClassList.Add(new Utc.CreatureClass { Class = 0, ClassLevel = 1 });
        return utc;
    }

    #endregion

    #region BIC to UTC Conversion Tests

    [Fact]
    public void ToUtcFile_SetsPaletteIdToCustomCategory()
    {
        var bic = CreateTestBicFile();

        var utc = bic.ToUtcFile("test_creature");

        // PaletteID 1 = Custom category in creature palette
        Assert.Equal((byte)1, utc.PaletteID);
    }

    [Fact]
    public void ToUtcFile_SetsTemplateResRefFromParameter()
    {
        var bic = CreateTestBicFile();

        var utc = bic.ToUtcFile("my_test_npc");

        Assert.Equal("my_test_npc", utc.TemplateResRef);
    }

    [Fact]
    public void ToUtcFile_TruncatesLongTemplateResRef()
    {
        var bic = CreateTestBicFile();

        var utc = bic.ToUtcFile("very_long_creature_name_that_exceeds_limit");

        // TemplateResRef is limited to 16 characters
        Assert.Equal(16, utc.TemplateResRef.Length);
        Assert.Equal("very_long_creatu", utc.TemplateResRef);
    }

    [Fact]
    public void ToUtcFile_SetsIsPcFalse()
    {
        var bic = CreateTestBicFile();
        Assert.True(bic.IsPC);

        var utc = bic.ToUtcFile();

        Assert.False(utc.IsPC);
    }

    [Fact]
    public void ToUtcFile_SetsDefaultScripts()
    {
        var bic = CreateTestBicFile();

        var utc = bic.ToUtcFile();

        // Should have default NWN OC scripts set
        Assert.Equal("nw_c2_default5", utc.ScriptAttacked);
        Assert.Equal("nw_c2_default6", utc.ScriptDamaged);
        Assert.Equal("nw_c2_default7", utc.ScriptDeath);
        Assert.Equal("nw_c2_default1", utc.ScriptHeartbeat);
    }

    [Fact]
    public void ToUtcFile_CopiesAbilityScores()
    {
        var bic = CreateTestBicFile();
        bic.Str = 18;
        bic.Dex = 16;
        bic.Con = 14;
        bic.Int = 12;
        bic.Wis = 10;
        bic.Cha = 8;

        var utc = bic.ToUtcFile();

        Assert.Equal(18, utc.Str);
        Assert.Equal(16, utc.Dex);
        Assert.Equal(14, utc.Con);
        Assert.Equal(12, utc.Int);
        Assert.Equal(10, utc.Wis);
        Assert.Equal(8, utc.Cha);
    }

    [Fact]
    public void ToUtcFile_PreservesPortraitString()
    {
        // BIC files typically have PortraitId=0 and use Portrait string
        // The Portrait string should be preserved during conversion
        var bic = CreateTestBicFile();
        bic.PortraitId = 0;
        bic.Portrait = "po_hu_m_01_";

        var utc = bic.ToUtcFile();

        // Portrait string should be preserved - this is the actual character portrait
        Assert.Equal("po_hu_m_01_", utc.Portrait);
        // PortraitId stays 0 when Portrait string is set
        Assert.Equal(0, utc.PortraitId);
    }

    [Fact]
    public void ToUtcFile_SetsDefaultPortraitWhenBothEmpty()
    {
        // If both PortraitId=0 and Portrait is empty, set a default
        var bic = CreateTestBicFile();
        bic.PortraitId = 0;
        bic.Portrait = "";

        var utc = bic.ToUtcFile();

        // Should set a default portrait string
        Assert.Equal("po_hu_m_99_", utc.Portrait);
    }

    [Fact]
    public void ToUtcFile_PreservesNonZeroPortraitId()
    {
        var bic = CreateTestBicFile();
        bic.PortraitId = 42;

        var utc = bic.ToUtcFile();

        Assert.Equal(42, utc.PortraitId);
    }

    private static BicFile CreateTestBicFile()
    {
        var bic = new BicFile
        {
            Str = 10,
            Dex = 10,
            Con = 10,
            Int = 10,
            Wis = 10,
            Cha = 10,
            Race = 6, // Human
            Gender = 0,
            HitPoints = 8,
            MaxHitPoints = 8,
            CurrentHitPoints = 8,
            Age = 25,
            Experience = 0,
            Gold = 0
        };
        bic.FirstName.SetString(0, "TestPC");
        bic.ClassList.Add(new Utc.CreatureClass { Class = 0, ClassLevel = 1 });
        return bic;
    }

    #endregion

    #region Name Comparison Tests

    [Fact]
    public void CompareNames_QTestVsDana_ShowsNameStructureDifference()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var qtestPath = Path.Combine(docs, "Neverwinter Nights", "localvault", "qtest.bic");
        var danaPath = Path.Combine(docs, "Neverwinter Nights", "localvault", "dana.bic");

        if (!File.Exists(qtestPath) || !File.Exists(danaPath))
        {
            Console.WriteLine("Test files not found, skipping");
            return;
        }

        var qtest = BicReader.Read(qtestPath);
        var dana = BicReader.Read(danaPath);

        Console.WriteLine("=== QTEST.BIC (game-created) ===");
        Console.WriteLine($"FirstName StrRef: {qtest.FirstName.StrRef}");
        Console.WriteLine($"FirstName SubStringCount: {qtest.FirstName.SubStringCount}");
        Console.WriteLine($"FirstName LocalizedStrings count: {qtest.FirstName.LocalizedStrings.Count}");
        foreach (var kvp in qtest.FirstName.LocalizedStrings)
            Console.WriteLine($"  LangID [{kvp.Key}] (0x{kvp.Key:X8}): \"{kvp.Value}\"");

        Console.WriteLine($"LastName StrRef: {qtest.LastName.StrRef}");
        Console.WriteLine($"LastName SubStringCount: {qtest.LastName.SubStringCount}");
        Console.WriteLine($"LastName LocalizedStrings count: {qtest.LastName.LocalizedStrings.Count}");
        foreach (var kvp in qtest.LastName.LocalizedStrings)
            Console.WriteLine($"  LangID [{kvp.Key}] (0x{kvp.Key:X8}): \"{kvp.Value}\"");

        Console.WriteLine("\n=== DANA.BIC (Quartermaster-created) ===");
        Console.WriteLine($"FirstName StrRef: {dana.FirstName.StrRef}");
        Console.WriteLine($"FirstName SubStringCount: {dana.FirstName.SubStringCount}");
        Console.WriteLine($"FirstName LocalizedStrings count: {dana.FirstName.LocalizedStrings.Count}");
        foreach (var kvp in dana.FirstName.LocalizedStrings)
            Console.WriteLine($"  LangID [{kvp.Key}] (0x{kvp.Key:X8}): \"{kvp.Value}\"");

        Console.WriteLine($"LastName StrRef: {dana.LastName.StrRef}");
        Console.WriteLine($"LastName SubStringCount: {dana.LastName.SubStringCount}");
        Console.WriteLine($"LastName LocalizedStrings count: {dana.LastName.LocalizedStrings.Count}");
        foreach (var kvp in dana.LastName.LocalizedStrings)
            Console.WriteLine($"  LangID [{kvp.Key}] (0x{kvp.Key:X8}): \"{kvp.Value}\"");
    }

    #endregion
}
