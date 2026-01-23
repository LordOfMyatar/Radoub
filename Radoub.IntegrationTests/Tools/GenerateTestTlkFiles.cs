// This file contains a method to generate test TLK files for integration tests.
// Run this once to create the test data, or regenerate if needed.

using Radoub.Formats.Tlk;
using System.IO;

namespace Radoub.IntegrationTests.Tools;

/// <summary>
/// Utility to generate test TLK files for integration tests.
/// </summary>
public static class TlkTestDataGenerator
{
    /// <summary>
    /// Generate test dialog.tlk and custom.tlk files.
    /// </summary>
    /// <param name="testDataRoot">Path to TestData directory</param>
    public static void GenerateTestTlkFiles(string testDataRoot)
    {
        var gameDataPath = Path.Combine(testDataRoot, "GameRoot", "data");
        var userTlkPath = Path.Combine(testDataRoot, "UserRoot", "tlk");

        Directory.CreateDirectory(gameDataPath);
        Directory.CreateDirectory(userTlkPath);

        // Generate dialog.tlk (base game TLK)
        GenerateDialogTlk(Path.Combine(gameDataPath, "dialog.tlk"));

        // Generate custom.tlk (custom content TLK)
        GenerateCustomTlk(Path.Combine(userTlkPath, "custom.tlk"));
    }

    /// <summary>
    /// Generate a minimal dialog.tlk for testing.
    /// Contains common strings that might be referenced by 2DA files.
    /// </summary>
    private static void GenerateDialogTlk(string outputPath)
    {
        var tlk = new TlkFile
        {
            LanguageId = 0 // English
        };

        // Add entries for common StrRefs
        // These map to typical 2DA references
        AddEntries(tlk, new[]
        {
            // 0-9: Basic UI strings
            (0u, "Bad Strref"),
            (1u, "Unknown"),
            (2u, "None"),
            (3u, "Yes"),
            (4u, "No"),
            (5u, "OK"),
            (6u, "Cancel"),
            (7u, "Apply"),
            (8u, "Close"),
            (9u, "Help"),

            // 10-19: Common game terms
            (10u, "Human"),
            (11u, "Elf"),
            (12u, "Dwarf"),
            (13u, "Halfling"),
            (14u, "Half-Elf"),
            (15u, "Half-Orc"),
            (16u, "Gnome"),
            (17u, "Fighter"),
            (18u, "Wizard"),
            (19u, "Cleric"),

            // 20-29: Ability scores
            (20u, "Strength"),
            (21u, "Dexterity"),
            (22u, "Constitution"),
            (23u, "Intelligence"),
            (24u, "Wisdom"),
            (25u, "Charisma"),
            (26u, "Hit Points"),
            (27u, "Armor Class"),
            (28u, "Attack Bonus"),
            (29u, "Damage"),

            // 30-39: Skills
            (30u, "Animal Empathy"),
            (31u, "Concentration"),
            (32u, "Disable Trap"),
            (33u, "Discipline"),
            (34u, "Heal"),
            (35u, "Hide"),
            (36u, "Listen"),
            (37u, "Lore"),
            (38u, "Move Silently"),
            (39u, "Open Lock"),

            // 100-109: Journal test entries
            (100u, "Quest Started"),
            (101u, "Quest Updated"),
            (102u, "Quest Completed"),
            (103u, "Journal Entry 1"),
            (104u, "Journal Entry 2"),
            (105u, "Test Quest Name"),
            (106u, "Find the missing adventurer"),
            (107u, "Return to the village"),
            (108u, "The mystery is solved"),
            (109u, "Quest Failed"),

            // 200-209: Store test entries
            (200u, "General Store"),
            (201u, "Blacksmith"),
            (202u, "Alchemist"),
            (203u, "Temple Store"),
            (204u, "Magic Shop"),
            (205u, "Welcome, traveler!"),
            (206u, "What can I get for you?"),
            (207u, "Come back anytime."),
            (208u, "I don't have that much gold."),
            (209u, "This item is not for sale."),

            // 1000-1009: Item names for testing
            (1000u, "Longsword"),
            (1001u, "Shortbow"),
            (1002u, "Leather Armor"),
            (1003u, "Healing Potion"),
            (1004u, "Gold Ring"),
            (1005u, "Iron Shield"),
            (1006u, "Magic Wand"),
            (1007u, "Torch"),
            (1008u, "Rope"),
            (1009u, "Backpack"),
        });

        TlkWriter.Write(tlk, outputPath);
    }

    /// <summary>
    /// Generate a custom.tlk for testing custom content string resolution.
    /// Custom TLK uses high-bit StrRefs (0x01000000+).
    /// </summary>
    private static void GenerateCustomTlk(string outputPath)
    {
        var tlk = new TlkFile
        {
            LanguageId = 0 // English
        };

        // Custom content entries start at StrRef 0 in the custom TLK
        // but are accessed via StrRef 0x01000000+ in game
        AddEntries(tlk, new[]
        {
            // 0-9: Custom races
            (0u, "Custom Race 1"),
            (1u, "Custom Race 2"),
            (2u, "Custom Subrace"),

            // 10-19: Custom classes
            (10u, "Custom Class"),
            (11u, "Prestige Class Test"),
            (12u, "Special Abilities"),

            // 100-109: CEP-style items
            (100u, "CEP Longsword +1"),
            (101u, "CEP Special Armor"),
            (102u, "Custom Potion of Speed"),

            // 200-209: Custom dialog
            (200u, "Greetings, adventurer!"),
            (201u, "I have a special quest for you."),
            (202u, "Thank you for your help."),
        });

        TlkWriter.Write(tlk, outputPath);
    }

    /// <summary>
    /// Add multiple entries to a TLK file, filling gaps with empty entries.
    /// </summary>
    private static void AddEntries(TlkFile tlk, (uint strRef, string text)[] entries)
    {
        // Find the highest StrRef
        uint maxStrRef = 0;
        foreach (var (strRef, _) in entries)
        {
            if (strRef > maxStrRef)
                maxStrRef = strRef;
        }

        // Initialize entries array with empty entries
        for (uint i = 0; i <= maxStrRef; i++)
        {
            tlk.Entries.Add(new TlkEntry { Flags = 0x0 });
        }

        // Fill in the actual entries
        foreach (var (strRef, text) in entries)
        {
            tlk.Entries[(int)strRef] = new TlkEntry
            {
                Flags = 0x1, // HasText
                Text = text
            };
        }
    }
}
