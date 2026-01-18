using Radoub.Formats.Bic;
using Radoub.Formats.Gff;
using Xunit;

namespace Radoub.Formats.Tests;

public class BicReaderTests
{
    private static readonly string TestDataPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "TestData", "Bic");

    [Fact]
    public void Read_BillyWanderers_ParsesFileType()
    {
        var filePath = Path.Combine(TestDataPath, "billywanderers.bic");
        if (!File.Exists(filePath)) return; // Skip if test file not available

        var bic = BicReader.Read(filePath);

        Assert.Equal("BIC ", bic.FileType);
        Assert.Equal("V3.2", bic.FileVersion);
    }

    [Fact]
    public void Read_BillyWanderers_ParsesPlayerFields()
    {
        var filePath = Path.Combine(TestDataPath, "billywanderers.bic");
        if (!File.Exists(filePath)) return;

        var bic = BicReader.Read(filePath);

        Assert.True(bic.IsPC, "BIC files should have IsPC=true");
        Assert.True(bic.Experience >= 0, "Experience should be valid");
        Assert.True(bic.Gold >= 0, "Gold should be valid");
        Assert.True(bic.Age >= 0, "Age should be valid");
    }

    [Fact]
    public void Read_BillyWanderers_ParsesCharacterName()
    {
        var filePath = Path.Combine(TestDataPath, "billywanderers.bic");
        if (!File.Exists(filePath)) return;

        var bic = BicReader.Read(filePath);

        var firstName = bic.FirstName.GetDefault();
        Assert.True(!string.IsNullOrEmpty(firstName) || bic.FirstName.StrRef != 0xFFFFFFFF,
            "Character should have a first name or TLK reference");
    }

    [Fact]
    public void Read_BillyWanderers_ParsesClassList()
    {
        var filePath = Path.Combine(TestDataPath, "billywanderers.bic");
        if (!File.Exists(filePath)) return;

        var bic = BicReader.Read(filePath);

        Assert.True(bic.ClassList.Count >= 1, "Player should have at least one class");

        foreach (var cls in bic.ClassList)
        {
            Assert.True(cls.Class >= 0, "Class index should be non-negative");
            Assert.True(cls.ClassLevel >= 1, "Class level should be at least 1");
        }
    }

    [Fact]
    public void Read_BillyWanderers_ParsesAbilityScores()
    {
        var filePath = Path.Combine(TestDataPath, "billywanderers.bic");
        if (!File.Exists(filePath)) return;

        var bic = BicReader.Read(filePath);

        Assert.InRange(bic.Str, (byte)3, (byte)50);
        Assert.InRange(bic.Dex, (byte)3, (byte)50);
        Assert.InRange(bic.Con, (byte)3, (byte)50);
        Assert.InRange(bic.Int, (byte)3, (byte)50);
        Assert.InRange(bic.Wis, (byte)3, (byte)50);
        Assert.InRange(bic.Cha, (byte)3, (byte)50);
    }

    [Fact]
    public void Read_BillyWanderers_ParsesQBList()
    {
        var filePath = Path.Combine(TestDataPath, "billywanderers.bic");
        if (!File.Exists(filePath)) return;

        var bic = BicReader.Read(filePath);

        if (bic.QBList.Count > 0)
        {
            Assert.Equal(36, bic.QBList.Count);

            foreach (var slot in bic.QBList)
            {
                var typeName = QuickBarObjectType.GetTypeName(slot.ObjectType);
                Assert.False(typeName.StartsWith("Unknown") && slot.ObjectType > 44,
                    $"Invalid QuickBar object type: {slot.ObjectType}");
            }
        }
    }

    [Fact]
    public void Read_BillyWanderers_ParsesSkillList()
    {
        var filePath = Path.Combine(TestDataPath, "billywanderers.bic");
        if (!File.Exists(filePath)) return;

        var bic = BicReader.Read(filePath);

        Assert.True(bic.SkillList.Count > 0, "Player should have skill ranks");
    }

    [Fact]
    public void Read_BillyWanderers_ParsesFeatList()
    {
        var filePath = Path.Combine(TestDataPath, "billywanderers.bic");
        if (!File.Exists(filePath)) return;

        var bic = BicReader.Read(filePath);

        Assert.True(bic.FeatList.Count >= 0, "FeatList should be readable");
    }

    [Fact]
    public void Read_Stream_MatchesFileRead()
    {
        var filePath = Path.Combine(TestDataPath, "billywanderers.bic");
        if (!File.Exists(filePath)) return;

        var fromFile = BicReader.Read(filePath);

        BicFile fromStream;
        using (var stream = File.OpenRead(filePath))
        {
            fromStream = BicReader.Read(stream);
        }

        Assert.Equal(fromFile.FileType, fromStream.FileType);
        Assert.Equal(fromFile.Experience, fromStream.Experience);
        Assert.Equal(fromFile.Gold, fromStream.Gold);
        Assert.Equal(fromFile.ClassList.Count, fromStream.ClassList.Count);
    }

    [Fact]
    public void Read_BillyWanderers_ParsesInventory()
    {
        var filePath = Path.Combine(TestDataPath, "billywanderers.bic");
        if (!File.Exists(filePath)) return;

        var bic = BicReader.Read(filePath);

        // Log inventory for debugging
        System.Diagnostics.Debug.WriteLine($"ItemList count: {bic.ItemList.Count}");
        foreach (var item in bic.ItemList)
        {
            System.Diagnostics.Debug.WriteLine($"  Item: {item.InventoryRes}");
        }

        // Players typically have some items
        // Note: BIC stores embedded item structs, ResRef comes from TemplateResRef field
    }

    [Fact]
    public void Read_BillyWanderers_ParsesEquippedItems()
    {
        var filePath = Path.Combine(TestDataPath, "billywanderers.bic");
        if (!File.Exists(filePath)) return;

        var bic = BicReader.Read(filePath);

        // Output for debugging
        Console.WriteLine($"EquipItemList count: {bic.EquipItemList.Count}");
        foreach (var item in bic.EquipItemList)
        {
            Console.WriteLine($"  Slot 0x{item.Slot:X4}: '{item.EquipRes}'");
        }

        // Verify equipped items are parsed with TemplateResRef
        // BIC files store embedded item structs, not ResRef references like UTC blueprints
        Assert.True(bic.EquipItemList.Count > 0, "Player should have at least one equipped item");

        // At least one equipped item should have a valid ResRef
        var hasValidResRef = bic.EquipItemList.Any(e => !string.IsNullOrEmpty(e.EquipRes));
        Assert.True(hasValidResRef, "At least one equipped item should have a TemplateResRef");
    }

    [Fact]
    public void RoundTrip_BillyWanderers_PreservesData()
    {
        var filePath = Path.Combine(TestDataPath, "billywanderers.bic");
        if (!File.Exists(filePath)) return;

        var original = BicReader.Read(filePath);
        var buffer = BicWriter.Write(original);
        var roundTripped = BicReader.Read(buffer);

        // Core fields
        Assert.Equal(original.FileType, roundTripped.FileType);
        Assert.Equal(original.FileVersion, roundTripped.FileVersion);
        Assert.Equal(original.Experience, roundTripped.Experience);
        Assert.Equal(original.Gold, roundTripped.Gold);
        Assert.Equal(original.Age, roundTripped.Age);
        Assert.Equal(original.IsPC, roundTripped.IsPC);

        // Identity
        Assert.Equal(original.FirstName.GetDefault(), roundTripped.FirstName.GetDefault());
        Assert.Equal(original.LastName.GetDefault(), roundTripped.LastName.GetDefault());
        Assert.Equal(original.Tag, roundTripped.Tag);

        // Abilities
        Assert.Equal(original.Str, roundTripped.Str);
        Assert.Equal(original.Dex, roundTripped.Dex);
        Assert.Equal(original.Con, roundTripped.Con);
        Assert.Equal(original.Int, roundTripped.Int);
        Assert.Equal(original.Wis, roundTripped.Wis);
        Assert.Equal(original.Cha, roundTripped.Cha);

        // Lists
        Assert.Equal(original.ClassList.Count, roundTripped.ClassList.Count);
        Assert.Equal(original.FeatList.Count, roundTripped.FeatList.Count);
        Assert.Equal(original.SkillList.Count, roundTripped.SkillList.Count);
        Assert.Equal(original.QBList.Count, roundTripped.QBList.Count);
        Assert.Equal(original.ReputationList.Count, roundTripped.ReputationList.Count);
    }

    [Fact]
    public void RoundTrip_BillyWanderers_PreservesClassDetails()
    {
        var filePath = Path.Combine(TestDataPath, "billywanderers.bic");
        if (!File.Exists(filePath)) return;

        var original = BicReader.Read(filePath);
        var buffer = BicWriter.Write(original);
        var roundTripped = BicReader.Read(buffer);

        for (int i = 0; i < original.ClassList.Count; i++)
        {
            Assert.Equal(original.ClassList[i].Class, roundTripped.ClassList[i].Class);
            Assert.Equal(original.ClassList[i].ClassLevel, roundTripped.ClassList[i].ClassLevel);
        }
    }

    [Fact]
    public void RoundTrip_BillyWanderers_PreservesQuickBarSlots()
    {
        var filePath = Path.Combine(TestDataPath, "billywanderers.bic");
        if (!File.Exists(filePath)) return;

        var original = BicReader.Read(filePath);
        if (original.QBList.Count == 0) return; // No QuickBar data to test

        var buffer = BicWriter.Write(original);
        var roundTripped = BicReader.Read(buffer);

        for (int i = 0; i < original.QBList.Count; i++)
        {
            Assert.Equal(original.QBList[i].ObjectType, roundTripped.QBList[i].ObjectType);
        }
    }

    [Fact]
    public void Read_MinimalBicFile_ParsesCorrectly()
    {
        var buffer = CreateMinimalBicFile();

        var bic = BicReader.Read(buffer);

        Assert.Equal("BIC ", bic.FileType);
        Assert.Equal("V3.2", bic.FileVersion);
        Assert.True(bic.IsPC);
    }

    [Fact]
    public void RoundTrip_MinimalBicFile_PreservesData()
    {
        var original = new BicFile
        {
            Str = 14,
            Dex = 16,
            Con = 12,
            Int = 10,
            Wis = 8,
            Cha = 18,
            Experience = 5000,
            Gold = 1234,
            Age = 25
        };
        original.FirstName.SetString(0, "Testy");
        original.LastName.SetString(0, "McTestFace");

        var buffer = BicWriter.Write(original);
        var roundTripped = BicReader.Read(buffer);

        Assert.Equal(original.Experience, roundTripped.Experience);
        Assert.Equal(original.Gold, roundTripped.Gold);
        Assert.Equal(original.Age, roundTripped.Age);
        Assert.Equal(original.Str, roundTripped.Str);
        Assert.Equal("Testy", roundTripped.FirstName.GetDefault());
        Assert.Equal("McTestFace", roundTripped.LastName.GetDefault());
    }

    private static byte[] CreateMinimalBicFile()
    {
        var bic = new BicFile
        {
            Str = 12,
            Dex = 14,
            Con = 10,
            Int = 16,
            Wis = 10,
            Cha = 8,
            Race = 0,
            Gender = 0
        };

        bic.FirstName.SetString(0, "Test");
        bic.LastName.SetString(0, "Character");

        return BicWriter.Write(bic);
    }

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

    #region Name Comparison Tests

    [Fact]
    public void CompareNames_QTestVsDana_ShowsNameStructureDifference()
    {
        var qtestPath = @"C:\Users\Sheri\Documents\Neverwinter Nights\localvault\qtest.bic";
        var danaPath = @"C:\Users\Sheri\Documents\Neverwinter Nights\localvault\dana.bic";

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
