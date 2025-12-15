using Avalonia.Headless.XUnit;
using Radoub.Formats.Jrl;
using Xunit;

namespace Manifest.Tests;

/// <summary>
/// Headless tests for Manifest UI operations.
/// Uses Avalonia.Headless for testing without launching a window.
/// </summary>
public class ManifestHeadlessTests : IDisposable
{
    private readonly string _testDirectory;

    public ManifestHeadlessTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ManifestHeadless_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        // Generate test files
        TestDataGenerator.GenerateTestFiles(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            try { Directory.Delete(_testDirectory, true); }
            catch { /* Ignore cleanup errors */ }
        }
    }

    #region JRL Model Operations

    [AvaloniaFact]
    public void AddCategory_ToEmptyJrl_AddsSuccessfully()
    {
        // Arrange
        var jrl = TestDataGenerator.CreateEmptyJrl();

        // Act
        var category = new JournalCategory
        {
            Tag = "new_quest",
            Priority = 2,
            XP = 0
        };
        category.Name.SetString(0, "New Quest");
        jrl.Categories.Add(category);

        // Assert
        Assert.Single(jrl.Categories);
        Assert.Equal("new_quest", jrl.Categories[0].Tag);
    }

    [AvaloniaFact]
    public void AddEntry_ToCategory_AddsSuccessfully()
    {
        // Arrange
        var jrl = TestDataGenerator.CreateSingleCategoryJrl();
        var category = jrl.Categories[0];

        // Act
        var entry = new JournalEntry
        {
            ID = 100,
            End = false
        };
        entry.Text.SetString(0, "New entry text");
        category.Entries.Add(entry);

        // Assert
        Assert.Single(category.Entries);
        Assert.Equal(100u, category.Entries[0].ID);
        Assert.Equal("New entry text", category.Entries[0].Text.GetDefault());
    }

    [AvaloniaFact]
    public void DeleteCategory_FromJrl_RemovesSuccessfully()
    {
        // Arrange
        var jrl = TestDataGenerator.CreateFullTestJrl();
        var initialCount = jrl.Categories.Count;
        var categoryToRemove = jrl.Categories[0];

        // Act
        jrl.Categories.Remove(categoryToRemove);

        // Assert
        Assert.Equal(initialCount - 1, jrl.Categories.Count);
        Assert.DoesNotContain(categoryToRemove, jrl.Categories);
    }

    [AvaloniaFact]
    public void DeleteEntry_FromCategory_RemovesSuccessfully()
    {
        // Arrange
        var jrl = TestDataGenerator.CreateFullTestJrl();
        var category = jrl.Categories[0];
        var initialCount = category.Entries.Count;
        var entryToRemove = category.Entries[0];

        // Act
        category.Entries.Remove(entryToRemove);

        // Assert
        Assert.Equal(initialCount - 1, category.Entries.Count);
        Assert.DoesNotContain(entryToRemove, category.Entries);
    }

    #endregion

    #region File Operations

    [AvaloniaFact]
    public void LoadJrl_FromFile_LoadsSuccessfully()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "full_test.jrl");

        // Act
        var jrl = JrlReader.Read(testFile);

        // Assert
        Assert.NotNull(jrl);
        Assert.Equal(2, jrl.Categories.Count);
        Assert.Equal("main_quest", jrl.Categories[0].Tag);
    }

    [AvaloniaFact]
    public void SaveJrl_ToFile_SavesSuccessfully()
    {
        // Arrange
        var jrl = TestDataGenerator.CreateFullTestJrl();
        var outputPath = Path.Combine(_testDirectory, "save_test.jrl");

        // Act
        JrlWriter.Write(jrl, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath));
        var reloaded = JrlReader.Read(outputPath);
        Assert.Equal(jrl.Categories.Count, reloaded.Categories.Count);
    }

    [AvaloniaFact]
    public void CreateEntry_SaveAndReload_PreservesData()
    {
        // Arrange - Start with single category JRL
        var jrl = TestDataGenerator.CreateSingleCategoryJrl();
        var category = jrl.Categories[0];

        // Act - Add entry
        var entry = new JournalEntry { ID = 100, End = false };
        entry.Text.SetString(0, "Added entry");
        category.Entries.Add(entry);

        // Save and reload
        var outputPath = Path.Combine(_testDirectory, "create_entry_test.jrl");
        JrlWriter.Write(jrl, outputPath);
        var reloaded = JrlReader.Read(outputPath);

        // Assert
        Assert.Single(reloaded.Categories[0].Entries);
        Assert.Equal("Added entry", reloaded.Categories[0].Entries[0].Text.GetDefault());
    }

    [AvaloniaFact]
    public void DeleteEntry_SaveAndReload_PersistsDelete()
    {
        // Arrange - Full test JRL
        var jrl = TestDataGenerator.CreateFullTestJrl();
        var category = jrl.Categories[0];
        var initialCount = category.Entries.Count;

        // Act - Delete first entry
        category.Entries.RemoveAt(0);

        // Save and reload
        var outputPath = Path.Combine(_testDirectory, "delete_entry_test.jrl");
        JrlWriter.Write(jrl, outputPath);
        var reloaded = JrlReader.Read(outputPath);

        // Assert
        Assert.Equal(initialCount - 1, reloaded.Categories[0].Entries.Count);
    }

    #endregion

    #region ID Generation Tests

    [AvaloniaFact]
    public void GenerateEntryId_IncrementsByHundred()
    {
        // Arrange
        var category = new JournalCategory { Tag = "test" };
        category.Entries.Add(new JournalEntry { ID = 100 });
        category.Entries.Add(new JournalEntry { ID = 200 });

        // Act - Calculate next ID (matching Manifest behavior)
        uint nextId = category.Entries.Count > 0
            ? ((category.Entries.Max(e => e.ID) / 100) + 1) * 100
            : 100;

        // Assert
        Assert.Equal(300u, nextId);
    }

    [AvaloniaFact]
    public void GenerateEntryId_FirstEntry_StartsAtHundred()
    {
        // Arrange
        var category = new JournalCategory { Tag = "test" };

        // Act
        uint nextId = category.Entries.Count > 0
            ? ((category.Entries.Max(e => e.ID) / 100) + 1) * 100
            : 100;

        // Assert
        Assert.Equal(100u, nextId);
    }

    #endregion
}
