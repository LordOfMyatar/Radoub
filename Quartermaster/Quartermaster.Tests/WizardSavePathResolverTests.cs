using System.IO;
using Quartermaster.Services;
using Xunit;

namespace Quartermaster.Tests;

public class WizardSavePathResolverTests
{
    [Fact]
    public void Bic_WithNwnPath_ReturnsLocalVault()
    {
        var nwn = Path.Combine(Path.GetTempPath(), "qm_wsr_nwn");
        var result = WizardSavePathResolver.ResolveDefaultDir(isBic: true, currentModulePath: null, nwnPath: nwn);
        Assert.Equal(Path.Combine(nwn, "localvault"), result);
    }

    [Fact]
    public void Bic_WithNullNwnPath_ReturnsNull()
    {
        var result = WizardSavePathResolver.ResolveDefaultDir(isBic: true, currentModulePath: null, nwnPath: null);
        Assert.Null(result);
    }

    [Fact]
    public void Utc_WithExistingModuleDir_ReturnsThatDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qm_wsr_moddir");
        Directory.CreateDirectory(dir);
        try
        {
            var result = WizardSavePathResolver.ResolveDefaultDir(isBic: false, currentModulePath: dir, nwnPath: null);
            Assert.Equal(dir, result);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Utc_WithModFile_ReturnsParentDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qm_wsr_modparent");
        Directory.CreateDirectory(dir);
        var modFile = Path.Combine(dir, "test.mod");
        File.WriteAllText(modFile, "x");
        try
        {
            var result = WizardSavePathResolver.ResolveDefaultDir(isBic: false, currentModulePath: modFile, nwnPath: null);
            Assert.Equal(dir, result);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Utc_WithNullModulePath_ReturnsNull()
    {
        var result = WizardSavePathResolver.ResolveDefaultDir(isBic: false, currentModulePath: null, nwnPath: null);
        Assert.Null(result);
    }
}
