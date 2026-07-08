using System.IO;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for SaveBlueprintPathResolver — pure path composition, Aurora filename
/// validation, overwrite detection, and directory resolution used by the shared
/// SaveBlueprintWindow (#2515).
/// </summary>
public class SaveBlueprintPathResolverTests
{
    [Fact]
    public void ComposePath_JoinsDirectoryResRefAndExtension()
    {
        var dir = Path.Combine("C:", "mod");
        var expected = Path.Combine(dir, "general_store.utm");
        Assert.Equal(expected, SaveBlueprintPathResolver.ComposePath(dir, "general_store", "utm"));
    }

    [Theory]
    [InlineData("general_store", true)]
    [InlineData("General_Store", true)]
    [InlineData("has space", false)]
    [InlineData("bad-hyphen", false)]
    [InlineData("", false)]
    [InlineData("this_name_is_too_long", false)] // 21 chars > 16
    public void IsValidAuroraFilename_EnforcesAuroraRules(string name, bool expected)
    {
        Assert.Equal(expected, SaveBlueprintPathResolver.IsValidAuroraFilename(name));
    }

    [Fact]
    public void WouldOverwrite_TrueWhenFileExists()
    {
        var temp = Path.Combine(Path.GetTempPath(), "SaveBlueprintOverwrite_" + System.Guid.NewGuid().ToString("N") + ".utm");
        try
        {
            File.WriteAllText(temp, "x");
            Assert.True(SaveBlueprintPathResolver.WouldOverwrite(temp));
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    [Fact]
    public void WouldOverwrite_FalseWhenFileMissing()
    {
        var missing = Path.Combine(Path.GetTempPath(), "SaveBlueprintMissing_" + System.Guid.NewGuid().ToString("N") + ".utm");
        Assert.False(SaveBlueprintPathResolver.WouldOverwrite(missing));
    }

    [Fact]
    public void ResolveDirectory_NoOverride_ReturnsContextDir()
    {
        var contextDir = Path.Combine("C:", "mod");
        Assert.Equal(contextDir, SaveBlueprintPathResolver.ResolveDirectory(null, contextDir));
    }

    [Fact]
    public void ResolveDirectory_Override_ReturnsOverride()
    {
        var contextDir = Path.Combine("C:", "mod");
        var overridePath = Path.Combine("C:", "other");
        Assert.Equal(overridePath, SaveBlueprintPathResolver.ResolveDirectory(overridePath, contextDir));
    }

    [Fact]
    public void ResolveDirectory_BothNull_ReturnsNull()
    {
        Assert.Null(SaveBlueprintPathResolver.ResolveDirectory(null, null));
    }
}
