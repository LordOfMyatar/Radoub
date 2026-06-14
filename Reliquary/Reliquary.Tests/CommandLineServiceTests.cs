using PlaceableEditor.Services;

namespace PlaceableEditor.Tests;

public class CommandLineServiceTests
{
    [Fact]
    public void Parse_NoArgs_ReturnsDefaults()
    {
        var options = CommandLineService.Parse(Array.Empty<string>());

        Assert.False(options.ShowHelp);
        Assert.Null(options.FilePath);
    }

    [Fact]
    public void Parse_HelpFlag_SetsShowHelp()
    {
        var options = CommandLineService.Parse(new[] { "--help" });
        Assert.True(options.ShowHelp);
    }

    [Fact]
    public void Parse_ShortHelpFlag_SetsShowHelp()
    {
        var options = CommandLineService.Parse(new[] { "-h" });
        Assert.True(options.ShowHelp);
    }

    [Fact]
    public void Parse_FileFlag_SetsFilePath()
    {
        var options = CommandLineService.Parse(new[] { "--file", "boulder001.utp" });
        Assert.Equal("boulder001.utp", options.FilePath);
    }

    [Fact]
    public void Parse_ShortFileFlag_SetsFilePath()
    {
        var options = CommandLineService.Parse(new[] { "-f", "chest_iron.utp" });
        Assert.Equal("chest_iron.utp", options.FilePath);
    }

    [Fact]
    public void Parse_PositionalArg_SetsFilePath()
    {
        var options = CommandLineService.Parse(new[] { "door_oak.utp" });
        Assert.Equal("door_oak.utp", options.FilePath);
    }
}
