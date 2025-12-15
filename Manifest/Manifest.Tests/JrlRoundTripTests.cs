using Radoub.Formats.Jrl;
using Xunit;

namespace Manifest.Tests;

/// <summary>
/// Round-trip tests for JRL (Journal) file format.
/// Validates that files can be read, written, and re-read with data preserved.
/// </summary>
public class JrlRoundTripTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _testDataPath;

    public JrlRoundTripTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ManifestTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        _testDataPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            try { Directory.Delete(_testDirectory, true); }
            catch { /* Ignore cleanup errors */ }
        }
    }

    #region Basic Round-Trip Tests

    [Fact]
    public void RoundTrip_EmptyJrl_Succeeds()
    {
        // Arrange: Create empty JRL
        var original = new JrlFile();

        // Act: Write and read back
        var buffer = JrlWriter.Write(original);
        var result = JrlReader.Read(buffer);

        // Assert
        Assert.Equal("JRL ", result.FileType);
        Assert.Equal("V3.2", result.FileVersion);
        Assert.Empty(result.Categories);
    }

    [Fact]
    public void RoundTrip_SingleCategory_PreservesAllFields()
    {
        // Arrange
        var original = new JrlFile();
        var category = new JournalCategory
        {
            Tag = "main_quest",
            Priority = 1,
            XP = 500,
            Comment = "Test comment"
        };
        category.Name.SetString(0, "The Main Quest");
        original.Categories.Add(category);

        // Act
        var buffer = JrlWriter.Write(original);
        var result = JrlReader.Read(buffer);

        // Assert
        Assert.Single(result.Categories);
        var cat = result.Categories[0];
        Assert.Equal("main_quest", cat.Tag);
        Assert.Equal("The Main Quest", cat.Name.GetDefault());
        Assert.Equal(1u, cat.Priority);
        Assert.Equal(500u, cat.XP);
        Assert.Equal("Test comment", cat.Comment);
    }

    [Fact]
    public void RoundTrip_CategoryWithEntries_PreservesEntries()
    {
        // Arrange
        var original = new JrlFile();
        var category = new JournalCategory { Tag = "quest1" };
        category.Name.SetString(0, "Quest Name");

        var entry1 = new JournalEntry { ID = 100, End = false };
        entry1.Text.SetString(0, "First entry text");
        category.Entries.Add(entry1);

        var entry2 = new JournalEntry { ID = 200, End = true };
        entry2.Text.SetString(0, "Quest complete!");
        category.Entries.Add(entry2);

        original.Categories.Add(category);

        // Act
        var buffer = JrlWriter.Write(original);
        var result = JrlReader.Read(buffer);

        // Assert
        Assert.Single(result.Categories);
        Assert.Equal(2, result.Categories[0].Entries.Count);

        var e1 = result.Categories[0].Entries[0];
        Assert.Equal(100u, e1.ID);
        Assert.Equal("First entry text", e1.Text.GetDefault());
        Assert.False(e1.End);

        var e2 = result.Categories[0].Entries[1];
        Assert.Equal(200u, e2.ID);
        Assert.Equal("Quest complete!", e2.Text.GetDefault());
        Assert.True(e2.End);
    }

    [Fact]
    public void RoundTrip_MultipleCategories_PreservesOrder()
    {
        // Arrange
        var original = new JrlFile();
        for (int i = 0; i < 5; i++)
        {
            var cat = new JournalCategory
            {
                Tag = $"quest_{i}",
                Priority = (uint)i,
                XP = (uint)(i * 100)
            };
            cat.Name.SetString(0, $"Quest {i}");
            original.Categories.Add(cat);
        }

        // Act
        var buffer = JrlWriter.Write(original);
        var result = JrlReader.Read(buffer);

        // Assert
        Assert.Equal(5, result.Categories.Count);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal($"quest_{i}", result.Categories[i].Tag);
            Assert.Equal($"Quest {i}", result.Categories[i].Name.GetDefault());
            Assert.Equal((uint)i, result.Categories[i].Priority);
            Assert.Equal((uint)(i * 100), result.Categories[i].XP);
        }
    }

    #endregion

    #region Priority Values Tests

    [Theory]
    [InlineData(0u)] // Highest
    [InlineData(1u)] // High
    [InlineData(2u)] // Medium
    [InlineData(3u)] // Low
    [InlineData(4u)] // Lowest
    public void RoundTrip_Priority_PreservesValue(uint priority)
    {
        var original = new JrlFile();
        var category = new JournalCategory { Tag = "test", Priority = priority };
        original.Categories.Add(category);

        var buffer = JrlWriter.Write(original);
        var result = JrlReader.Read(buffer);

        Assert.Equal(priority, result.Categories[0].Priority);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void RoundTrip_CategoryNoEntries_Succeeds()
    {
        var original = new JrlFile();
        var category = new JournalCategory { Tag = "empty_quest" };
        category.Name.SetString(0, "Empty Quest");
        original.Categories.Add(category);

        var buffer = JrlWriter.Write(original);
        var result = JrlReader.Read(buffer);

        Assert.Single(result.Categories);
        Assert.Empty(result.Categories[0].Entries);
    }

    [Fact]
    public void RoundTrip_LongEntryText_PreservesFullText()
    {
        var longText = new string('A', 1000);

        var original = new JrlFile();
        var category = new JournalCategory { Tag = "quest1" };
        var entry = new JournalEntry { ID = 1 };
        entry.Text.SetString(0, longText);
        category.Entries.Add(entry);
        original.Categories.Add(category);

        var buffer = JrlWriter.Write(original);
        var result = JrlReader.Read(buffer);

        Assert.Equal(longText, result.Categories[0].Entries[0].Text.GetDefault());
    }

    [Fact]
    public void RoundTrip_SpecialCharacters_PreservesText()
    {
        var specialText = "Line1\nLine2\tTabbed \"Quoted\" <angle> & ampersand";

        var original = new JrlFile();
        var category = new JournalCategory { Tag = "quest1" };
        category.Name.SetString(0, specialText);
        original.Categories.Add(category);

        var buffer = JrlWriter.Write(original);
        var result = JrlReader.Read(buffer);

        Assert.Equal(specialText, result.Categories[0].Name.GetDefault());
    }

    [Fact]
    public void RoundTrip_UnicodeText_PreservesText()
    {
        var unicodeText = "Quest: Élémentaire à résoudre!";

        var original = new JrlFile();
        var category = new JournalCategory { Tag = "quest1" };
        category.Name.SetString(0, unicodeText);
        original.Categories.Add(category);

        var buffer = JrlWriter.Write(original);
        var result = JrlReader.Read(buffer);

        Assert.Equal(unicodeText, result.Categories[0].Name.GetDefault());
    }

    #endregion

    #region Real File Tests

    [Fact]
    public void RoundTrip_RealJrlFile_PreservesData()
    {
        var originalPath = Path.Combine(_testDataPath, "original_module.jrl");
        if (!File.Exists(originalPath))
        {
            // Skip if test data not available
            return;
        }

        // Read original
        var original = JrlReader.Read(originalPath);

        // Write to temp and read back
        var tempPath = Path.Combine(_testDirectory, "roundtrip.jrl");
        JrlWriter.Write(original, tempPath);
        var result = JrlReader.Read(tempPath);

        // Compare structure
        Assert.Equal(original.Categories.Count, result.Categories.Count);

        for (int i = 0; i < original.Categories.Count; i++)
        {
            var origCat = original.Categories[i];
            var resCat = result.Categories[i];

            Assert.Equal(origCat.Tag, resCat.Tag);
            Assert.Equal(origCat.Priority, resCat.Priority);
            Assert.Equal(origCat.XP, resCat.XP);
            Assert.Equal(origCat.Entries.Count, resCat.Entries.Count);
        }
    }

    #endregion
}
