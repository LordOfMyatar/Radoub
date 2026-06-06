using System;
using System.IO;
using System.Reflection;
using DialogEditor.Services;
using Xunit;

namespace DialogEditor.Tests
{
    /// <summary>
    /// Round-trip persistence coverage for Parley SettingsService bool flags (#2361).
    ///
    /// Most checkbox state lands in a SettingsService bool that is serialized to
    /// ParleySettings.json. Before this suite none of the 19 bools had a
    /// save→reload→assert test, so a serialization rename or default flip would
    /// ship silently. Each test flips a bool to its NON-default value, lets the
    /// service persist, constructs a fresh service against the same isolated
    /// directory, and asserts the value survived.
    /// </summary>
    public class SettingsBoolPersistenceTests : IDisposable
    {
        private readonly string _testSettingsDir;
        private readonly string? _originalEnv;

        public SettingsBoolPersistenceTests()
        {
            _testSettingsDir = Path.Combine(Path.GetTempPath(), "ParleyBoolTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testSettingsDir);

            // Redirect SettingsService at the isolated directory via its documented
            // test-override env var (see SettingsService.SettingsDirectory).
            _originalEnv = Environment.GetEnvironmentVariable("PARLEY_SETTINGS_DIR");
            Environment.SetEnvironmentVariable("PARLEY_SETTINGS_DIR", _testSettingsDir);

            // SettingsService caches its directory in a static field on first access
            // (see SettingsService.SettingsDirectory). Without resetting it, the first
            // test's temp dir is reused by every later test in the process — fake
            // isolation that leaks state and would flake the moment two tests touched
            // the same bool. Clear it so each test binds to its own directory.
            ResetCachedSettingsDirectory();
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("PARLEY_SETTINGS_DIR", _originalEnv);
            ResetCachedSettingsDirectory();
            try
            {
                if (Directory.Exists(_testSettingsDir))
                    Directory.Delete(_testSettingsDir, recursive: true);
            }
            catch { /* best-effort cleanup */ }
        }

        private static void ResetCachedSettingsDirectory()
        {
            var field = typeof(SettingsService).GetField("_settingsDirectory",
                BindingFlags.NonPublic | BindingFlags.Static);
            field?.SetValue(null, null);
        }

        /// <summary>
        /// Builds a SettingsService with real sub-service dependencies, all sharing
        /// the isolated settings directory. A fresh instance re-reads the JSON, so
        /// two calls bracket a save→reload cycle.
        /// </summary>
        private static SettingsService NewService()
        {
            var parameterCache = new ParameterCacheService();
            return new SettingsService(
                new RecentFilesService(),
                new UISettingsService(),
                new WindowLayoutService(),
                new SpeakerPreferencesService(),
                parameterCache,
                new LoggingSettingsService(),
                new ModulePathsService(),
                new EditorPreferencesService(parameterCache));
        }

        private static void AssertRoundTrip(Action<SettingsService> set, Func<SettingsService, bool> get, bool expected)
        {
            var writer = NewService();
            set(writer);

            // A fresh service reads the persisted JSON from disk.
            var reader = NewService();
            Assert.Equal(expected, get(reader));
        }

        // ---- EditorPreferencesService-backed bools ----

        [Fact] public void AutoSaveEnabled_RoundTrips()
            => AssertRoundTrip(s => s.AutoSaveEnabled = false, s => s.AutoSaveEnabled, expected: false);

        [Fact] public void EnableNpcTagColoring_RoundTrips()
            => AssertRoundTrip(s => s.EnableNpcTagColoring = false, s => s.EnableNpcTagColoring, expected: false);

        [Fact] public void EnableParameterCache_RoundTrips()
            => AssertRoundTrip(s => s.EnableParameterCache = false, s => s.EnableParameterCache, expected: false);

        [Fact] public void SpellCheckEnabled_RoundTrips()
            => AssertRoundTrip(s => s.SpellCheckEnabled = false, s => s.SpellCheckEnabled, expected: false);

        [Fact] public void ShowDeleteConfirmation_RoundTrips()
            => AssertRoundTrip(s => s.ShowDeleteConfirmation = false, s => s.ShowDeleteConfirmation, expected: false);

        [Fact] public void SimulatorShowWarnings_RoundTrips()
            => AssertRoundTrip(s => s.SimulatorShowWarnings = false, s => s.SimulatorShowWarnings, expected: false);

        [Fact] public void SoundBrowserIncludeGameResources_RoundTrips()
            => AssertRoundTrip(s => s.SoundBrowserIncludeGameResources = false, s => s.SoundBrowserIncludeGameResources, expected: false);

        [Fact] public void SoundBrowserIncludeHakFiles_RoundTrips()
            => AssertRoundTrip(s => s.SoundBrowserIncludeHakFiles = false, s => s.SoundBrowserIncludeHakFiles, expected: false);

        [Fact] public void SoundBrowserIncludeBifFiles_RoundTrips()
            => AssertRoundTrip(s => s.SoundBrowserIncludeBifFiles = true, s => s.SoundBrowserIncludeBifFiles, expected: true);

        [Fact] public void SoundBrowserMonoOnly_RoundTrips()
            => AssertRoundTrip(s => s.SoundBrowserMonoOnly = false, s => s.SoundBrowserMonoOnly, expected: false);

        // ---- LoggingSettingsService-backed bool ----

        [Fact] public void DebugWindowVisible_RoundTrips()
            => AssertRoundTrip(s => s.DebugWindowVisible = true, s => s.DebugWindowVisible, expected: true);

        // ---- UISettingsService-backed bools ----

        [Fact] public void TreeViewWordWrap_RoundTrips()
            => AssertRoundTrip(s => s.TreeViewWordWrap = true, s => s.TreeViewWordWrap, expected: true);

        [Fact] public void ShowNodeIndexNumbers_RoundTrips()
            => AssertRoundTrip(s => s.ShowNodeIndexNumbers = true, s => s.ShowNodeIndexNumbers, expected: true);

        [Fact] public void AllowScrollbarAutoHide_RoundTrips()
            => AssertRoundTrip(s => s.AllowScrollbarAutoHide = true, s => s.AllowScrollbarAutoHide, expected: true);

        // ---- WindowLayoutService-backed bools ----

        [Fact] public void WindowMaximized_RoundTrips()
            => AssertRoundTrip(s => s.WindowMaximized = true, s => s.WindowMaximized, expected: true);

        [Fact] public void FlowchartWindowOpen_RoundTrips()
            => AssertRoundTrip(s => s.FlowchartWindowOpen = true, s => s.FlowchartWindowOpen, expected: true);

        [Fact] public void FlowchartVisible_RoundTrips()
            => AssertRoundTrip(s => s.FlowchartVisible = true, s => s.FlowchartVisible, expected: true);

        [Fact] public void DialogBrowserPanelVisible_RoundTrips()
            => AssertRoundTrip(s => s.DialogBrowserPanelVisible = false, s => s.DialogBrowserPanelVisible, expected: false);

        /// <summary>
        /// Isolation guard: a fresh service in a clean directory must see the
        /// default (true), even though other tests flip AutoSaveEnabled to false.
        /// If the static-directory cache leaks between tests this fails, which is
        /// exactly the cross-test bleed we hardened against in the constructor.
        /// </summary>
        [Fact]
        public void AutoSaveEnabled_DefaultsTrue_InCleanDirectory()
        {
            var service = NewService();
            Assert.True(service.AutoSaveEnabled);
        }
    }
}
