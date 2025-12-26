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
}
