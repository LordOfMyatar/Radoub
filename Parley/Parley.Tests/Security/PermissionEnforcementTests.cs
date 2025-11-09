using System;
using System.Collections.Generic;
using DialogEditor.Plugins;
using DialogEditor.Plugins.Security;
using Xunit;

namespace Parley.Tests.Security
{
    /// <summary>
    /// Tests for permission enforcement across all plugin APIs
    /// </summary>
    public class PermissionEnforcementTests
    {
        private static PluginManifest CreateManifest(string pluginId, params string[] permissions)
        {
            return new PluginManifest
            {
                ManifestVersion = "1.0",
                Plugin = new PluginInfo
                {
                    Id = pluginId,
                    Name = "Test Plugin",
                    Version = "1.0.0",
                    Author = "Test",
                    Description = "Test plugin",
                    ParleyVersion = ">=0.1.5"
                },
                Permissions = new List<string>(permissions),
                EntryPoint = "test.py",
                TrustLevel = "unverified"
            };
        }

        [Fact]
        public void PermissionChecker_WithoutPermission_DeniesAccess()
        {
            // Arrange
            var manifest = CreateManifest("test.denied");
            var permissions = new PermissionChecker(manifest);

            // Act & Assert
            Assert.False(permissions.HasPermission("audio.play"));
            Assert.False(permissions.HasPermission("ui.show_notification"));
            Assert.False(permissions.HasPermission("file.read"));
            Assert.False(permissions.HasPermission("dialog.read"));
        }

        [Fact]
        public void PermissionChecker_WithSpecificPermission_AllowsOnlyThatPermission()
        {
            // Arrange
            var manifest = CreateManifest("test.specific", "audio.play");
            var permissions = new PermissionChecker(manifest);

            // Act & Assert
            Assert.True(permissions.HasPermission("audio.play"));
            Assert.False(permissions.HasPermission("audio.stop"));
            Assert.False(permissions.HasPermission("ui.show_notification"));
            Assert.False(permissions.HasPermission("file.read"));
        }

        [Fact]
        public void PermissionChecker_WithWildcard_AllowsCategoryPermissions()
        {
            // Arrange
            var manifest = CreateManifest("test.wildcard", "ui.*");
            var permissions = new PermissionChecker(manifest);

            // Act & Assert
            Assert.True(permissions.HasPermission("ui.show_notification"));
            Assert.True(permissions.HasPermission("ui.show_dialog"));
            Assert.True(permissions.HasPermission("ui.custom_action"));
            Assert.False(permissions.HasPermission("audio.play"));
            Assert.False(permissions.HasPermission("file.read"));
        }

        [Fact]
        public void PermissionChecker_MultiplePermissions_AllowsAll()
        {
            // Arrange
            var manifest = CreateManifest("test.multiple", "audio.play", "ui.show_notification", "file.read");
            var permissions = new PermissionChecker(manifest);

            // Act & Assert
            Assert.True(permissions.HasPermission("audio.play"));
            Assert.True(permissions.HasPermission("ui.show_notification"));
            Assert.True(permissions.HasPermission("file.read"));
            Assert.False(permissions.HasPermission("file.write"));
            Assert.False(permissions.HasPermission("dialog.read"));
        }

        [Fact]
        public void PermissionChecker_RequirePermission_ThrowsWhenDenied()
        {
            // Arrange
            var manifest = CreateManifest("test.require");
            var permissions = new PermissionChecker(manifest);

            // Act & Assert
            var ex = Assert.Throws<PermissionDeniedException>(
                () => permissions.RequirePermission("audio.play"));

            Assert.Contains("audio.play", ex.Message);
        }

        [Fact]
        public void PermissionChecker_RequirePermission_SucceedsWhenAllowed()
        {
            // Arrange
            var manifest = CreateManifest("test.require.allowed", "audio.play");
            var permissions = new PermissionChecker(manifest);

            // Act & Assert - Should not throw
            permissions.RequirePermission("audio.play");
        }

        [Fact]
        public void SecurityContext_CheckSecurity_EnforcesPermissionsAndRateLimit()
        {
            // Arrange
            var manifest = CreateManifest("test.security", "audio.play");
            var permissions = new PermissionChecker(manifest);
            var rateLimiter = new RateLimiter();
            var auditLog = new SecurityAuditLog();
            var security = new PluginSecurityContext(manifest.Plugin.Id, permissions, rateLimiter, auditLog);

            // Act & Assert - Should succeed with permission
            security.CheckSecurity("audio.play", "TestOperation");

            // Should fail without permission
            Assert.Throws<PermissionDeniedException>(
                () => security.CheckSecurity("ui.show_notification", "TestOperation"));
        }

        [Fact]
        public void SecurityContext_CheckSecurity_EnforcesRateLimit()
        {
            // Arrange
            var manifest = CreateManifest("test.ratelimit.security", "audio.*");
            var permissions = new PermissionChecker(manifest);
            var rateLimiter = new RateLimiter();
            var auditLog = new SecurityAuditLog();
            var security = new PluginSecurityContext(manifest.Plugin.Id, permissions, rateLimiter, auditLog);

            // Act - Make 1000 calls (at limit)
            for (int i = 0; i < 1000; i++)
            {
                security.CheckSecurity("audio.play", "TestOperation");
            }

            // Assert - 1001st call should throw rate limit exception
            Assert.Throws<RateLimitExceededException>(
                () => security.CheckSecurity("audio.play", "TestOperation"));
        }

        [Fact]
        public void SecurityContext_LogSandboxViolation_RecordsEvent()
        {
            // Arrange
            var manifest = CreateManifest("test.sandbox.log", "file.*");
            var permissions = new PermissionChecker(manifest);
            var rateLimiter = new RateLimiter();
            var auditLog = new SecurityAuditLog();
            var security = new PluginSecurityContext(manifest.Plugin.Id, permissions, rateLimiter, auditLog);

            // Act
            security.LogSandboxViolation("../../../etc/passwd");

            // Assert - Should complete without exception
            var events = auditLog.GetEvents(manifest.Plugin.Id);
            Assert.NotEmpty(events);
            Assert.Contains(events, e => e.EventType == SecurityEventType.SandboxViolation);
        }

        [Fact]
        public void PermissionChecker_CaseSensitive_MatchesExactly()
        {
            // Arrange
            var manifest = CreateManifest("test.case", "Audio.Play"); // Wrong case
            var permissions = new PermissionChecker(manifest);

            // Act & Assert - Should be case-sensitive
            Assert.False(permissions.HasPermission("audio.play"));
        }

        [Fact]
        public void PermissionChecker_EmptyPermissions_DeniesAll()
        {
            // Arrange
            var manifest = CreateManifest("test.empty");
            var permissions = new PermissionChecker(manifest);

            // Act & Assert
            Assert.False(permissions.HasPermission("audio.play"));
            Assert.False(permissions.HasPermission("ui.show_notification"));
            Assert.False(permissions.HasPermission("file.read"));
            Assert.False(permissions.HasPermission("dialog.read"));
        }

        [Fact]
        public void PermissionChecker_WildcardDoesNotGrantOtherCategories()
        {
            // Arrange
            var manifest = CreateManifest("test.wildcard.scoped", "file.*");
            var permissions = new PermissionChecker(manifest);

            // Act & Assert
            Assert.True(permissions.HasPermission("file.read"));
            Assert.True(permissions.HasPermission("file.write"));
            Assert.True(permissions.HasPermission("file.dialog"));
            Assert.False(permissions.HasPermission("audio.play"));
            Assert.False(permissions.HasPermission("ui.show_notification"));
            Assert.False(permissions.HasPermission("dialog.read"));
        }
    }
}
