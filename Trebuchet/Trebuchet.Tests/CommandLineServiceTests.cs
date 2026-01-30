using RadoubLauncher.Services;

namespace Trebuchet.Tests;

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
        Assert.False(options.SafeMode);
        Assert.Null(options.ModulePath);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Parse_HelpFlag_SetsShowHelp(string helpArg)
    {
        var args = new[] { helpArg };

        var options = CommandLineService.Parse(args);

        Assert.True(options.ShowHelp);
    }

    [Theory]
    [InlineData("--safemode")]
    [InlineData("--safe-mode")]
    public void Parse_SafeModeFlag_SetsSafeMode(string safeArg)
    {
        var args = new[] { safeArg };

        var options = CommandLineService.Parse(args);

        Assert.True(options.SafeMode);
    }

    [Theory]
    [InlineData("--module")]
    [InlineData("-m")]
    public void Parse_ModuleFlag_SetsModulePath(string moduleArg)
    {
        var args = new[] { moduleArg, "mymodule.mod" };

        var options = CommandLineService.Parse(args);

        Assert.Equal("mymodule.mod", options.ModulePath);
    }

    [Fact]
    public void Parse_ModuleFlag_WithoutValue_DoesNotThrow()
    {
        var args = new[] { "--module" };

        var options = CommandLineService.Parse(args);

        // Should not crash, ModulePath remains null
        Assert.Null(options.ModulePath);
    }

    [Fact]
    public void Parse_MultipleArgs_ParsesAll()
    {
        var args = new[] { "--help", "--safemode", "--module", "test.mod" };

        var options = CommandLineService.Parse(args);

        Assert.True(options.ShowHelp);
        Assert.True(options.SafeMode);
        Assert.Equal("test.mod", options.ModulePath);
    }

    [Fact]
    public void Parse_CaseInsensitive_RecognizesFlags()
    {
        var args = new[] { "--HELP", "--MODULE", "test.mod" };

        var options = CommandLineService.Parse(args);

        Assert.True(options.ShowHelp);
        Assert.Equal("test.mod", options.ModulePath);
    }

    [Fact]
    public void Parse_UnknownFlag_IsIgnored()
    {
        var args = new[] { "--unknown", "--help" };

        var options = CommandLineService.Parse(args);

        Assert.True(options.ShowHelp);
        Assert.Null(options.ModulePath);
    }

    [Fact]
    public void Parse_ModulePathWithSpaces_Preserved()
    {
        var args = new[] { "-m", "path with spaces/mymodule.mod" };

        var options = CommandLineService.Parse(args);

        Assert.Equal("path with spaces/mymodule.mod", options.ModulePath);
    }
}
