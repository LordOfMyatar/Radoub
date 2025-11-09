using System;
using System.Collections.Generic;
using System.Threading;
using DialogEditor.Plugins;
using DialogEditor.Plugins.Security;
using Xunit;

namespace Parley.Tests.Security
{
    /// <summary>
    /// Tests for rate limiting enforcement
    /// </summary>
    public class RateLimitTests
    {
        private static PluginManifest CreateManifestWithPermissions(string pluginId, params string[] permissions)
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
        public void RateLimiter_AllowsCallsUnderLimit()
        {
            // Arrange
            var rateLimiter = new RateLimiter();
            var pluginId = "test.ratelimit.under";
            var operation = "TestOperation";

            // Act - Make 100 calls (well under 1000/min limit)
            for (int i = 0; i < 100; i++)
            {
                Assert.True(rateLimiter.AllowCall(pluginId, operation));
            }

            // Assert - All calls should be allowed
        }

        [Fact]
        public void RateLimiter_BlocksCallsOverLimit()
        {
            // Arrange
            var rateLimiter = new RateLimiter();
            var pluginId = "test.ratelimit.over";
            var operation = "TestOperation";

            // Act - Make 1000 calls (at limit)
            for (int i = 0; i < 1000; i++)
            {
                Assert.True(rateLimiter.AllowCall(pluginId, operation));
            }

            // Assert - 1001st call should be blocked
            Assert.False(rateLimiter.AllowCall(pluginId, operation));
        }

        [Fact]
        public void RateLimiter_TracksPerPlugin()
        {
            // Arrange
            var rateLimiter = new RateLimiter();

            // Act - Make 500 calls for each plugin
            for (int i = 0; i < 500; i++)
            {
                Assert.True(rateLimiter.AllowCall("test.plugin1", "Operation"));
                Assert.True(rateLimiter.AllowCall("test.plugin2", "Operation"));
            }

            // Assert - Both plugins should still be under their individual limits
            Assert.True(rateLimiter.AllowCall("test.plugin1", "Operation"));
            Assert.True(rateLimiter.AllowCall("test.plugin2", "Operation"));
        }

        [Fact]
        public void RateLimiter_TracksPerOperation()
        {
            // Arrange
            var rateLimiter = new RateLimiter();
            var pluginId = "test.ratelimit.multiop";

            // Act - Make 500 calls for each operation
            for (int i = 0; i < 500; i++)
            {
                Assert.True(rateLimiter.AllowCall(pluginId, "Operation1"));
                Assert.True(rateLimiter.AllowCall(pluginId, "Operation2"));
            }

            // Assert - Both operations should still be under their individual limits
            Assert.True(rateLimiter.AllowCall(pluginId, "Operation1"));
            Assert.True(rateLimiter.AllowCall(pluginId, "Operation2"));
        }

        [Fact]
        public void RateLimiter_GetCallCount_ReturnsAccurateCount()
        {
            // Arrange
            var rateLimiter = new RateLimiter();
            var pluginId = "test.ratelimit.count";
            var operation = "TestOperation";

            // Act
            for (int i = 0; i < 50; i++)
            {
                rateLimiter.AllowCall(pluginId, operation);
            }

            // Assert
            var count = rateLimiter.GetCallCount(pluginId, operation);
            Assert.Equal(50, count);
        }

        [Fact]
        public void RateLimiter_ResetAfterWindow()
        {
            // Arrange
            var rateLimiter = new RateLimiter(1000, TimeSpan.FromMilliseconds(100)); // 100ms window
            var pluginId = "test.ratelimit.reset";
            var operation = "TestOperation";

            // Act - Fill the limit
            for (int i = 0; i < 1000; i++)
            {
                rateLimiter.AllowCall(pluginId, operation);
            }

            // Verify limit is hit
            Assert.False(rateLimiter.AllowCall(pluginId, operation));

            // Wait for window to expire
            Thread.Sleep(150);

            // Assert - Should allow calls again after window expires
            Assert.True(rateLimiter.AllowCall(pluginId, operation));
        }

        [Fact]
        public void RateLimiter_IntegrationWithSecurityContext()
        {
            // Arrange
            var manifest = CreateManifestWithPermissions("test.ratelimit.security", "audio.*");
            var permissions = new PermissionChecker(manifest);
            var rateLimiter = new RateLimiter();
            var auditLog = new SecurityAuditLog();
            var security = new PluginSecurityContext(manifest.Plugin.Id, permissions, rateLimiter, auditLog);

            // Act - Make 999 calls (under limit)
            for (int i = 0; i < 999; i++)
            {
                security.CheckSecurity("audio.play", "TestOp");
            }

            // Assert - Should still work
            security.CheckSecurity("audio.play", "TestOp");

            // But 1001st call should fail
            Assert.Throws<RateLimitExceededException>(() =>
                security.CheckSecurity("audio.play", "TestOp"));
        }

        [Fact]
        public void RateLimiter_DifferentOperations_SeparateLimits()
        {
            // Arrange
            var rateLimiter = new RateLimiter();
            var pluginId = "test.ratelimit.separate";

            // Act - Max out Operation1
            for (int i = 0; i < 1000; i++)
            {
                rateLimiter.AllowCall(pluginId, "Operation1");
            }

            // Assert - Operation1 is blocked, but Operation2 is still allowed
            Assert.False(rateLimiter.AllowCall(pluginId, "Operation1"));
            Assert.True(rateLimiter.AllowCall(pluginId, "Operation2"));
        }

        [Fact]
        public void RateLimiter_CustomLimit_Enforced()
        {
            // Arrange
            var rateLimiter = new RateLimiter(maxCallsPerWindow: 50); // Custom limit
            var pluginId = "test.ratelimit.custom";
            var operation = "TestOp";

            // Act - Make 50 calls (at custom limit)
            for (int i = 0; i < 50; i++)
            {
                Assert.True(rateLimiter.AllowCall(pluginId, operation));
            }

            // Assert - 51st call should be blocked
            Assert.False(rateLimiter.AllowCall(pluginId, operation));
        }

        [Fact]
        public void SecurityAuditLog_LogsRateLimitViolations()
        {
            // Arrange
            var auditLog = new SecurityAuditLog();
            var pluginId = "test.ratelimit.audit";

            // Act
            auditLog.LogRateLimitViolation(pluginId, "SpamOperation", 1001, 1000);

            // Assert
            var events = auditLog.GetEvents(pluginId);
            Assert.NotEmpty(events);
            Assert.Contains(events, e =>
                e.EventType == SecurityEventType.RateLimitViolation &&
                e.Details.Contains("1001"));
        }
    }
}
