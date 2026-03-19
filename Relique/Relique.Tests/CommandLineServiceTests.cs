using ItemEditor.Services;

namespace ItemEditor.Tests;

[Collection("CommandLine")]
public class CommandLineServiceTests
{
    [Fact]
    public void Parse_NoArgs_ReturnsDefaults()
    {
        var options = CommandLineService.Parse(Array.Empty<string>());

        Assert.False(options.ShowHelp);
        Assert.False(options.SafeMode);
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
    public void Parse_SafeModeFlag_SetsSafeMode()
    {
        var options = CommandLineService.Parse(new[] { "--safemode" });
        Assert.True(options.SafeMode);
    }

    [Fact]
    public void Parse_FileFlag_SetsFilePath()
    {
        var options = CommandLineService.Parse(new[] { "--file", "sword.uti" });
        Assert.Equal("sword.uti", options.FilePath);
    }

    [Fact]
    public void Parse_ShortFileFlag_SetsFilePath()
    {
        var options = CommandLineService.Parse(new[] { "-f", "armor.uti" });
        Assert.Equal("armor.uti", options.FilePath);
    }

    [Fact]
    public void Parse_PositionalArg_SetsFilePath()
    {
        var options = CommandLineService.Parse(new[] { "shield.uti" });
        Assert.Equal("shield.uti", options.FilePath);
    }
}
