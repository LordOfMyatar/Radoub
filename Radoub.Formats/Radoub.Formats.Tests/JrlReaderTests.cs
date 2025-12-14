using System.Text;
using Radoub.Formats.Gff;
using Radoub.Formats.Jrl;
using Xunit;

namespace Radoub.Formats.Tests;

public class JrlReaderTests
{
    [Fact]
    public void Read_MinimalJrl_ParsesCorrectly()
    {
        var buffer = CreateMinimalJrl();

        var result = JrlReader.Read(buffer);

        Assert.Equal("JRL ", result.FileType);
        Assert.Equal("V3.2", result.FileVersion);
        Assert.NotNull(result.Categories);
    }

    [Fact]
    public void Read_JrlWithCategory_ParsesCategory()
    {
        var buffer = CreateJrlWithCategory("test_quest", "Test Quest Name");

        var result = JrlReader.Read(buffer);

        Assert.Single(result.Categories);
        var category = result.Categories[0];
        Assert.Equal("test_quest", category.Tag);
        Assert.Equal("Test Quest Name", category.Name.GetDefault());
    }

    [Fact]
    public void Read_JrlWithEntry_ParsesEntry()
    {
        var buffer = CreateJrlWithEntry("quest1", 1, "First journal entry", false);

        var result = JrlReader.Read(buffer);

        Assert.Single(result.Categories);
        Assert.Single(result.Categories[0].Entries);
        var entry = result.Categories[0].Entries[0];
        Assert.Equal(1u, entry.ID);
        Assert.Equal("First journal entry", entry.Text.GetDefault());
        Assert.False(entry.End);
    }

    [Fact]
    public void Read_JrlWithEndEntry_ParsesEndFlag()
    {
        var buffer = CreateJrlWithEntry("quest1", 10, "Quest complete!", true);

        var result = JrlReader.Read(buffer);

        var entry = result.Categories[0].Entries[0];
        Assert.True(entry.End);
    }

    [Fact]
    public void Read_JrlWithMultipleCategories_ParsesAll()
    {
        var buffer = CreateJrlWithMultipleCategories();

        var result = JrlReader.Read(buffer);

        Assert.Equal(2, result.Categories.Count);
        Assert.Equal("main_quest", result.Categories[0].Tag);
        Assert.Equal("side_quest", result.Categories[1].Tag);
    }

    [Fact]
    public void Read_JrlWithPriorityAndXP_ParsesValues()
    {
        var buffer = CreateJrlWithPriorityAndXP("quest1", 5, 100);

        var result = JrlReader.Read(buffer);

        var category = result.Categories[0];
        Assert.Equal(5u, category.Priority);
        Assert.Equal(100u, category.XP);
    }

    [Fact]
    public void FindCategory_ExistingTag_ReturnsCategory()
    {
        var jrl = new JrlFile();
        jrl.Categories.Add(new JournalCategory { Tag = "test_quest" });

        var found = jrl.FindCategory("test_quest");

        Assert.NotNull(found);
        Assert.Equal("test_quest", found.Tag);
    }

    [Fact]
    public void FindCategory_CaseInsensitive_ReturnsCategory()
    {
        var jrl = new JrlFile();
        jrl.Categories.Add(new JournalCategory { Tag = "Test_Quest" });

        var found = jrl.FindCategory("test_quest");

        Assert.NotNull(found);
    }

    [Fact]
    public void FindCategory_NotFound_ReturnsNull()
    {
        var jrl = new JrlFile();

        var found = jrl.FindCategory("nonexistent");

        Assert.Null(found);
    }

    [Fact]
    public void FindEntry_ExistingId_ReturnsEntry()
    {
        var category = new JournalCategory { Tag = "quest1" };
        category.Entries.Add(new JournalEntry { ID = 5 });

        var found = category.FindEntry(5);

        Assert.NotNull(found);
        Assert.Equal(5u, found.ID);
    }

    [Fact]
    public void JrlLocString_GetDefault_ReturnsEnglish()
    {
        var locString = new JrlLocString();
        locString.Strings[0] = "English";
        locString.Strings[2] = "French";

        Assert.Equal("English", locString.GetDefault());
    }

    [Fact]
    public void JrlLocString_GetDefault_FallsBackToFirst()
    {
        var locString = new JrlLocString();
        locString.Strings[2] = "French";

        Assert.Equal("French", locString.GetDefault());
    }

    [Fact]
    public void RoundTrip_SimpleJrl_PreservesData()
    {
        var original = new JrlFile();
        var category = new JournalCategory
        {
            Tag = "test_quest",
            Priority = 1,
            XP = 50
        };
        category.Name.SetString(0, "Test Quest");
        category.Entries.Add(new JournalEntry
        {
            ID = 1,
            End = false
        });
        category.Entries[0].Text.SetString(0, "Entry text");
        original.Categories.Add(category);

        var buffer = JrlWriter.Write(original);
        var result = JrlReader.Read(buffer);

        Assert.Single(result.Categories);
        Assert.Equal("test_quest", result.Categories[0].Tag);
        Assert.Equal("Test Quest", result.Categories[0].Name.GetDefault());
        Assert.Equal(1u, result.Categories[0].Priority);
        Assert.Equal(50u, result.Categories[0].XP);
        Assert.Single(result.Categories[0].Entries);
        Assert.Equal(1u, result.Categories[0].Entries[0].ID);
        Assert.Equal("Entry text", result.Categories[0].Entries[0].Text.GetDefault());
    }

    [Fact]
    public void RoundTrip_MultipleCategories_PreservesAll()
    {
        var original = new JrlFile();
        original.Categories.Add(new JournalCategory { Tag = "quest1", Priority = 1, XP = 100 });
        original.Categories.Add(new JournalCategory { Tag = "quest2", Priority = 2, XP = 200 });

        var buffer = JrlWriter.Write(original);
        var result = JrlReader.Read(buffer);

        Assert.Equal(2, result.Categories.Count);
        Assert.Equal("quest1", result.Categories[0].Tag);
        Assert.Equal("quest2", result.Categories[1].Tag);
        Assert.Equal(100u, result.Categories[0].XP);
        Assert.Equal(200u, result.Categories[1].XP);
    }

    [Fact]
    public void RoundTrip_EntryEndFlag_PreservesValue()
    {
        var original = new JrlFile();
        var category = new JournalCategory { Tag = "quest1" };
        category.Entries.Add(new JournalEntry { ID = 1, End = false });
        category.Entries.Add(new JournalEntry { ID = 2, End = true });
        original.Categories.Add(category);

        var buffer = JrlWriter.Write(original);
        var result = JrlReader.Read(buffer);

        Assert.False(result.Categories[0].Entries[0].End);
        Assert.True(result.Categories[0].Entries[1].End);
    }

    [Fact]
    public void Read_InvalidFileType_ThrowsException()
    {
        var buffer = CreateGffWithFileType("DLG ");

        Assert.Throws<InvalidDataException>(() => JrlReader.Read(buffer));
    }

    [Fact(Skip = "Manual test - requires specific NWN module file")]
    public void Read_XP2Chapter2ModuleJrl_DumpsContents()
    {
        // Manual test to analyze real JRL file structure
        var filePath = @"C:\Users\Sheri\Documents\Neverwinter Nights\modules\XP2_Chapter2\module.jrl";

        if (!File.Exists(filePath))
        {
            Assert.Fail($"File not found: {filePath}");
            return;
        }

        var jrl = JrlReader.Read(filePath);

        // Dump basic info
        Console.WriteLine($"File Type: {jrl.FileType}");
        Console.WriteLine($"File Version: {jrl.FileVersion}");
        Console.WriteLine($"Categories: {jrl.Categories.Count}");
        Console.WriteLine();

        // Analyze StrRef usage and structure
        var strRefUsage = new Dictionary<uint, List<string>>();

        foreach (var category in jrl.Categories)
        {
            Console.WriteLine($"=== Category: {category.Tag} ===");
            Console.WriteLine($"  Priority: {category.Priority}");
            Console.WriteLine($"  XP: {category.XP}");
            Console.WriteLine($"  Comment: {category.Comment}");
            Console.WriteLine($"  Picture: {category.Picture}");

            // Name
            Console.WriteLine($"  Name StrRef: {category.Name.StrRef:X8}");
            if (category.Name.StrRef != 0xFFFFFFFF)
            {
                if (!strRefUsage.ContainsKey(category.Name.StrRef))
                    strRefUsage[category.Name.StrRef] = new List<string>();
                strRefUsage[category.Name.StrRef].Add($"Category:{category.Tag}:Name");
            }

            if (category.Name.Strings.Any())
            {
                Console.WriteLine($"  Name Strings: {category.Name.Strings.Count}");
                foreach (var kvp in category.Name.Strings)
                {
                    Console.WriteLine($"    Lang {kvp.Key}: {kvp.Value.Substring(0, Math.Min(50, kvp.Value.Length))}...");
                }
            }

            Console.WriteLine($"  Entries: {category.Entries.Count}");

            // Entries
            foreach (var entry in category.Entries.Take(3)) // Show first 3 entries
            {
                Console.WriteLine($"    Entry ID: {entry.ID}");
                Console.WriteLine($"      End: {entry.End}");
                Console.WriteLine($"      Text StrRef: {entry.Text.StrRef:X8}");

                if (entry.Text.StrRef != 0xFFFFFFFF)
                {
                    if (!strRefUsage.ContainsKey(entry.Text.StrRef))
                        strRefUsage[entry.Text.StrRef] = new List<string>();
                    strRefUsage[entry.Text.StrRef].Add($"Category:{category.Tag}:Entry:{entry.ID}");
                }

                if (entry.Text.Strings.Any())
                {
                    Console.WriteLine($"      Text Strings: {entry.Text.Strings.Count}");
                    foreach (var kvp in entry.Text.Strings)
                    {
                        var preview = kvp.Value.Substring(0, Math.Min(60, kvp.Value.Length));
                        Console.WriteLine($"        Lang {kvp.Key}: {preview}...");
                    }
                }
            }

            if (category.Entries.Count > 3)
            {
                Console.WriteLine($"    ... and {category.Entries.Count - 3} more entries");
            }

            Console.WriteLine();
        }

        // Summary of StrRef usage
        Console.WriteLine("=== StrRef Summary ===");
        Console.WriteLine($"Unique StrRefs used: {strRefUsage.Count}");
        foreach (var kvp in strRefUsage.Take(10))
        {
            Console.WriteLine($"  StrRef {kvp.Key:X8}: used {kvp.Value.Count} times");
            foreach (var usage in kvp.Value)
            {
                Console.WriteLine($"    - {usage}");
            }
        }

        // Sample data structure
        if (jrl.Categories.Any())
        {
            var sampleCat = jrl.Categories[0];
            Console.WriteLine();
            Console.WriteLine("=== Sample Data Structure ===");
            Console.WriteLine($"Category Tag: {sampleCat.Tag}");
            Console.WriteLine($"  Name.StrRef: {sampleCat.Name.StrRef}");
            Console.WriteLine($"  Name.Strings.Count: {sampleCat.Name.Strings.Count}");
            Console.WriteLine($"  Entries.Count: {sampleCat.Entries.Count}");

            if (sampleCat.Entries.Any())
            {
                var sampleEntry = sampleCat.Entries[0];
                Console.WriteLine($"  Entry[0].ID: {sampleEntry.ID}");
                Console.WriteLine($"  Entry[0].Text.StrRef: {sampleEntry.Text.StrRef}");
                Console.WriteLine($"  Entry[0].Text.Strings.Count: {sampleEntry.Text.Strings.Count}");
                Console.WriteLine($"  Entry[0].End: {sampleEntry.End}");
            }
        }
    }

    #region Test Helpers

    private static byte[] CreateMinimalJrl()
    {
        // Create via GffWriter with JRL file type
        var gff = new GffFile
        {
            FileType = "JRL ",
            FileVersion = "V3.2"
        };
        gff.RootStruct = new GffStruct { Type = 0xFFFFFFFF };

        // Empty Categories list
        gff.RootStruct.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "Categories",
            Value = new GffList()
        });

        return GffWriter.Write(gff);
    }

    private static byte[] CreateJrlWithCategory(string tag, string name)
    {
        var gff = new GffFile
        {
            FileType = "JRL ",
            FileVersion = "V3.2"
        };
        gff.RootStruct = new GffStruct { Type = 0xFFFFFFFF };

        var categoriesList = new GffList();
        var categoryStruct = new GffStruct { Type = 0 };

        // Tag
        categoryStruct.Fields.Add(new GffField
        {
            Type = GffField.CExoString,
            Label = "Tag",
            Value = tag
        });

        // Name
        var nameLocString = new CExoLocString();
        nameLocString.LocalizedStrings[0] = name;
        categoryStruct.Fields.Add(new GffField
        {
            Type = GffField.CExoLocString,
            Label = "Name",
            Value = nameLocString
        });

        // Priority
        categoryStruct.Fields.Add(new GffField
        {
            Type = GffField.DWORD,
            Label = "Priority",
            Value = 0u
        });

        // XP
        categoryStruct.Fields.Add(new GffField
        {
            Type = GffField.DWORD,
            Label = "XP",
            Value = 0u
        });

        // Empty EntryList
        categoryStruct.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "EntryList",
            Value = new GffList()
        });

        categoriesList.Elements.Add(categoryStruct);
        categoriesList.Count = 1;

        gff.RootStruct.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "Categories",
            Value = categoriesList
        });

        return GffWriter.Write(gff);
    }

    private static byte[] CreateJrlWithEntry(string tag, uint entryId, string entryText, bool isEnd)
    {
        var gff = new GffFile
        {
            FileType = "JRL ",
            FileVersion = "V3.2"
        };
        gff.RootStruct = new GffStruct { Type = 0xFFFFFFFF };

        var categoriesList = new GffList();
        var categoryStruct = new GffStruct { Type = 0 };

        // Tag
        categoryStruct.Fields.Add(new GffField
        {
            Type = GffField.CExoString,
            Label = "Tag",
            Value = tag
        });

        // Name
        categoryStruct.Fields.Add(new GffField
        {
            Type = GffField.CExoLocString,
            Label = "Name",
            Value = new CExoLocString()
        });

        // Priority
        categoryStruct.Fields.Add(new GffField
        {
            Type = GffField.DWORD,
            Label = "Priority",
            Value = 0u
        });

        // XP
        categoryStruct.Fields.Add(new GffField
        {
            Type = GffField.DWORD,
            Label = "XP",
            Value = 0u
        });

        // EntryList with one entry
        var entryList = new GffList();
        var entryStruct = new GffStruct { Type = 0 };

        entryStruct.Fields.Add(new GffField
        {
            Type = GffField.DWORD,
            Label = "ID",
            Value = entryId
        });

        var textLocString = new CExoLocString();
        textLocString.LocalizedStrings[0] = entryText;
        entryStruct.Fields.Add(new GffField
        {
            Type = GffField.CExoLocString,
            Label = "Text",
            Value = textLocString
        });

        entryStruct.Fields.Add(new GffField
        {
            Type = GffField.WORD,
            Label = "End",
            Value = (ushort)(isEnd ? 1 : 0)
        });

        entryList.Elements.Add(entryStruct);
        entryList.Count = 1;

        categoryStruct.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "EntryList",
            Value = entryList
        });

        categoriesList.Elements.Add(categoryStruct);
        categoriesList.Count = 1;

        gff.RootStruct.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "Categories",
            Value = categoriesList
        });

        return GffWriter.Write(gff);
    }

    private static byte[] CreateJrlWithMultipleCategories()
    {
        var gff = new GffFile
        {
            FileType = "JRL ",
            FileVersion = "V3.2"
        };
        gff.RootStruct = new GffStruct { Type = 0xFFFFFFFF };

        var categoriesList = new GffList();

        // First category
        var cat1 = new GffStruct { Type = 0 };
        cat1.Fields.Add(new GffField { Type = GffField.CExoString, Label = "Tag", Value = "main_quest" });
        cat1.Fields.Add(new GffField { Type = GffField.CExoLocString, Label = "Name", Value = new CExoLocString() });
        cat1.Fields.Add(new GffField { Type = GffField.DWORD, Label = "Priority", Value = 0u });
        cat1.Fields.Add(new GffField { Type = GffField.DWORD, Label = "XP", Value = 0u });
        cat1.Fields.Add(new GffField { Type = GffField.List, Label = "EntryList", Value = new GffList() });
        categoriesList.Elements.Add(cat1);

        // Second category
        var cat2 = new GffStruct { Type = 0 };
        cat2.Fields.Add(new GffField { Type = GffField.CExoString, Label = "Tag", Value = "side_quest" });
        cat2.Fields.Add(new GffField { Type = GffField.CExoLocString, Label = "Name", Value = new CExoLocString() });
        cat2.Fields.Add(new GffField { Type = GffField.DWORD, Label = "Priority", Value = 0u });
        cat2.Fields.Add(new GffField { Type = GffField.DWORD, Label = "XP", Value = 0u });
        cat2.Fields.Add(new GffField { Type = GffField.List, Label = "EntryList", Value = new GffList() });
        categoriesList.Elements.Add(cat2);

        categoriesList.Count = 2;

        gff.RootStruct.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "Categories",
            Value = categoriesList
        });

        return GffWriter.Write(gff);
    }

    private static byte[] CreateJrlWithPriorityAndXP(string tag, uint priority, uint xp)
    {
        var gff = new GffFile
        {
            FileType = "JRL ",
            FileVersion = "V3.2"
        };
        gff.RootStruct = new GffStruct { Type = 0xFFFFFFFF };

        var categoriesList = new GffList();
        var categoryStruct = new GffStruct { Type = 0 };

        categoryStruct.Fields.Add(new GffField { Type = GffField.CExoString, Label = "Tag", Value = tag });
        categoryStruct.Fields.Add(new GffField { Type = GffField.CExoLocString, Label = "Name", Value = new CExoLocString() });
        categoryStruct.Fields.Add(new GffField { Type = GffField.DWORD, Label = "Priority", Value = priority });
        categoryStruct.Fields.Add(new GffField { Type = GffField.DWORD, Label = "XP", Value = xp });
        categoryStruct.Fields.Add(new GffField { Type = GffField.List, Label = "EntryList", Value = new GffList() });

        categoriesList.Elements.Add(categoryStruct);
        categoriesList.Count = 1;

        gff.RootStruct.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "Categories",
            Value = categoriesList
        });

        return GffWriter.Write(gff);
    }

    private static byte[] CreateGffWithFileType(string fileType)
    {
        var gff = new GffFile
        {
            FileType = fileType,
            FileVersion = "V3.2"
        };
        gff.RootStruct = new GffStruct { Type = 0xFFFFFFFF };
        gff.RootStruct.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "Categories",
            Value = new GffList()
        });

        return GffWriter.Write(gff);
    }

    #endregion
}
