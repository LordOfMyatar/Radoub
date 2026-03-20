using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for the shared CommandLineParser, specifically the --project flag (#1781).
/// </summary>
public class CommandLineParserTests
{
    [Fact]
    public void Parse_ProjectFlag_SetsProjectPath()
    {
        var args = new[] { "--project", "LNS" };
        var options = CommandLineParser.Parse<CommandLineOptions>(args);
        Assert.Equal("LNS", options.ProjectPath);
    }

    [Fact]
    public void Parse_ProjectShortFlag_SetsProjectPath()
    {
        var args = new[] { "-p", "LNS" };
        var options = CommandLineParser.Parse<CommandLineOptions>(args);
        Assert.Equal("LNS", options.ProjectPath);
    }

    [Fact]
    public void Parse_ProjectAndFile_SetsBoth()
    {
        var args = new[] { "--project", "LNS", "--file", "dialog.dlg" };
        var options = CommandLineParser.Parse<CommandLineOptions>(args);
        Assert.Equal("LNS", options.ProjectPath);
        Assert.Equal("dialog.dlg", options.FilePath);
    }

    [Fact]
    public void Parse_ProjectFlag_WithoutValue_DoesNotThrow()
    {
        var args = new[] { "--project" };
        var options = CommandLineParser.Parse<CommandLineOptions>(args);
        Assert.Null(options.ProjectPath);
    }

    [Fact]
    public void Parse_ProjectFlag_CaseInsensitive()
    {
        var args = new[] { "--PROJECT", "MyMod" };
        var options = CommandLineParser.Parse<CommandLineOptions>(args);
        Assert.Equal("MyMod", options.ProjectPath);
    }

    [Fact]
    public void Parse_NoProjectFlag_ProjectPathIsNull()
    {
        var args = new[] { "--file", "test.utc" };
        var options = CommandLineParser.Parse<CommandLineOptions>(args);
        Assert.Null(options.ProjectPath);
    }
}
