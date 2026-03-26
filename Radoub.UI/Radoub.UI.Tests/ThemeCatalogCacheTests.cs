using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

public class ThemeCatalogCacheTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _cachePath;

    public ThemeCatalogCacheTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"RadoubTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _cachePath = Path.Combine(_tempDir, "ThemeCatalog.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenNoCacheExists()
    {
        var cache = new ThemeCatalogCache(_cachePath);
        var dirs = new List<string> { Path.Combine(_tempDir, "themes") };

        Assert.False(cache.IsValid(dirs));
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenCacheIsCorrupt()
    {
        File.WriteAllText(_cachePath, "not valid json{{{");
        var cache = new ThemeCatalogCache(_cachePath);
        var dirs = new List<string> { Path.Combine(_tempDir, "themes") };

        Assert.False(cache.IsValid(dirs));
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenCacheVersionMismatch()
    {
        var data = new ThemeCatalogData { CacheVersion = 99 };
        var cache = new ThemeCatalogCache(_cachePath);
        cache.Save(data);

        var dirs = new List<string> { Path.Combine(_tempDir, "themes") };
        Assert.False(cache.IsValid(dirs));
    }

    [Fact]
    public void IsValid_ReturnsTrue_WhenTimestampsMatch()
    {
        var themesDir = Path.Combine(_tempDir, "themes");
        Directory.CreateDirectory(themesDir);

        var dirs = new List<string> { themesDir };
        var timestamps = ThemeCatalogCache.BuildTimestamps(dirs);

        var data = new ThemeCatalogData
        {
            CacheVersion = 1,
            DirectoryTimestamps = timestamps,
            ThemeFiles = new Dictionary<string, string>
            {
                ["org.test.light"] = Path.Combine(themesDir, "light.json")
            }
        };

        var cache = new ThemeCatalogCache(_cachePath);
        cache.Save(data);

        Assert.True(cache.IsValid(dirs));
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenDirectoryModified()
    {
        var themesDir = Path.Combine(_tempDir, "themes");
        Directory.CreateDirectory(themesDir);

        var dirs = new List<string> { themesDir };
        var timestamps = ThemeCatalogCache.BuildTimestamps(dirs);

        var data = new ThemeCatalogData
        {
            CacheVersion = 1,
            DirectoryTimestamps = timestamps,
            ThemeFiles = new Dictionary<string, string>()
        };

        var cache = new ThemeCatalogCache(_cachePath);
        cache.Save(data);

        // Modify the directory timestamp to simulate a change
        Directory.SetLastWriteTimeUtc(themesDir, DateTime.UtcNow.AddHours(1));

        Assert.False(cache.IsValid(dirs));
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenDirectoryCountChanges()
    {
        var dir1 = Path.Combine(_tempDir, "dir1");
        var dir2 = Path.Combine(_tempDir, "dir2");
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        // Cache with 1 directory
        var data = new ThemeCatalogData
        {
            CacheVersion = 1,
            DirectoryTimestamps = ThemeCatalogCache.BuildTimestamps(new List<string> { dir1 }),
            ThemeFiles = new Dictionary<string, string>()
        };

        var cache = new ThemeCatalogCache(_cachePath);
        cache.Save(data);

        // Validate with 2 directories
        Assert.False(cache.IsValid(new List<string> { dir1, dir2 }));
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenDirectoryDoesNotExist()
    {
        var nonExistentDir = Path.Combine(_tempDir, "does_not_exist");
        var dirs = new List<string> { nonExistentDir };

        // Cache with null timestamp for non-existent dir
        var data = new ThemeCatalogData
        {
            CacheVersion = 1,
            DirectoryTimestamps = new Dictionary<string, string?> { [nonExistentDir] = null },
            ThemeFiles = new Dictionary<string, string>()
        };

        var cache = new ThemeCatalogCache(_cachePath);
        cache.Save(data);

        // Non-existent dir matches null timestamp
        Assert.True(cache.IsValid(dirs));
    }

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var cache = new ThemeCatalogCache(_cachePath);
        var data = new ThemeCatalogData
        {
            CacheVersion = 1,
            DirectoryTimestamps = new Dictionary<string, string?>
            {
                ["/some/path"] = "2026-03-25T10:00:00.0000000Z"
            },
            ThemeFiles = new Dictionary<string, string>
            {
                ["org.test.dark"] = "/some/path/dark.json",
                ["org.test.light"] = "/some/path/light.json"
            }
        };

        cache.Save(data);
        var loaded = cache.Load();

        Assert.NotNull(loaded);
        Assert.Equal(1, loaded.CacheVersion);
        Assert.Single(loaded.DirectoryTimestamps);
        Assert.Equal(2, loaded.ThemeFiles.Count);
        Assert.Equal("/some/path/dark.json", loaded.ThemeFiles["org.test.dark"]);
    }

    [Fact]
    public void Load_ReturnsNull_WhenFileDoesNotExist()
    {
        var cache = new ThemeCatalogCache(Path.Combine(_tempDir, "nonexistent.json"));
        Assert.Null(cache.Load());
    }

    [Fact]
    public void GetDirectoryTimestamp_ReturnsNull_ForNonExistentDirectory()
    {
        var result = ThemeCatalogCache.GetDirectoryTimestamp(Path.Combine(_tempDir, "nope"));
        Assert.Null(result);
    }

    [Fact]
    public void GetDirectoryTimestamp_ReturnsValue_ForExistingDirectory()
    {
        var dir = Path.Combine(_tempDir, "exists");
        Directory.CreateDirectory(dir);
        var result = ThemeCatalogCache.GetDirectoryTimestamp(dir);
        Assert.NotNull(result);
    }

    [Fact]
    public void TimestampsMatch_ReturnsFalse_WhenCountDiffers()
    {
        var cached = new Dictionary<string, string?> { ["a"] = "ts1" };
        var dirs = new List<string> { "a", "b" };
        Assert.False(ThemeCatalogCache.TimestampsMatch(cached, dirs));
    }

    [Fact]
    public void TimestampsMatch_ReturnsFalse_WhenKeyMissing()
    {
        var cached = new Dictionary<string, string?> { ["a"] = "ts1" };
        var dirs = new List<string> { "b" };
        Assert.False(ThemeCatalogCache.TimestampsMatch(cached, dirs));
    }
}
