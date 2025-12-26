using Radoub.Formats.Gff;
using Radoub.Formats.Jrl;

namespace Manifest.Tests;

/// <summary>
/// Generates test JRL files for testing Manifest.
/// Run once to create test data files.
/// </summary>
public static class TestDataGenerator
{
    /// <summary>
    /// Create a minimal empty JRL for testing basic operations.
    /// </summary>
    public static JrlFile CreateEmptyJrl()
    {
        return new JrlFile();
    }

    /// <summary>
    /// Create a JRL with one category and no entries.
    /// </summary>
    public static JrlFile CreateSingleCategoryJrl()
    {
        var jrl = new JrlFile();
        var category = new JournalCategory
        {
            Tag = "test_quest",
            Priority = 2, // Medium
            XP = 0
        };
        category.Name.SetString(0, "Test Quest");
        jrl.Categories.Add(category);
        return jrl;
    }

    /// <summary>
    /// Create a JRL with multiple categories and entries for comprehensive testing.
    /// </summary>
    public static JrlFile CreateFullTestJrl()
    {
        var jrl = new JrlFile();

        // Main quest with multiple entries
        var mainQuest = new JournalCategory
        {
            Tag = "main_quest",
            Priority = 0, // Highest
            XP = 1000,
            Comment = "Main storyline quest"
        };
        mainQuest.Name.SetString(0, "The Main Quest");

        mainQuest.Entries.Add(new JournalEntry
        {
            ID = 100,
            End = false,
            Text = CreateLocString("Speak with the wizard in the tower.")
        });
        mainQuest.Entries.Add(new JournalEntry
        {
            ID = 200,
            End = false,
            Text = CreateLocString("Find the ancient artifact in the dungeon.")
        });
        mainQuest.Entries.Add(new JournalEntry
        {
            ID = 300,
            End = true,
            Text = CreateLocString("Quest completed! The artifact has been secured.")
        });

        jrl.Categories.Add(mainQuest);

        // Side quest
        var sideQuest = new JournalCategory
        {
            Tag = "side_gather",
            Priority = 3, // Low
            XP = 100
        };
        sideQuest.Name.SetString(0, "Gather Herbs");

        sideQuest.Entries.Add(new JournalEntry
        {
            ID = 100,
            End = false,
            Text = CreateLocString("Collect 5 healing herbs from the forest.")
        });
        sideQuest.Entries.Add(new JournalEntry
        {
            ID = 200,
            End = true,
            Text = CreateLocString("Herbs collected and delivered.")
        });

        jrl.Categories.Add(sideQuest);

        return jrl;
    }

    private static CExoLocString CreateLocString(string text)
    {
        var loc = new CExoLocString();
        loc.SetString(0, text);
        return loc;
    }

    /// <summary>
    /// Write test JRL files to the specified directory.
    /// </summary>
    public static void GenerateTestFiles(string directory)
    {
        Directory.CreateDirectory(directory);

        JrlWriter.Write(CreateEmptyJrl(), Path.Combine(directory, "empty.jrl"));
        JrlWriter.Write(CreateSingleCategoryJrl(), Path.Combine(directory, "single_category.jrl"));
        JrlWriter.Write(CreateFullTestJrl(), Path.Combine(directory, "full_test.jrl"));
    }
}
