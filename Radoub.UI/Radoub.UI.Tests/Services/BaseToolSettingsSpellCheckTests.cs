using System;
using System.IO;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests.Services;

/// <summary>
/// Tests for the base-class SpellCheckEnabled setting (#2390).
/// Uses a minimal concrete subclass of BaseToolSettingsService so the base
/// behavior is exercised independently of any tool.
/// </summary>
public class BaseToolSettingsSpellCheckTests : IDisposable
{
    private const string EnvVar = "RADOUBUI_SPELLCHECK_TEST_DIR";
    private readonly string _dir;

    public BaseToolSettingsSpellCheckTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"RadoubUI_SpellCheck_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        Environment.SetEnvironmentVariable(EnvVar, _dir);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(EnvVar, null);
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch { /* ignore cleanup failures */ }
    }

    [Fact]
    public void SpellCheckEnabled_DefaultsToTrue()
    {
        var service = new TestSettingsService();
        Assert.True(service.SpellCheckEnabled);
    }

    [Fact]
    public void SpellCheckEnabled_RoundTripsThroughDisk()
    {
        var service = new TestSettingsService();
        service.SpellCheckEnabled = false;

        // Fresh instance reads the persisted JSON
        var reloaded = new TestSettingsService();
        Assert.False(reloaded.SpellCheckEnabled);
    }

    [Fact]
    public void SpellCheckEnabled_ReadsLegacyManifestKey()
    {
        // A JSON file with the legacy SpellCheckEnabled key (as Manifest wrote it)
        // must still be honored after the property moved to the base class.
        File.WriteAllText(
            Path.Combine(_dir, "TestToolSettings.json"),
            "{ \"SpellCheckEnabled\": false }");

        var service = new TestSettingsService();
        Assert.False(service.SpellCheckEnabled);
    }

    /// <summary>
    /// Minimal concrete settings service that adds no tool-specific properties,
    /// so the test exercises only the base class behavior.
    /// </summary>
    private sealed class TestSettingsService : BaseToolSettingsService<TestSettingsService.Data>
    {
        public TestSettingsService() => Initialize();

        protected override string ToolName => "TestTool";
        protected override string SettingsEnvironmentVariable => EnvVar;
        protected override string SettingsFileName => "TestToolSettings.json";

        protected override void LoadToolSettings(Data settings) { }
        protected override void SaveToolSettings(Data settings) { }

        public sealed class Data : BaseSettingsData { }
    }
}
