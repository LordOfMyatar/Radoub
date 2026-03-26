using Manifest.Services;
using Radoub.UI.Services;
using Radoub.TestUtilities.Helpers;
using Xunit;

namespace Manifest.Tests;

/// <summary>
/// Tests for SpellCheckService singleton behavior and word management.
/// </summary>
public class SpellCheckServiceTests : IDisposable
{
    private readonly string _testSettingsDir;

    public SpellCheckServiceTests()
    {
        _testSettingsDir = Path.Combine(Path.GetTempPath(), $"ManifestSpellCheck_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testSettingsDir);

        // Reset singletons for isolation
        SingletonTestHelper.ResetStaticSingleton(typeof(SpellCheckService), "_instance");
        SingletonTestHelper.ResetSingleton<SettingsService>();
        SingletonTestHelper.ConfigureSettingsDirectory("MANIFEST_SETTINGS_DIR", _testSettingsDir);
    }

    public void Dispose()
    {
        SingletonTestHelper.ResetStaticSingleton(typeof(SpellCheckService), "_instance");
        SingletonTestHelper.ResetSingleton<SettingsService>();
        SingletonTestHelper.ConfigureSettingsDirectory("MANIFEST_SETTINGS_DIR", null);

        try
        {
            if (Directory.Exists(_testSettingsDir))
                Directory.Delete(_testSettingsDir, recursive: true);
        }
        catch { }
    }

    [Fact]
    public void Instance_ReturnsSameInstance()
    {
        var instance1 = SpellCheckService.Instance;
        var instance2 = SpellCheckService.Instance;

        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void IsReady_BeforeInitialize_ReturnsFalse()
    {
        var service = SpellCheckService.Instance;

        Assert.False(service.IsReady);
    }

    [Fact]
    public void IsCorrect_WhenNotReady_ReturnsTrue()
    {
        // When not initialized, all words should be treated as correct (fail-open)
        var service = SpellCheckService.Instance;

        Assert.True(service.IsCorrect("anythingxyz"));
    }

    [Fact]
    public void CheckText_WhenNotReady_ReturnsEmpty()
    {
        var service = SpellCheckService.Instance;

        var errors = service.CheckText("misspeled wrds").ToList();

        Assert.Empty(errors);
    }

    [Fact]
    public void GetSuggestions_WhenNotReady_ReturnsEmpty()
    {
        var service = SpellCheckService.Instance;

        var suggestions = service.GetSuggestions("misspeled").ToList();

        Assert.Empty(suggestions);
    }

    [Fact]
    public void IsCorrect_NullOrEmpty_ReturnsTrue()
    {
        var service = SpellCheckService.Instance;

        Assert.True(service.IsCorrect(""));
        Assert.True(service.IsCorrect("   "));
    }

    [Fact]
    public void CheckText_NullOrEmpty_ReturnsEmpty()
    {
        var service = SpellCheckService.Instance;

        Assert.Empty(service.CheckText(""));
        Assert.Empty(service.CheckText("   "));
    }

    [Fact]
    public void SessionIgnoredCount_BeforeInit_ReturnsZero()
    {
        var service = SpellCheckService.Instance;

        Assert.Equal(0, service.SessionIgnoredCount);
    }

    [Fact]
    public void GetCustomWordCount_BeforeInit_ReturnsZero()
    {
        var service = SpellCheckService.Instance;

        Assert.Equal(0, service.GetCustomWordCount());
    }

    [Fact]
    public void AddToCustomDictionary_AddsWord_IncreasesCount()
    {
        var service = SpellCheckService.Instance;
        var initialCount = service.GetCustomWordCount();

        service.AddToCustomDictionary("Waterdeep");

        Assert.Equal(initialCount + 1, service.GetCustomWordCount());
    }

    [Fact]
    public void AddToCustomDictionary_EmptyString_DoesNotAdd()
    {
        var service = SpellCheckService.Instance;
        var initialCount = service.GetCustomWordCount();

        service.AddToCustomDictionary("");
        service.AddToCustomDictionary("   ");

        Assert.Equal(initialCount, service.GetCustomWordCount());
    }

    [Fact]
    public void AddToCustomDictionary_TrimsWhitespace()
    {
        var service = SpellCheckService.Instance;

        service.AddToCustomDictionary("  Neverwinter  ");

        // Should have added the trimmed word
        Assert.Equal(1, service.GetCustomWordCount());
    }
}
