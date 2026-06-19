using System.Collections.Generic;
using System.IO;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Guards #2300: a tool that was already running when Trebuchet *edits* an
/// existing theme JSON kept a stale cached ThemeManifest forever. The
/// Activated refresh path (ReloadSettings + ApplySharedTheme) never
/// re-discovered themes, so ApplyTheme(id) succeeded but applied outdated
/// colors.
///
/// Fix: ApplySharedTheme's effective-id resolution now calls DiscoverThemes()
/// first. DiscoverThemes honors the catalog cache's mtime check, so it is a
/// cheap no-op when nothing changed and a rescan the moment Trebuchet
/// rewrites the JSON.
/// </summary>
public class ThemeManagerActivatedRefreshTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _themesDir;

    public ThemeManagerActivatedRefreshTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"RadoubThemeRefresh_{Guid.NewGuid():N}");
        _themesDir = Path.Combine(_tempDir, "Themes");
        Directory.CreateDirectory(_themesDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string WriteTheme(string id, string background)
    {
        var path = Path.Combine(_themesDir, $"{id}.json");
        File.WriteAllText(path, $$"""
        {
          "manifest_version": "1.0",
          "plugin": { "id": "{{id}}", "name": "Test", "type": "theme" },
          "base_theme": "Light",
          "colors": { "background": "{{background}}", "text": "#000000" }
        }
        """);
        return path;
    }

    [Fact]
    public void RefreshThemeCatalog_PicksUpEditedThemeFile()
    {
        const string id = "org.test.editme";
        var path = WriteTheme(id, "#111111");

        // Test-only ctor with explicit dirs + isolated cache so we never touch
        // real user themes or the shared catalog cache.
        var cachePath = Path.Combine(_tempDir, "ThemeCatalog.json");
        var manager = new ThemeManager("Test", new List<string> { _themesDir }, cachePath);
        manager.DiscoverThemes();

        var before = manager.GetCachedManifest(id);
        Assert.NotNull(before);
        Assert.Equal("#111111", before!.Colors.Background);

        // Trebuchet rewrites the existing theme file with new colors.
        File.WriteAllText(path, File.ReadAllText(path).Replace("#111111", "#222222"));
        // Bump the directory mtime so the catalog cache's mtime check invalidates,
        // mirroring what a real file rewrite does.
        Directory.SetLastWriteTimeUtc(_themesDir, DateTime.UtcNow.AddMinutes(5));

        // The Activated path's id resolution must re-discover before returning.
        manager.RefreshThemeCatalog();

        var after = manager.GetCachedManifest(id);
        Assert.NotNull(after);
        Assert.Equal("#222222", after!.Colors.Background);
    }
}
