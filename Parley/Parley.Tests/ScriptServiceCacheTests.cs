using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using DialogEditor.Services;

namespace Parley.Tests
{
    public class ScriptServiceCacheTests
    {
        [Fact]
        public async Task GetParameterDeclarations_CachesResults()
        {
            // Arrange
            var service = ScriptService.Instance;
            service.ClearCache();

            var scriptName = "sc_base_item"; // Real test script with declarations

            // Act - First call should parse and cache
            var firstCall = await service.GetParameterDeclarationsAsync(scriptName);
            var stats1 = service.GetCacheStats();

            // Second call should return from cache
            var secondCall = await service.GetParameterDeclarationsAsync(scriptName);
            var stats2 = service.GetCacheStats();

            // Assert
            Assert.NotNull(firstCall);
            Assert.NotNull(secondCall);
            Assert.Same(firstCall, secondCall); // Should be same object from cache
            Assert.Equal(1, stats2.ParameterCount); // Should have 1 cached parameter declaration
        }

        [Fact]
        public async Task GetScriptContent_CachesResults()
        {
            // Arrange
            var service = ScriptService.Instance;
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
            var service = ScriptService.Instance;

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
            var service = ScriptService.Instance;
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
            var service = ScriptService.Instance;
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
            var service = ScriptService.Instance;

            // Act
            var nullResult = await service.GetScriptContentAsync(null);
            var emptyResult = await service.GetScriptContentAsync("");
            var whitespaceResult = await service.GetScriptContentAsync("   ");

            // Assert
            Assert.Null(nullResult);
            Assert.Null(emptyResult);
            Assert.Null(whitespaceResult);
        }
    }
}
