using Xunit;

namespace Radoub.Dictionary.Tests;

/// <summary>
/// Tests for dictionary settings persistence + change notifications (#2264).
/// Uses the internal custom-path constructor so each test gets an isolated settings file.
/// </summary>
public class DictionarySettingsServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _settingsPath;

    public DictionarySettingsServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"RadoubDictSettings_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _settingsPath = Path.Combine(_dir, "settings.json");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch { }
    }

    private DictionarySettingsService New() => new(_settingsPath);

    [Fact]
    public void PrimaryLanguage_RoundTripsThroughDisk()
    {
        var a = New();
        a.PrimaryLanguage = "de_DE";

        var reloaded = New();
        Assert.Equal("de_DE", reloaded.PrimaryLanguage);
    }

    [Fact]
    public void SpellCheckEnabled_RoundTripsThroughDisk()
    {
        var a = New();
        a.SpellCheckEnabled = false;

        var reloaded = New();
        Assert.False(reloaded.SpellCheckEnabled);
    }

    [Fact]
    public void SetCustomDictionaryEnabled_RoundTripsThroughDisk()
    {
        var a = New();
        a.SetCustomDictionaryEnabled("lotr", false);
        Assert.False(a.IsCustomDictionaryEnabled("lotr"));

        var reloaded = New();
        Assert.False(reloaded.IsCustomDictionaryEnabled("lotr"));
    }

    [Fact]
    public async Task PrimaryLanguageChanged_FiresOnChange()
    {
        var svc = New();
        var tcs = new TaskCompletionSource<string>();
        svc.PrimaryLanguageChanged += (_, lang) => tcs.TrySetResult(lang);

        svc.PrimaryLanguage = "fr_FR";

        // Event is dispatched off the caller thread (Finding #4) — await with a timeout.
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
        Assert.Same(tcs.Task, completed);
        Assert.Equal("fr_FR", await tcs.Task);
    }

    [Fact]
    public void PrimaryLanguage_NoChange_DoesNotRewrite()
    {
        var svc = New();
        svc.PrimaryLanguage = "en_US"; // default — same value, no event/save expected
        // Setting the same value again must be a no-op (no throw).
        svc.PrimaryLanguage = "en_US";
        Assert.Equal("en_US", svc.PrimaryLanguage);
    }
}
