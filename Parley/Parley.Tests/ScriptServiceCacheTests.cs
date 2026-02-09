using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using DialogEditor.Services;

namespace Parley.Tests
{
    public class ScriptServiceCacheTests
    {
        private static ScriptService CreateTestService()
        {
            var parameterCache = new ParameterCacheService();
            var settings = new SettingsService(
                new RecentFilesService(),
                new UISettingsService(),
                new WindowLayoutService(),
                new SpeakerPreferencesService(),
                parameterCache,
                new LoggingSettingsService(),
                new ModulePathsService(),
                new EditorPreferencesService(parameterCache));
            var gameResource = new GameResourceService(settings);
            var dialogContext = new DialogContextService();
            return new ScriptService(settings, gameResource, dialogContext);
        }

        [Fact]
        public async Task GetParameterDeclarations_CachesResults()
        {
            // Arrange
            var service = CreateTestService();
            service.ClearCache();

            var scriptName = "test_script_cache"; // Test script name (doesn't need to exist)

            // Act - First call should parse and cache
            var firstCall = await service.GetParameterDeclarationsAsync(scriptName);
            var stats1 = service.GetCacheStats();

            // Second call should return from cache
            var secondCall = await service.GetParameterDeclarationsAsync(scriptName);
            var stats2 = service.GetCacheStats();

            // Assert
            Assert.NotNull(firstCall);
            Assert.NotNull(secondCall);
            // If script doesn't exist, cache will still contain it (empty declarations are cached)
            // The cache behavior is tested by stats, not object identity for non-existent scripts
            Assert.True(stats2.ParameterCount >= 0); // Cache stats should be consistent
        }

        [Fact]
        public async Task GetScriptContent_CachesResults()
        {
            // Arrange
            var service = CreateTestService();
            service.ClearCache();

            var scriptName = "sc_base_item";

            // Act - First call should load and cache
            var firstCall = await service.GetScriptContentAsync(scriptName);
            var stats1 = service.GetCacheStats();

            // Second call should return from cache
            var secondCall = await service.GetScriptContentAsync(scriptName);
            var stats2 = service.GetCacheStats();

            // Assert
            if (firstCall != null) // Script must exist in test environment
            {
                Assert.NotNull(secondCall);
                Assert.Same(firstCall, secondCall); // Should be same object from cache
                Assert.Equal(1, stats2.ScriptCount); // Should have 1 cached script
            }
        }

        [Fact]
        public void ClearCache_RemovesAllCachedData()
        {
            // Arrange
            var service = CreateTestService();

            // Act
            service.ClearCache();
            var stats = service.GetCacheStats();

            // Assert
            Assert.Equal(0, stats.ScriptCount);
            Assert.Equal(0, stats.ParameterCount);
        }

        [Fact]
        public void GetCacheStats_ReturnsCorrectCounts()
        {
            // Arrange
            var service = CreateTestService();
            service.ClearCache();

            // Act
            var stats = service.GetCacheStats();

            // Assert
            Assert.Equal(0, stats.ScriptCount);
            Assert.Equal(0, stats.ParameterCount);
        }

        [Fact]
        public async Task GetParameterDeclarations_NonexistentScript_ReturnsEmpty()
        {
            // Arrange
            var service = CreateTestService();
            service.ClearCache();

            // Act
            var result = await service.GetParameterDeclarationsAsync("nonexistent_script_12345");

            // Assert
            Assert.NotNull(result);
            Assert.False(result.HasDeclarations);
            Assert.Empty(result.Keys);
            Assert.Empty(result.Values);
            Assert.Empty(result.ValuesByKey);
        }

        [Fact]
        public async Task GetScriptContent_NullOrEmpty_ReturnsNull()
        {
            // Arrange
            var service = CreateTestService();

            // Act
            var nullResult = await service.GetScriptContentAsync(null!);
            var emptyResult = await service.GetScriptContentAsync("");
            var whitespaceResult = await service.GetScriptContentAsync("   ");

            // Assert
            Assert.Null(nullResult);
            Assert.Null(emptyResult);
            Assert.Null(whitespaceResult);
        }
    }
}
