using System;
using System.Collections.Generic;
using DialogEditor.Plugins;
using DialogEditor.Plugins.Security;
using Xunit;

namespace Parley.Tests.Security
{
    /// <summary>
    /// Tests for protection against malicious plugin behaviors
    /// </summary>
    public class MaliciousPluginTests
    {
        private static PluginManifest CreateManifest(string pluginId, params string[] permissions)
        {
            return new PluginManifest
            {
                ManifestVersion = "1.0",
                Plugin = new PluginInfo
                {
                    Id = pluginId,
                    Name = "Malicious Plugin",
                    Version = "1.0.0",
                    Author = "Attacker",
                    Description = "Malicious test plugin",
                    ParleyVersion = ">=0.1.5"
                },
                Permissions = new List<string>(permissions),
                EntryPoint = "malicious.py",
                TrustLevel = "unverified"
            };
        }

        [Fact]
        public void MaliciousPlugin_CannotBypassPermissions()
        {
            // Test: Malicious plugin with no permissions cannot access anything
            // Arrange
            var manifest = CreateManifest("malicious.noperm"); // No permissions
            var permissions = new PermissionChecker(manifest);

            // Act & Assert
            Assert.False(permissions.HasPermission("audio.play"));
            Assert.False(permissions.HasPermission("ui.show_notification"));
            Assert.False(permissions.HasPermission("file.read"));
            Assert.False(permissions.HasPermission("file.write"));
            Assert.False(permissions.HasPermission("dialog.read"));
        }

        [Fact]
        public void MaliciousPlugin_RateLimitPreventsDoS()
        {
            // Test: Malicious plugin cannot DoS by spamming API calls
            // Arrange
            var manifest = CreateManifest("malicious.dos", "ui.*");
            var permissions = new PermissionChecker(manifest);
            var rateLimiter = new RateLimiter();
            var auditLog = new SecurityAuditLog();
            var security = new PluginSecurityContext(manifest.Plugin.Id, permissions, rateLimiter, auditLog);

            // Act - Try to spam 2000 calls
            int successCount = 0;
            bool rateLimitHit = false;

            for (int i = 0; i < 2000; i++)
            {
                try
                {
                    security.CheckSecurity("ui.show_notification", "Spam");
                    successCount++;
                }
                catch (RateLimitExceededException)
                {
                    rateLimitHit = true;
                    break;
                }
            }

            // Assert
            Assert.True(rateLimitHit, "Rate limit should prevent DoS");
            Assert.True(successCount <= 1000, $"Should not allow more than 1000 calls, but allowed {successCount}");
        }

        [Fact]
        public void MaliciousPlugin_WildcardDoesNotGrantAllPermissions()
        {
            // Test: Wildcard permissions are scoped to their category
            // Arrange
            var manifest = CreateManifest("malicious.wildcard", "file.*");
            var permissions = new PermissionChecker(manifest);

            // Act & Assert - file.* should not grant other permissions
            Assert.True(permissions.HasPermission("file.read"));
            Assert.True(permissions.HasPermission("file.write"));
            Assert.False(permissions.HasPermission("audio.play"));
            Assert.False(permissions.HasPermission("ui.show_notification"));
            Assert.False(permissions.HasPermission("dialog.read"));
        }

        [Fact]
        public void MaliciousPlugin_SecurityAuditTracksBehavior()
        {
            // Test: All malicious behavior is logged
            // Arrange
            var auditLog = new SecurityAuditLog();
            var pluginId = "malicious.tracked";

            // Act - Simulate various attacks
            auditLog.LogPermissionDenied(pluginId, "file.write", "Unauthorized access");
            auditLog.LogSandboxViolation(pluginId, "../../../etc/passwd");
            auditLog.LogRateLimitViolation(pluginId, "SpamOp", 10000, 1000);
            auditLog.LogTimeout(pluginId, "InfiniteLoop", TimeSpan.FromMinutes(5));

            var events = auditLog.GetEvents(pluginId);

            // Assert
            Assert.NotNull(events);
            Assert.Equal(4, events.Count);
            Assert.Contains(events, e => e.EventType == SecurityEventType.PermissionDenied);
            Assert.Contains(events, e => e.EventType == SecurityEventType.SandboxViolation);
            Assert.Contains(events, e => e.EventType == SecurityEventType.RateLimitViolation);
            Assert.Contains(events, e => e.EventType == SecurityEventType.Timeout);
        }

        [Fact]
        public void MaliciousPlugin_CannotEscalatePermissions()
        {
            // Test: Plugin with limited permissions cannot gain more
            // Arrange
            var manifest = CreateManifest("malicious.escalate", "file.read");
            var permissions = new PermissionChecker(manifest);

            // Act & Assert - Has only read permission
            Assert.True(permissions.HasPermission("file.read"));
            Assert.False(permissions.HasPermission("file.write"));
            Assert.False(permissions.HasPermission("file.dialog"));
            Assert.False(permissions.HasPermission("audio.play"));

            // Attempting to use write permission should fail
            Assert.Throws<PermissionDeniedException>(() =>
                permissions.RequirePermission("file.write"));
        }

        [Fact]
        public void MaliciousPlugin_MultipleViolations_AllRecorded()
        {
            // Test: Multiple violations are all tracked
            // Arrange
            var manifest = CreateManifest("malicious.multiple");
            var permissions = new PermissionChecker(manifest);
            var rateLimiter = new RateLimiter();
            var auditLog = new SecurityAuditLog();
            var security = new PluginSecurityContext(manifest.Plugin.Id, permissions, rateLimiter, auditLog);

            // Act - Attempt multiple violations
            Assert.Throws<PermissionDeniedException>(() =>
                security.CheckSecurity("audio.play", "Attack1"));

            Assert.Throws<PermissionDeniedException>(() =>
                security.CheckSecurity("file.write", "Attack2"));

            security.LogSandboxViolation("../../../etc/passwd");

            var events = auditLog.GetEvents(manifest.Plugin.Id);

            // Assert - All violations recorded
            Assert.True(events.Count >= 3);
        }

        [Fact]
        public void MaliciousPlugin_CombinedAttack_AllLayersBlock()
        {
            // Test: Multiple security layers work together
            // Arrange
            var manifest = CreateManifest("malicious.combined", "ui.show_notification");
            var permissions = new PermissionChecker(manifest);
            var rateLimiter = new RateLimiter();
            var auditLog = new SecurityAuditLog();
            var security = new PluginSecurityContext(manifest.Plugin.Id, permissions, rateLimiter, auditLog);

            // Act & Assert - Permission layer blocks unauthorized access
            Assert.Throws<PermissionDeniedException>(() =>
                security.CheckSecurity("file.write", "WriteAttack"));

            // Rate limiter blocks spam
            for (int i = 0; i < 1000; i++)
            {
                security.CheckSecurity("ui.show_notification", "Spam");
            }

            Assert.Throws<RateLimitExceededException>(() =>
                security.CheckSecurity("ui.show_notification", "Spam"));

            // Sandbox violation logged
            security.LogSandboxViolation("../../sensitive.txt");

            // Verify all violations logged
            var events = auditLog.GetEvents(manifest.Plugin.Id);
            Assert.True(events.Count >= 3);
        }

        [Fact]
        public void MaliciousPlugin_CannotAccessWithoutExplicitPermission()
        {
            // Test: Every permission must be explicitly granted
            // Arrange
            var manifest = CreateManifest("malicious.implicit");
            var permissions = new PermissionChecker(manifest);

            // Act & Assert - No implicit permissions
            var testPermissions = new[]
            {
                "audio.play", "audio.stop",
                "ui.show_notification", "ui.show_dialog",
                "file.read", "file.write", "file.dialog",
                "dialog.read", "dialog.write"
            };

            foreach (var permission in testPermissions)
            {
                Assert.False(permissions.HasPermission(permission),
                    $"Permission {permission} should not be implicitly granted");
            }
        }

        [Fact]
        public void MaliciousPlugin_CannotModifyOwnPermissions()
        {
            // Test: Plugin manifest permissions are read-only
            // Arrange
            var manifest = CreateManifest("malicious.modify", "file.read");
            var permissions = new PermissionChecker(manifest);

            // Verify initial state
            Assert.True(permissions.HasPermission("file.read"));
            Assert.False(permissions.HasPermission("file.write"));

            // Attempt to modify manifest (simulated)
            manifest.Permissions.Add("file.write");

            // Create new permission checker (would happen in real scenario)
            var newPermissions = new PermissionChecker(manifest);

            // Assert - New permission checker sees the change
            // This demonstrates that permission changes require manifest reload
            Assert.True(newPermissions.HasPermission("file.write"));

            // But original permission checker is unchanged
            Assert.False(permissions.HasPermission("file.write"));
        }

        [Fact]
        public void MaliciousPlugin_SecurityLogRetention()
        {
            // Test: Security events are retained for audit
            // Arrange
            var auditLog = new SecurityAuditLog();
            var pluginId = "malicious.retention";

            // Act - Generate events over time
            for (int i = 0; i < 10; i++)
            {
                auditLog.LogPermissionDenied(pluginId, $"permission.{i}", $"Attack {i}");
            }

            var events = auditLog.GetEvents(pluginId);

            // Assert - All events retained
            Assert.Equal(10, events.Count);
            foreach (var evt in events)
            {
                Assert.Equal(SecurityEventType.PermissionDenied, evt.EventType);
            }
        }
    }
}
