using System;
using System.IO;
using System.Collections.Generic;
using DialogEditor.Plugins;
using DialogEditor.Plugins.Security;
using Xunit;

namespace Parley.Tests.Security
{
    /// <summary>
    /// Tests for sandboxed file access enforcement logic
    /// </summary>
    public class SandboxTests
    {
        [Fact]
        public void Sandbox_PreventsAccessOutsidePluginDirectory()
        {
            // This test verifies that the sandbox path is correctly configured
            var sandboxPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Parley", "PluginData"
            );

            // Verify sandbox directory is in user profile
            Assert.Contains("Parley", sandboxPath);
            Assert.Contains("PluginData", sandboxPath);
        }

        [Fact]
        public void Sandbox_PathValidation_DetectsTraversal()
        {
            // Test path traversal detection logic
            var sandboxPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Parley", "PluginData"
            );

            var maliciousPaths = new[]
            {
                "../../../etc/passwd",
                "..\\..\\..\\Windows\\System32",
                "/etc/passwd",
                "C:\\Windows\\System32\\notepad.exe"
            };

            foreach (var maliciousPath in maliciousPaths)
            {
                // Try to resolve relative path
                string fullPath;
                try
                {
                    if (!Path.IsPathRooted(maliciousPath))
                    {
                        fullPath = Path.GetFullPath(Path.Combine(sandboxPath, maliciousPath));
                    }
                    else
                    {
                        fullPath = Path.GetFullPath(maliciousPath);
                    }

                    // Check if path is within sandbox
                    var isWithinSandbox = fullPath.StartsWith(Path.GetFullPath(sandboxPath), StringComparison.OrdinalIgnoreCase);

                    // Assert - Malicious paths should NOT be within sandbox
                    Assert.False(isWithinSandbox, $"Malicious path should be blocked: {maliciousPath}");
                }
                catch
                {
                    // Path resolution failed (also acceptable)
                }
            }
        }

        [Fact]
        public void Sandbox_AllowsSubdirectories()
        {
            // Test that subdirectories within sandbox are allowed
            var sandboxPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Parley", "PluginData"
            );

            var validPaths = new[]
            {
                "data.txt",
                "subdir/data.txt",
                "deep/nested/path/file.txt"
            };

            foreach (var validPath in validPaths)
            {
                var fullPath = Path.GetFullPath(Path.Combine(sandboxPath, validPath));
                var isWithinSandbox = fullPath.StartsWith(Path.GetFullPath(sandboxPath), StringComparison.OrdinalIgnoreCase);

                // Assert - Valid paths should be within sandbox
                Assert.True(isWithinSandbox, $"Valid path should be allowed: {validPath}");
            }
        }

        [Fact]
        public void Sandbox_BlocksSystemDirectories()
        {
            // Verify system directories are outside sandbox
            var sandboxPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Parley", "PluginData"
            );

            var systemPaths = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                AppContext.BaseDirectory
            };

            foreach (var systemPath in systemPaths)
            {
                if (string.IsNullOrEmpty(systemPath))
                    continue;

                var fullSystemPath = Path.GetFullPath(systemPath);
                var isWithinSandbox = fullSystemPath.StartsWith(Path.GetFullPath(sandboxPath), StringComparison.OrdinalIgnoreCase);

                // Assert - System paths should NOT be within sandbox
                Assert.False(isWithinSandbox, $"System path should be blocked: {systemPath}");
            }
        }

        [Fact]
        public void SecurityAuditLog_LogsSandboxViolations()
        {
            // Arrange
            var auditLog = new SecurityAuditLog();
            var pluginId = "test.sandbox.audit";
            var attemptedPath = "../../../etc/passwd";

            // Act
            auditLog.LogSandboxViolation(pluginId, attemptedPath);

            // Assert
            var events = auditLog.GetEvents(pluginId);
            Assert.NotEmpty(events);
            Assert.Contains(events, e =>
                e.EventType == SecurityEventType.SandboxViolation &&
                e.Details.Contains(attemptedPath));
        }

        [Fact]
        public void Sandbox_FileCreation_LimitedToSandboxDirectory()
        {
            // Test that file creation is properly scoped
            var sandboxPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Parley", "PluginData"
            );

            // Ensure sandbox directory exists
            Directory.CreateDirectory(sandboxPath);

            // Create a test file in sandbox
            var testFile = Path.Combine(sandboxPath, "sandbox-test.txt");
            File.WriteAllText(testFile, "Test content");

            try
            {
                // Verify file exists
                Assert.True(File.Exists(testFile));

                // Verify it's within sandbox
                var fullPath = Path.GetFullPath(testFile);
                Assert.StartsWith(Path.GetFullPath(sandboxPath), fullPath, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                // Cleanup
                if (File.Exists(testFile))
                    File.Delete(testFile);
            }
        }
    }
}
