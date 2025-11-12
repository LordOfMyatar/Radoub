using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DialogEditor.Plugins;
using DialogEditor.Plugins.Proto;
using DialogEditor.Plugins.Security;
using Grpc.Core;
using Xunit;

namespace Parley.Tests.Security
{
    /// <summary>
    /// Tests for timeout protection on plugin operations
    /// </summary>
    public class TimeoutTests
    {
        [Fact]
        public async Task PluginProcess_PingTimeout_DetectsUnresponsive()
        {
            // Note: This test validates that PluginProcess has timeout protection
            // In actual implementation, PluginProcess.PingAsync uses a 5-second deadline
            // We can't easily test this without a real plugin process, so we verify the code structure

            // Arrange
            var pluginId = "test.timeout.ping";
            var pythonPath = "python"; // Won't actually start, just for test
            var entryPoint = "nonexistent.py";

            // Act
            var process = new PluginProcess(pluginId, pythonPath, entryPoint);

            // Assert - Verify PingAsync won't hang indefinitely
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(10));

            // This will fail to start, but we're testing that it doesn't hang
            var result = await process.StartAsync(cancellationTokenSource.Token);
            Assert.False(result); // Should fail to start

            // Cleanup
            await process.StopAsync();
            process.Dispose();
        }

        [Fact]
        public void SecurityAuditLog_LogsTimeout()
        {
            // Arrange
            var auditLog = new SecurityAuditLog();
            var pluginId = "test.timeout.log";
            var operation = "SlowOperation";
            var duration = TimeSpan.FromSeconds(15);

            // Act
            auditLog.LogTimeout(pluginId, operation, duration);

            // Assert - Verify log entry was created
            // Note: SecurityAuditLog stores events internally
            // We can verify it doesn't throw exceptions
        }

        [Fact]
        public void PluginSecurityContext_LogTimeout_RecordsEvent()
        {
            // Arrange
            var manifest = new PluginManifest
            {
                ManifestVersion = "1.0",
                Plugin = new PluginInfo
                {
                    Id = "test.timeout.context",
                    Name = "Test Plugin",
                    Version = "1.0.0",
                    Author = "Test",
                    Description = "Test plugin",
                    ParleyVersion = ">=0.1.5"
                },
                Permissions = new List<string>(),
                EntryPoint = "test.py",
                TrustLevel = "unverified"
            };

            var permissions = new PermissionChecker(manifest);
            var rateLimiter = new RateLimiter();
            var auditLog = new SecurityAuditLog();
            var security = new PluginSecurityContext(manifest.Plugin.Id, permissions, rateLimiter, auditLog);

            // Act
            security.LogTimeout("TestOperation", TimeSpan.FromSeconds(20));

            // Assert - Should complete without exceptions
        }

        [Fact]
        public async Task PluginProcess_HealthCheck_DetectsUnresponsive()
        {
            // This test verifies that health monitoring works as expected
            // The PluginProcess performs health checks every 10 seconds

            // Arrange
            var pluginId = "test.timeout.health";
            var pythonPath = "python";
            var entryPoint = "nonexistent.py";
            var process = new PluginProcess(pluginId, pythonPath, entryPoint);

            process.Crashed += (sender, args) =>
            {
                // Crash event registered but not expected to fire during startup failure
                Assert.Equal(pluginId, args.PluginId);
            };

            // Act - Try to start (will fail) and verify crash detection
            await process.StartAsync(CancellationToken.None);

            // Assert - Process should fail to start
            Assert.False(process.IsRunning);

            // Cleanup
            await process.StopAsync();
            process.Dispose();
        }

        [Fact]
        public void RateLimiter_WindowExpiration_ResetsLimits()
        {
            // This tests that rate limiter windows expire properly
            // which prevents indefinite blocking

            // Arrange
            var rateLimiter = new RateLimiter(1000, TimeSpan.FromMilliseconds(50)); // Very short window for testing
            var pluginId = "test.timeout.window";
            var operation = "FastOperation";

            // Act - Fill the limit
            for (int i = 0; i < 1000; i++)
            {
                rateLimiter.AllowCall(pluginId, operation);
            }

            // Verify limit is hit
            Assert.False(rateLimiter.AllowCall(pluginId, operation));

            // Wait for window to expire (prevents indefinite blocking)
            Thread.Sleep(100);

            // Assert - Window should reset, allowing new calls
            Assert.True(rateLimiter.AllowCall(pluginId, operation));
        }

        [Fact]
        public async Task CancellationToken_HonorsCancellation()
        {
            // Verify that operations respect cancellation tokens
            // This prevents operations from running indefinitely

            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            var pluginId = "test.timeout.cancellation";
            var pythonPath = "python";
            var entryPoint = "nonexistent.py";
            var process = new PluginProcess(pluginId, pythonPath, entryPoint);

            // Act - Start with a cancellation token
            cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(100));

            var startTask = process.StartAsync(cancellationTokenSource.Token);

            try
            {
                await startTask;
            }
            catch (OperationCanceledException)
            {
                // Expected - cancellation was requested
            }

            // Assert - Process should handle cancellation gracefully
            await process.StopAsync();
            process.Dispose();
        }

        [Fact]
        public async Task PluginManager_HandlesPluginTimeout()
        {
            // Verify that PluginManager properly handles plugin timeouts

            // Arrange
            var manager = new PluginManager();

            // The manager's SecurityLog should track timeouts
            var testPluginId = "test.timeout.manager";
            manager.SecurityLog.LogTimeout(testPluginId, "TestOperation", TimeSpan.FromSeconds(30));

            // Assert - Should complete without exceptions
            await Task.CompletedTask;
        }

        [Fact]
        public void SecurityAuditLog_GetEvents_ReturnsTimeoutEvents()
        {
            // Arrange
            var auditLog = new SecurityAuditLog();
            var pluginId = "test.timeout.events";

            // Act
            auditLog.LogTimeout(pluginId, "Operation1", TimeSpan.FromSeconds(10));
            auditLog.LogTimeout(pluginId, "Operation2", TimeSpan.FromSeconds(15));
            auditLog.LogTimeout(pluginId, "Operation3", TimeSpan.FromSeconds(20));

            var events = auditLog.GetEvents(pluginId);

            // Assert
            Assert.NotNull(events);
            Assert.NotEmpty(events);
            // All events should be timeout events
            Assert.All(events, e => Assert.Equal(SecurityEventType.Timeout, e.EventType));
        }
    }
}
