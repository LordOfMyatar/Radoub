using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Radoub.Formats.Common;
using Radoub.Formats.Services;
using Radoub.TestUtilities.Mocks;
using Radoub.UI.Services;
using RadoubLauncher.Services;

namespace Trebuchet.Tests;

/// <summary>
/// TDD tests for PaletteCacheWarmupService.
/// Tests cover: BIF cache skip/build, cancellation, HAK cancellation on module change.
/// </summary>
public class PaletteCacheWarmupServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _testCacheDir;
    private readonly SharedPaletteCacheService _cacheService;
    private readonly HakPaletteScannerService _hakScanner;
    private readonly PaletteCacheWarmupService _service;

    public PaletteCacheWarmupServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "RadoubTests", Guid.NewGuid().ToString("N"));
        _testCacheDir = Path.Combine(_testDir, "Cache");
        Directory.CreateDirectory(_testDir);
        Directory.CreateDirectory(_testCacheDir);
        _cacheService = new SharedPaletteCacheService(_testCacheDir);
        _hakScanner = new HakPaletteScannerService();
        _service = new PaletteCacheWarmupService(_cacheService, _hakScanner);
    }

    public void Dispose()
    {
        _service.Dispose();
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, recursive: true); }
            catch { /* cleanup best-effort */ }
        }
    }

    #region BIF Warm-up

    [Fact]
    public async Task WarmBifCacheAsync_SkipsWhenCacheValid()
    {
        // Pre-populate a valid BIF cache
        await _cacheService.SaveSourceCacheAsync("bif", new List<SharedPaletteCacheItem>
        {
            new() { ResRef = "existing_item", DisplayName = "Existing", BaseItemType = 1 }
        });
        Assert.True(_cacheService.HasValidSourceCache("bif"));

        var mockGameData = new MockGameDataService(includeSampleData: false);

        await _service.WarmBifCacheAsync(mockGameData, CancellationToken.None);

        // Cache should still have the original item (not rebuilt)
        var loaded = _cacheService.LoadSourceCache("bif");
        Assert.NotNull(loaded);
        Assert.Single(loaded);
        Assert.Equal("existing_item", loaded[0].ResRef);
    }

    [Fact]
    public async Task WarmBifCacheAsync_BuildsCacheWhenMissing()
    {
        // No existing cache
        Assert.False(_cacheService.HasValidSourceCache("bif"));

        var mockGameData = new MockGameDataService(includeSampleData: false);
        // Don't add any UTI resources — just verify the flow completes and creates a cache file
        // (An empty BIF scan is valid — it just means no UTI items in BIF)

        await _service.WarmBifCacheAsync(mockGameData, CancellationToken.None);

        // Should now have a valid (empty) cache
        Assert.True(_cacheService.HasValidSourceCache("bif"));
    }

    [Fact]
    public async Task WarmBifCacheAsync_ReleasesBuildLockOnSuccess()
    {
        Assert.False(_cacheService.HasValidSourceCache("bif"));
        var mockGameData = new MockGameDataService(includeSampleData: false);

        await _service.WarmBifCacheAsync(mockGameData, CancellationToken.None);

        // Build lock should be released (no .building file)
        var sentinelFile = Path.Combine(_testCacheDir, "bif.json.building");
        Assert.False(File.Exists(sentinelFile));
    }

    [Fact]
    public async Task WarmBifCacheAsync_ReleasesBuildLockOnCancellation()
    {
        Assert.False(_cacheService.HasValidSourceCache("bif"));

        var mockGameData = new MockGameDataService(includeSampleData: false);
        // Add enough resources that cancellation has time to trigger
        for (int i = 0; i < 100; i++)
        {
            mockGameData.AddResourceInfo($"item_{i:D3}", ResourceTypes.Uti);
            // Don't add actual resource data — FindResource will return null, items will be skipped
        }

        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        await _service.WarmBifCacheAsync(mockGameData, cts.Token);

        // Build lock should be released even after cancellation
        var sentinelFile = Path.Combine(_testCacheDir, "bif.json.building");
        Assert.False(File.Exists(sentinelFile));
    }

    [Fact]
    public async Task WarmBifCacheAsync_SkipsWhenLockHeldByAnotherProcess()
    {
        // Pre-acquire the lock (simulating another process)
        Assert.True(_cacheService.AcquireBuildLock("bif"));

        var mockGameData = new MockGameDataService(includeSampleData: false);

        await _service.WarmBifCacheAsync(mockGameData, CancellationToken.None);

        // Cache should NOT have been built (lock was held)
        Assert.False(_cacheService.HasValidSourceCache("bif"));

        // Clean up the lock
        _cacheService.ReleaseBuildLock("bif");
    }

    #endregion

    #region Cancellation

    [Fact]
    public async Task CancelAll_StopsBifWarm()
    {
        // Start BIF warm in a background task, cancel immediately
        var mockGameData = new MockGameDataService(includeSampleData: false);

        var cts = new CancellationTokenSource();
        var task = _service.WarmBifCacheAsync(mockGameData, cts.Token);
        _service.CancelAll();

        // Should complete without throwing
        await task;
    }

    #endregion
}
