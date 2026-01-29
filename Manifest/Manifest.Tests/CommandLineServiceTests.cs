using System;
using System.IO;
using Manifest.Services;
using Xunit;

namespace Manifest.Tests;

/// <summary>
/// Tests for Manifest CommandLineService - validates cross-tool CLI argument parsing.
/// Manifest has unique --quest and --entry flags for cross-tool navigation from Parley.
/// </summary>
public class CommandLineServiceTests
{
    [Fact]
    public void Parse_NoArgs_ReturnsEmptyOptions()
    {
        // Arrange
        var args = Array.Empty<string>();

        // Act
        var options = CommandLineService.Parse(args);

        // Assert
        Assert.Null(options.FilePath);
        Assert.Null(options.QuestTag);
        Assert.Null(options.EntryId);
        Assert.False(options.SafeMode);
        Assert.False(options.ShowHelp);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("/?")]
    public void Parse_HelpFlag_SetsShowHelp(string flag)
    {
        // Arrange
        var args = new[] { flag };

        // Act
        var options = CommandLineService.Parse(args);

        // Assert
        Assert.True(options.ShowHelp);
    }

    [Theory]
    [InlineData("--safemode")]
    [InlineData("-s")]
    [InlineData("--safe-mode")]
    public void Parse_SafeModeFlag_SetsSafeMode(string flag)
    {
        // Arrange
        var args = new[] { flag };

        // Act
        var options = CommandLineService.Parse(args);

        // Assert
        Assert.True(options.SafeMode);
    }

    [Theory]
    [InlineData("--file", "test.jrl")]
    [InlineData("-f", "test.jrl")]
    public void Parse_FileFlag_SetsFilePath(string flag, string file)
    {
        // Arrange
        var args = new[] { flag, file };

        // Act
        var options = CommandLineService.Parse(args);

        // Assert
        Assert.Equal(file, options.FilePath);
    }

    [Fact]
    public void Parse_JrlFile_SetsFilePath()
    {
        // Arrange - positional argument ending in .jrl
        var args = new[] { "module.jrl" };

        // Act
        var options = CommandLineService.Parse(args);

        // Assert
        Assert.Equal("module.jrl", options.FilePath);
    }

    #region Quest/Entry Navigation Tests (Cross-Tool Integration)

    [Theory]
    [InlineData("--quest", "my_quest_tag")]
    [InlineData("-q", "my_quest_tag")]
    public void Parse_QuestFlag_SetsQuestTag(string flag, string questTag)
    {
        // Arrange
        var args = new[] { flag, questTag };

        // Act
        var options = CommandLineService.Parse(args);

        // Assert
        Assert.Equal(questTag, options.QuestTag);
    }

    [Theory]
    [InlineData("--entry", "100")]
    [InlineData("-e", "100")]
    public void Parse_EntryFlag_SetsEntryId(string flag, string entryId)
    {
        // Arrange
        var args = new[] { flag, entryId };

        // Act
        var options = CommandLineService.Parse(args);

        // Assert
        Assert.Equal(100u, options.EntryId);
    }

    [Fact]
    public void Parse_InvalidEntryId_DoesNotSetEntryId()
    {
        // Arrange - non-numeric entry ID
        var args = new[] { "--entry", "not_a_number" };

        // Act
        var options = CommandLineService.Parse(args);

        // Assert
        Assert.Null(options.EntryId);
    }

    [Fact]
    public void Parse_NegativeEntryId_DoesNotSetEntryId()
    {
        // Arrange - negative entry ID (uint.TryParse will fail)
        var args = new[] { "--entry", "-5" };

        // Act
        var options = CommandLineService.Parse(args);

        // Assert
        Assert.Null(options.EntryId);
    }

    [Fact]
    public void Parse_QuestAndEntry_ParsesBoth()
    {
        // Arrange - full cross-tool invocation pattern
        var args = new[] { "module.jrl", "--quest", "main_quest", "--entry", "150" };

        // Act
        var options = CommandLineService.Parse(args);

        // Assert
        Assert.Equal("module.jrl", options.FilePath);
        Assert.Equal("main_quest", options.QuestTag);
        Assert.Equal(150u, options.EntryId);
    }

    [Fact]
    public void Parse_CrossToolFullArgs_ParsesAllCorrectly()
    {
        // Arrange - simulates: Parley launches Manifest with quest navigation
        // Example: Manifest.exe --file "C:\module.jrl" -q my_quest -e 100
        var args = new[] { "--file", "C:\\path\\module.jrl", "-q", "side_quest_01", "-e", "42" };

        // Act
        var options = CommandLineService.Parse(args);

        // Assert
        Assert.Equal("C:\\path\\module.jrl", options.FilePath);
        Assert.Equal("side_quest_01", options.QuestTag);
        Assert.Equal(42u, options.EntryId);
    }

    #endregion

    [Fact]
    public void Parse_FilePathWithoutExtension_AcceptsExistingFile()
    {
        // Arrange - create a temp file
        var tempFile = Path.GetTempFileName();
        try
        {
            var args = new[] { tempFile };

            // Act
            var options = CommandLineService.Parse(args);

            // Assert - existing files without .jrl extension should be accepted
            Assert.Equal(tempFile, options.FilePath);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Parse_CombinedArgs_ParsesAllCorrectly()
    {
        // Arrange
        var args = new[] { "--safe-mode", "journal.jrl", "-q", "test_quest" };

        // Act
        var options = CommandLineService.Parse(args);

        // Assert
        Assert.True(options.SafeMode);
        Assert.Equal("journal.jrl", options.FilePath);
        Assert.Equal("test_quest", options.QuestTag);
    }

    [Fact]
    public void PrintHelp_DoesNotThrow()
    {
        // Act & Assert - just verify it doesn't throw
        var exception = Record.Exception(() => CommandLineService.PrintHelp());
        Assert.Null(exception);
    }

    [Fact]
    public void Options_StaticProperty_ReturnsParsedOptions()
    {
        // Arrange
        var args = new[] { "--safe-mode", "-q", "test_quest" };
        CommandLineService.Parse(args);

        // Act
        var options = CommandLineService.Options;

        // Assert
        Assert.True(options.SafeMode);
        Assert.Equal("test_quest", options.QuestTag);
    }
}
