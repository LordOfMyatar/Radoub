using DialogEditor.Services;
using Xunit;

namespace DialogEditor.Tests
{
    /// <summary>
    /// Tests for the one-time migration of Parley's local ExternalEditorPath/ManifestPath
    /// values onto the shared RadoubSettings keys (#2357). The migration rule mirrors
    /// Trebuchet's (#2295): adopt the legacy value only when the shared value is still empty,
    /// so a value set elsewhere is never clobbered.
    /// </summary>
    public class SettingsPathMigrationTests
    {
        [Fact]
        public void AdoptLegacyIfSharedEmpty_SharedEmpty_AdoptsLegacy()
        {
            var result = SettingsPathMigration.AdoptLegacyIfSharedEmpty("C:/legacy/editor.exe", "");
            Assert.Equal("C:/legacy/editor.exe", result);
        }

        [Fact]
        public void AdoptLegacyIfSharedEmpty_SharedSet_KeepsShared()
        {
            var result = SettingsPathMigration.AdoptLegacyIfSharedEmpty("C:/legacy/editor.exe", "C:/shared/editor.exe");
            Assert.Equal("C:/shared/editor.exe", result);
        }

        [Fact]
        public void AdoptLegacyIfSharedEmpty_BothEmpty_StaysEmpty()
        {
            var result = SettingsPathMigration.AdoptLegacyIfSharedEmpty("", "");
            Assert.Equal("", result);
        }

        [Fact]
        public void AdoptLegacyIfSharedEmpty_LegacyEmptySharedSet_KeepsShared()
        {
            var result = SettingsPathMigration.AdoptLegacyIfSharedEmpty("", "C:/shared/editor.exe");
            Assert.Equal("C:/shared/editor.exe", result);
        }

        [Fact]
        public void AdoptLegacyIfSharedEmpty_NullLegacy_TreatedAsEmpty()
        {
            var result = SettingsPathMigration.AdoptLegacyIfSharedEmpty(null, "");
            Assert.Equal("", result);
        }

        [Fact]
        public void AdoptLegacyIfSharedEmpty_NullShared_TreatedAsEmptyAndAdoptsLegacy()
        {
            var result = SettingsPathMigration.AdoptLegacyIfSharedEmpty("C:/legacy/editor.exe", null);
            Assert.Equal("C:/legacy/editor.exe", result);
        }
    }
}
