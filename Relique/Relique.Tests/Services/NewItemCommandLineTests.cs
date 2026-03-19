using ItemEditor.Services;

namespace ItemEditor.Tests.Services;

public class NewItemCommandLineTests
{
    [Fact]
    public void Parse_NewFlag_SetsNewItem()
    {
        var options = CommandLineService.Parse(["--new"]);
        Assert.True(options.NewItem);
    }

    [Fact]
    public void Parse_ShortNewFlag_SetsNewItem()
    {
        var options = CommandLineService.Parse(["-n"]);
        Assert.True(options.NewItem);
    }

    [Fact]
    public void Parse_NoFlags_NewItemIsFalse()
    {
        var options = CommandLineService.Parse([]);
        Assert.False(options.NewItem);
    }

    [Fact]
    public void Parse_NewAndFile_BothSet()
    {
        var options = CommandLineService.Parse(["--new", "--file", "test.uti"]);
        Assert.True(options.NewItem);
        Assert.Equal("test.uti", options.FilePath);
    }
}
