using System.IO;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests.Services;

/// <summary>
/// Tests for the shared external-editor helper (#2295): the pure path-resolution and
/// editor-selection logic. The actual Process.Start launch is not unit-tested (side effect).
/// </summary>
public class ExternalEditorServiceTests
{
    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "radoub_ee_" + Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        return dir;
    }

    // --- ResolveScriptPath ---

    [Fact]
    public void ResolveScriptPath_FindsInCurrentFileDirectory()
    {
        var dir = TempDir();
        var script = Path.Combine(dir, "my_open.nss");
        File.WriteAllText(script, "void main() {}");

        var result = ExternalEditorService.ResolveScriptPath("my_open", dir, null);

        Assert.Equal(script, result);
        Directory.Delete(dir, true);
    }

    [Fact]
    public void ResolveScriptPath_StripsNssExtensionFromName()
    {
        var dir = TempDir();
        File.WriteAllText(Path.Combine(dir, "foo.nss"), "");

        var result = ExternalEditorService.ResolveScriptPath("foo.nss", dir, null);

        Assert.EndsWith("foo.nss", result);
        Directory.Delete(dir, true);
    }

    [Fact]
    public void ResolveScriptPath_FallsBackToModuleDirectory()
    {
        var moduleDir = TempDir();
        var script = Path.Combine(moduleDir, "mod_script.nss");
        File.WriteAllText(script, "");

        var result = ExternalEditorService.ResolveScriptPath("mod_script", "C:/nonexistent_dir", moduleDir);

        Assert.Equal(script, result);
        Directory.Delete(moduleDir, true);
    }

    [Fact]
    public void ResolveScriptPath_ReturnsNullWhenNotFound()
    {
        var result = ExternalEditorService.ResolveScriptPath("ghost", "C:/nope", "C:/also_nope");
        Assert.Null(result);
    }

    [Fact]
    public void ResolveScriptPath_ReturnsNullForEmptyName()
    {
        Assert.Null(ExternalEditorService.ResolveScriptPath("", TempDir(), null));
    }

    // --- ChooseEditor ---

    [Fact]
    public void ChooseEditor_ReturnsConfiguredPathWhenItExists()
    {
        var result = ExternalEditorService.ChooseEditor("C:/Editors/code.exe", p => p == "C:/Editors/code.exe");
        Assert.Equal("C:/Editors/code.exe", result);
    }

    [Fact]
    public void ChooseEditor_ReturnsNullWhenConfiguredPathMissing()
    {
        // null result = caller should fall back to OS default
        var result = ExternalEditorService.ChooseEditor("C:/Editors/gone.exe", _ => false);
        Assert.Null(result);
    }

    [Fact]
    public void ChooseEditor_ReturnsNullWhenUnconfigured()
    {
        Assert.Null(ExternalEditorService.ChooseEditor("", _ => true));
        Assert.Null(ExternalEditorService.ChooseEditor(null, _ => true));
    }
}
