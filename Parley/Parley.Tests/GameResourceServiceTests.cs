using System;
using System.IO;
using DialogEditor.Services;
using Parley.Tests.Mocks;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Tests for GameResourceService: initialization, TLK lookups,
    /// disposal, and graceful degradation when game paths are missing.
    /// </summary>
    public class GameResourceServiceTests : IDisposable
    {
        private readonly MockSettingsService _settings;

        public GameResourceServiceTests()
        {
            _settings = new MockSettingsService();
        }

        public void Dispose()
        {
            // Nothing to clean up
        }

        #region Initialization

        [Fact]
        public void Constructor_NullSettings_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new GameResourceService(null!));
        }

        [Fact]
        public void IsAvailable_NoGamePath_ReturnsFalse()
        {
            _settings.BaseGameInstallPath = "";
            _settings.NeverwinterNightsPath = "";

            using var service = new GameResourceService(_settings);

            Assert.False(service.IsAvailable);
        }

        [Fact]
        public void IsAvailable_NonexistentPath_ReturnsFalse()
        {
            _settings.BaseGameInstallPath = Path.Combine(Path.GetTempPath(), "nonexistent_nwn_12345");

            using var service = new GameResourceService(_settings);

            Assert.False(service.IsAvailable);
        }

        #endregion

        #region TLK Lookups Without Game Data

        [Fact]
        public void GetTlkString_NoResolver_ReturnsNull()
        {
            _settings.BaseGameInstallPath = "";
            using var service = new GameResourceService(_settings);

            var result = service.GetTlkString(0);

            Assert.Null(result);
        }

        [Fact]
        public void GetTlkString_MaxStrRef_ReturnsNull()
        {
            // 0xFFFFFFFF means "no StrRef" (custom text)
            _settings.BaseGameInstallPath = "";
            using var service = new GameResourceService(_settings);

            var result = service.GetTlkString(0xFFFFFFFF);

            Assert.Null(result);
        }

        [Fact]
        public void GetTlkEntry_MaxStrRef_ReturnsNull()
        {
            _settings.BaseGameInstallPath = "";
            using var service = new GameResourceService(_settings);

            var result = service.GetTlkEntry(0xFFFFFFFF);

            Assert.Null(result);
        }

        [Fact]
        public void GetTlkEntry_NoResolver_ReturnsNull()
        {
            _settings.BaseGameInstallPath = "";
            using var service = new GameResourceService(_settings);

            var result = service.GetTlkEntry(100);

            Assert.Null(result);
        }

        #endregion

        #region Resource Lookups Without Game Data

        [Fact]
        public void ListBuiltInScripts_NoResolver_ReturnsEmpty()
        {
            _settings.BaseGameInstallPath = "";
            using var service = new GameResourceService(_settings);

            var scripts = service.ListBuiltInScripts();

            Assert.Empty(scripts);
        }

        [Fact]
        public void FindResource_NoResolver_ReturnsNull()
        {
            _settings.BaseGameInstallPath = "";
            using var service = new GameResourceService(_settings);

            var result = service.FindResource("nw_s0_fireball", 2009);

            Assert.Null(result);
        }

        #endregion

        #region InvalidateResolver

        [Fact]
        public void InvalidateResolver_NoResolver_DoesNotThrow()
        {
            _settings.BaseGameInstallPath = "";
            using var service = new GameResourceService(_settings);

            service.InvalidateResolver();
            // Should not throw even when resolver is null
        }

        [Fact]
        public void InvalidateResolver_AfterAccess_AllowsReinitialization()
        {
            _settings.BaseGameInstallPath = "";
            using var service = new GameResourceService(_settings);

            // Access to trigger lazy init (returns null since path is empty)
            _ = service.IsAvailable;

            // Invalidate should not throw
            service.InvalidateResolver();

            // Should be able to query again
            Assert.False(service.IsAvailable);
        }

        #endregion

        #region Disposal

        [Fact]
        public void Dispose_MultipleDispose_DoesNotThrow()
        {
            var service = new GameResourceService(_settings);
            service.Dispose();
            service.Dispose(); // Second dispose should be safe
        }

        [Fact]
        public void Dispose_UnsubscribesFromSettingsChanges()
        {
            var service = new GameResourceService(_settings);
            service.Dispose();

            // Changing settings after dispose should not cause issues
            _settings.BaseGameInstallPath = "/some/new/path";
            // If PropertyChanged handler wasn't unsubscribed, this could
            // try to access disposed resources
        }

        #endregion
    }
}
