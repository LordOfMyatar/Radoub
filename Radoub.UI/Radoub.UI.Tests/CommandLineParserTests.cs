using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for the shared CommandLineParser, specifically the --mod flag (#1781).
/// </summary>
public class CommandLineParserTests
{
    [Fact]
    public void Parse_ModFlag_SetsModuleName()
    {
        var args = new[] { "--mod", "LNS" };
        var options = CommandLineParser.Parse<CommandLineOptions>(args);
        Assert.Equal("LNS", options.ModuleName);
    }

    [Fact]
    public void Parse_ModShortFlag_SetsModuleName()
    {
        var args = new[] { "-m", "LNS" };
        var options = CommandLineParser.Parse<CommandLineOptions>(args);
        Assert.Equal("LNS", options.ModuleName);
    }

    [Fact]
    public void Parse_ModAndFile_SetsBoth()
    {
        var args = new[] { "--mod", "LNS", "--file", "dialog.dlg" };
        var options = CommandLineParser.Parse<CommandLineOptions>(args);
        Assert.Equal("LNS", options.ModuleName);
        Assert.Equal("dialog.dlg", options.FilePath);
    }

    [Fact]
    public void Parse_ModFlag_WithoutValue_DoesNotThrow()
    {
        var args = new[] { "--mod" };
        var options = CommandLineParser.Parse<CommandLineOptions>(args);
        Assert.Null(options.ModuleName);
    }

    [Fact]
    public void Parse_ModFlag_CaseInsensitive()
    {
        var args = new[] { "--MOD", "MyMod" };
        var options = CommandLineParser.Parse<CommandLineOptions>(args);
        Assert.Equal("MyMod", options.ModuleName);
    }

    [Fact]
    public void Parse_NoModFlag_ModuleNameIsNull()
    {
        var args = new[] { "--file", "test.utc" };
        var options = CommandLineParser.Parse<CommandLineOptions>(args);
        Assert.Null(options.ModuleName);
    }
}
