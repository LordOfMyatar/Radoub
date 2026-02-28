using MerchantEditor.Services;

namespace Fence.Tests;

/// <summary>
/// Tests for Fence CommandLineService - validates CLI argument parsing.
/// </summary>
public class CommandLineServiceTests
{
    [Fact]
    public void Parse_NoArgs_ReturnsEmptyOptions()
    {
        var args = Array.Empty<string>();

        var options = CommandLineService.Parse(args);

        Assert.Null(options.FilePath);
        Assert.False(options.SafeMode);
        Assert.False(options.ShowHelp);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("/?")]
    public void Parse_HelpFlag_SetsShowHelp(string flag)
    {
        var options = CommandLineService.Parse(new[] { flag });

        Assert.True(options.ShowHelp);
    }

    [Theory]
    [InlineData("--safemode")]
    [InlineData("-s")]
    [InlineData("--safe-mode")]
    public void Parse_SafeModeFlag_SetsSafeMode(string flag)
    {
        var options = CommandLineService.Parse(new[] { flag });

        Assert.True(options.SafeMode);
    }

    [Theory]
    [InlineData("--file", "store.utm")]
    [InlineData("-f", "store.utm")]
    public void Parse_FileFlag_SetsFilePath(string flag, string file)
    {
        var options = CommandLineService.Parse(new[] { flag, file });

        Assert.Equal(file, options.FilePath);
    }

    [Fact]
    public void Parse_BareExistingFile_SetsFilePath()
    {
        // Bare file args only work when file exists on disk (no fileExtension configured)
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.utm");
        try
        {
            File.WriteAllText(tempFile, "");
            var options = CommandLineService.Parse(new[] { tempFile });

            Assert.Equal(tempFile, options.FilePath);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Parse_BareNonExistentFile_DoesNotSetFilePath()
    {
        // Without fileExtension configured, non-existent bare paths are ignored
        var options = CommandLineService.Parse(new[] { "nonexistent.utm" });

        Assert.Null(options.FilePath);
    }

    [Fact]
    public void Parse_CombinedArgs_ParsesAll()
    {
        var options = CommandLineService.Parse(new[] { "--safemode", "--file", "test.utm" });

        Assert.True(options.SafeMode);
        Assert.Equal("test.utm", options.FilePath);
    }

    [Fact]
    public void Options_StaticProperty_ReturnsParsedOptions()
    {
        CommandLineService.Parse(new[] { "--safemode" });

        Assert.True(CommandLineService.Options.SafeMode);
    }

    [Fact]
    public void PrintHelp_DoesNotThrow()
    {
        var exception = Record.Exception(() => CommandLineService.PrintHelp());

        Assert.Null(exception);
    }
}
