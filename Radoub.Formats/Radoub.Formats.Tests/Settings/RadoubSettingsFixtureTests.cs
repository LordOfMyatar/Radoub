using Radoub.Formats.Settings;
using Radoub.Formats.Tests.Settings;
using Xunit;

namespace Radoub.Formats.Tests.Settings;

/// <summary>
/// Tests for <see cref="RadoubSettingsFixture"/> — the fixture itself, not the singleton.
/// Verifies the lifecycle contract that downstream singleton-touching tests rely on:
/// env var set on construction, env var cleared on disposal, temp directory present,
/// temp directory deleted, singleton instance reset.
/// </summary>
public class RadoubSettingsFixtureTests
{
    [Fact]
    public void Construction_SetsEnvironmentVariable()
    {
        using var fixture = new RadoubSettingsFixture();

        var envValue = Environment.GetEnvironmentVariable("RADOUB_SETTINGS_DIR");
        Assert.Equal(fixture.TestDirectory, envValue);
    }

    [Fact]
    public void Construction_CreatesTestDirectory()
    {
        using var fixture = new RadoubSettingsFixture();

        Assert.True(Directory.Exists(fixture.TestDirectory));
    }

    [Fact]
    public void Construction_TestDirectoryIsUnderTempPath()
    {
        using var fixture = new RadoubSettingsFixture();

        Assert.StartsWith(Path.GetTempPath(), fixture.TestDirectory);
    }

    [Fact]
    public void Dispose_ClearsEnvironmentVariable()
    {
        var fixture = new RadoubSettingsFixture();
        fixture.Dispose();

        var envValue = Environment.GetEnvironmentVariable("RADOUB_SETTINGS_DIR");
        Assert.True(string.IsNullOrEmpty(envValue));
    }

    [Fact]
    public void Dispose_DeletesTestDirectory()
    {
        var fixture = new RadoubSettingsFixture();
        var dir = fixture.TestDirectory;
        Assert.True(Directory.Exists(dir));

        fixture.Dispose();

        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public void Dispose_DoesNotThrow_WhenTestDirectoryAlreadyDeleted()
    {
        var fixture = new RadoubSettingsFixture();
        Directory.Delete(fixture.TestDirectory, recursive: true);

        // Should not throw — fixture must swallow IO errors during cleanup
        fixture.Dispose();
    }

    [Fact]
    public void Construction_AfterPriorInstance_GetsFreshDirectory()
    {
        string firstDir;
        using (var first = new RadoubSettingsFixture())
        {
            firstDir = first.TestDirectory;
        }

        using var second = new RadoubSettingsFixture();
        Assert.NotEqual(firstDir, second.TestDirectory);
    }

    [Fact]
    public void Construction_ResetsRadoubSettingsSingleton()
    {
        // Touch singleton in first fixture's environment
        using (var first = new RadoubSettingsFixture())
        {
            var instance = RadoubSettings.Instance;
            instance.CustomTlkPath = Path.Combine(first.TestDirectory, "tlk", "first.tlk");
        }

        // Second fixture must give us a fresh singleton (no leaked state from first)
        using var second = new RadoubSettingsFixture();
        var fresh = RadoubSettings.Instance;
        Assert.Equal("", fresh.CustomTlkPath);
    }
}
