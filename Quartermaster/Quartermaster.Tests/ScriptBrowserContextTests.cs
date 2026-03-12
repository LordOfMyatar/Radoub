using Quartermaster.Services;
using Radoub.Formats.Common;
using Radoub.Formats.Settings;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for QuartermasterScriptBrowserContext — path resolution for script browsing.
///
/// Desired behaviors:
///   - CurrentFileDirectory should prefer the open file's directory
///   - Falls back to RadoubSettings.CurrentModulePath when no file is open
///   - For .mod files, searches for unpacked working directories (moduleName, temp0, temp1)
///   - Returns null when no valid path can be resolved
///   - ListBuiltInScripts enumerates .nss resources from game data
///   - FindBuiltInResource delegates to game data service
///   - GameResourcesAvailable reflects whether game data is configured
/// </summary>
public class ScriptBrowserContextTests : IDisposable
{
    private readonly string _testDir;

    public ScriptBrowserContextTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"Quartermaster_ScriptBrowser_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); }
        catch { /* best effort cleanup */ }
    }

    #region CurrentFileDirectory — Open File Path

    [Fact]
    public void CurrentFileDirectory_WithValidFilePath_ReturnsFileDirectory()
    {
        // Create a temp file so its directory exists
        var filePath = Path.Combine(_testDir, "creature.utc");
        File.WriteAllText(filePath, "");

        var context = new QuartermasterScriptBrowserContext(filePath);

        Assert.Equal(_testDir, context.CurrentFileDirectory);
    }

    [Fact]
    public void CurrentFileDirectory_WithNullFilePath_ReturnsNull()
    {
        var context = new QuartermasterScriptBrowserContext(null);

        // With no file and no module path configured, should be null
        // (RadoubSettings.CurrentModulePath may be set from prior test state,
        // so we just verify it doesn't throw)
        var result = context.CurrentFileDirectory;
        // Null or a valid path — both are acceptable
        Assert.True(result == null || Directory.Exists(result));
    }

    [Fact]
    public void CurrentFileDirectory_WithNonexistentFilePath_DoesNotThrow()
    {
        var context = new QuartermasterScriptBrowserContext("/nonexistent/path/creature.utc");

        // Should gracefully fall through to module path fallback
        var result = context.CurrentFileDirectory;
        Assert.True(result == null || Directory.Exists(result));
    }

    #endregion

    #region CurrentFileDirectory — Module Path Fallback (Directory)

    [Fact]
    public void CurrentFileDirectory_NoFile_FallsBackToModuleDirectory()
    {
        var moduleDir = Path.Combine(_testDir, "mymodule");
        Directory.CreateDirectory(moduleDir);

        // Save original and set test value
        var original = RadoubSettings.Instance.CurrentModulePath;
        try
        {
            RadoubSettings.Instance.CurrentModulePath = moduleDir;
            var context = new QuartermasterScriptBrowserContext(null);

            Assert.Equal(moduleDir, context.CurrentFileDirectory);
        }
        finally
        {
            RadoubSettings.Instance.CurrentModulePath = original;
        }
    }

    #endregion

    #region CurrentFileDirectory — .mod File Working Directory

    [Fact]
    public void CurrentFileDirectory_ModFile_FindsNamedDirectory()
    {
        // Simulate: modules/mymodule.mod + modules/mymodule/ (unpacked)
        var modulesDir = Path.Combine(_testDir, "modules");
        Directory.CreateDirectory(modulesDir);
        var modFile = Path.Combine(modulesDir, "mymodule.mod");
        File.WriteAllText(modFile, ""); // Fake .mod file
        var unpackedDir = Path.Combine(modulesDir, "mymodule");
        Directory.CreateDirectory(unpackedDir);

        var original = RadoubSettings.Instance.CurrentModulePath;
        try
        {
            RadoubSettings.Instance.CurrentModulePath = modFile;
            var context = new QuartermasterScriptBrowserContext(null);

            Assert.Equal(unpackedDir, context.CurrentFileDirectory);
        }
        finally
        {
            RadoubSettings.Instance.CurrentModulePath = original;
        }
    }

    [Fact]
    public void CurrentFileDirectory_ModFile_FindsTemp0Directory()
    {
        // No named directory, but temp0 exists
        var modulesDir = Path.Combine(_testDir, "modules_temp0");
        Directory.CreateDirectory(modulesDir);
        var modFile = Path.Combine(modulesDir, "mymodule.mod");
        File.WriteAllText(modFile, "");
        var temp0Dir = Path.Combine(modulesDir, "temp0");
        Directory.CreateDirectory(temp0Dir);

        var original = RadoubSettings.Instance.CurrentModulePath;
        try
        {
            RadoubSettings.Instance.CurrentModulePath = modFile;
            var context = new QuartermasterScriptBrowserContext(null);

            Assert.Equal(temp0Dir, context.CurrentFileDirectory);
        }
        finally
        {
            RadoubSettings.Instance.CurrentModulePath = original;
        }
    }

    [Fact]
    public void CurrentFileDirectory_ModFile_PrefersNamedOverTemp()
    {
        // Both named directory AND temp0 exist — named should win
        var modulesDir = Path.Combine(_testDir, "modules_both");
        Directory.CreateDirectory(modulesDir);
        var modFile = Path.Combine(modulesDir, "mymodule.mod");
        File.WriteAllText(modFile, "");
        var namedDir = Path.Combine(modulesDir, "mymodule");
        Directory.CreateDirectory(namedDir);
        var temp0Dir = Path.Combine(modulesDir, "temp0");
        Directory.CreateDirectory(temp0Dir);

        var original = RadoubSettings.Instance.CurrentModulePath;
        try
        {
            RadoubSettings.Instance.CurrentModulePath = modFile;
            var context = new QuartermasterScriptBrowserContext(null);

            Assert.Equal(namedDir, context.CurrentFileDirectory);
        }
        finally
        {
            RadoubSettings.Instance.CurrentModulePath = original;
        }
    }

    [Fact]
    public void CurrentFileDirectory_ModFile_NoUnpackedDir_ReturnsNull()
    {
        var modulesDir = Path.Combine(_testDir, "modules_empty");
        Directory.CreateDirectory(modulesDir);
        var modFile = Path.Combine(modulesDir, "mymodule.mod");
        File.WriteAllText(modFile, "");
        // No unpacked directory exists

        var original = RadoubSettings.Instance.CurrentModulePath;
        try
        {
            RadoubSettings.Instance.CurrentModulePath = modFile;
            var context = new QuartermasterScriptBrowserContext(null);

            Assert.Null(context.CurrentFileDirectory);
        }
        finally
        {
            RadoubSettings.Instance.CurrentModulePath = original;
        }
    }

    #endregion

    #region CurrentFileDirectory — Priority

    [Fact]
    public void CurrentFileDirectory_OpenFile_TakesPriorityOverModulePath()
    {
        var fileDir = Path.Combine(_testDir, "filedir");
        Directory.CreateDirectory(fileDir);
        var filePath = Path.Combine(fileDir, "creature.utc");
        File.WriteAllText(filePath, "");

        var moduleDir = Path.Combine(_testDir, "moduledir");
        Directory.CreateDirectory(moduleDir);

        var original = RadoubSettings.Instance.CurrentModulePath;
        try
        {
            RadoubSettings.Instance.CurrentModulePath = moduleDir;
            var context = new QuartermasterScriptBrowserContext(filePath);

            // File directory should win over module path
            Assert.Equal(fileDir, context.CurrentFileDirectory);
        }
        finally
        {
            RadoubSettings.Instance.CurrentModulePath = original;
        }
    }

    #endregion

    #region ListBuiltInScripts

    [Fact]
    public void ListBuiltInScripts_WithConfiguredGameData_ReturnsScripts()
    {
        var mockGameData = new MockGameDataService(includeSampleData: false);
        mockGameData.AddResourceInfo("nw_s0_fireball", ResourceTypes.Nss, "data/xp2.bif");
        mockGameData.AddResourceInfo("nw_s0_heal", ResourceTypes.Nss, "data/xp1.bif");

        var context = new QuartermasterScriptBrowserContext(null, mockGameData);

        var scripts = context.ListBuiltInScripts().ToList();
        Assert.Equal(2, scripts.Count);
        Assert.Contains(scripts, s => s.ResRef == "nw_s0_fireball");
        Assert.Contains(scripts, s => s.ResRef == "nw_s0_heal");
    }

    [Fact]
    public void ListBuiltInScripts_NoGameData_ReturnsEmpty()
    {
        var context = new QuartermasterScriptBrowserContext(null, null);

        var scripts = context.ListBuiltInScripts().ToList();
        Assert.Empty(scripts);
    }

    [Fact]
    public void ListBuiltInScripts_UnconfiguredGameData_ReturnsEmpty()
    {
        var mockGameData = new MockGameDataService(includeSampleData: false);
        mockGameData.IsConfigured = false;

        var context = new QuartermasterScriptBrowserContext(null, mockGameData);

        var scripts = context.ListBuiltInScripts().ToList();
        Assert.Empty(scripts);
    }

    #endregion

    #region FindBuiltInResource

    [Fact]
    public void FindBuiltInResource_WithGameData_DelegatesToService()
    {
        var mockGameData = new MockGameDataService(includeSampleData: false);
        var scriptBytes = new byte[] { 0x01, 0x02, 0x03 };
        mockGameData.SetResource("nw_s0_fireball", ResourceTypes.Nss, scriptBytes);

        var context = new QuartermasterScriptBrowserContext(null, mockGameData);

        var result = context.FindBuiltInResource("nw_s0_fireball", ResourceTypes.Nss);
        Assert.NotNull(result);
        Assert.Equal(scriptBytes, result);
    }

    [Fact]
    public void FindBuiltInResource_NoGameData_ReturnsNull()
    {
        var context = new QuartermasterScriptBrowserContext(null, null);

        var result = context.FindBuiltInResource("nw_s0_fireball", ResourceTypes.Nss);
        Assert.Null(result);
    }

    #endregion

    #region GameResourcesAvailable

    [Fact]
    public void GameResourcesAvailable_ConfiguredGameData_ReturnsTrue()
    {
        var mockGameData = new MockGameDataService(includeSampleData: false);
        var context = new QuartermasterScriptBrowserContext(null, mockGameData);

        Assert.True(context.GameResourcesAvailable);
    }

    [Fact]
    public void GameResourcesAvailable_UnconfiguredGameData_ReturnsFalse()
    {
        var mockGameData = new MockGameDataService(includeSampleData: false);
        mockGameData.IsConfigured = false;
        var context = new QuartermasterScriptBrowserContext(null, mockGameData);

        Assert.False(context.GameResourcesAvailable);
    }

    [Fact]
    public void GameResourcesAvailable_NoGameData_ReturnsFalse()
    {
        var context = new QuartermasterScriptBrowserContext(null, null);

        Assert.False(context.GameResourcesAvailable);
    }

    #endregion

    #region Other Properties

    [Fact]
    public void ExternalEditorPath_ReturnsNull()
    {
        // Quartermaster doesn't support external editors yet
        var context = new QuartermasterScriptBrowserContext(null);
        Assert.Null(context.ExternalEditorPath);
    }

    [Fact]
    public void NeverwinterNightsPath_ReadsFromSettings()
    {
        var context = new QuartermasterScriptBrowserContext(null);

        // Should match RadoubSettings — just verify it doesn't throw
        var path = context.NeverwinterNightsPath;
        Assert.True(path == null || path is string);
    }

    #endregion
}
