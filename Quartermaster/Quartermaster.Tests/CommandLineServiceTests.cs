using Quartermaster.Services;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for CommandLineService argument parsing.
/// </summary>
public class CommandLineServiceTests
{
    [Fact]
    public void Parse_EmptyArgs_ReturnsDefaultOptions()
    {
        var args = Array.Empty<string>();

        var options = CommandLineService.Parse(args);

        Assert.NotNull(options);
        Assert.False(options.ShowHelp);
        Assert.Null(options.FilePath);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("-?")]
    public void Parse_HelpFlag_SetsShowHelp(string helpArg)
    {
        var args = new[] { helpArg };

        var options = CommandLineService.Parse(args);

        Assert.True(options.ShowHelp);
    }

    [Theory]
    [InlineData("--file")]
    [InlineData("-f")]
    public void Parse_FileFlag_SetsFilePath(string fileArg)
    {
        var args = new[] { fileArg, "creature.utc" };

        var options = CommandLineService.Parse(args);

        Assert.Equal("creature.utc", options.FilePath);
    }

    [Fact]
    public void Parse_FileFlag_WithoutValue_DoesNotThrow()
    {
        var args = new[] { "--file" };

        var options = CommandLineService.Parse(args);

        // Should not crash, FilePath remains null
        Assert.Null(options.FilePath);
    }

    [Fact]
    public void Parse_MultipleArgs_ParsesAll()
    {
        var args = new[] { "--help", "--file", "test.bic" };

        var options = CommandLineService.Parse(args);

        Assert.True(options.ShowHelp);
        Assert.Equal("test.bic", options.FilePath);
    }

    [Fact]
    public void Parse_CaseInsensitive_RecognizesFlags()
    {
        var args = new[] { "--HELP", "--FILE", "test.utc" };

        var options = CommandLineService.Parse(args);

        Assert.True(options.ShowHelp);
        Assert.Equal("test.utc", options.FilePath);
    }

    [Fact]
    public void Parse_UnknownFlag_IsIgnored()
    {
        var args = new[] { "--unknown", "--help" };

        var options = CommandLineService.Parse(args);

        Assert.True(options.ShowHelp);
        Assert.Null(options.FilePath);
    }

    [Fact]
    public void Parse_SetsStaticOptions()
    {
        var args = new[] { "--file", "static.utc" };

        CommandLineService.Parse(args);

        Assert.Equal("static.utc", CommandLineService.Options.FilePath);
    }
}
