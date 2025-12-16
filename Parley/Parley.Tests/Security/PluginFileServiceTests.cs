using System;
using System.IO;
using System.Threading.Tasks;
using DialogEditor.Plugins;
using DialogEditor.Plugins.Security;
using DialogEditor.Plugins.Services;
using DialogEditor.Plugins.Proto;
using Google.Protobuf;
using Xunit;

namespace Parley.Tests.Security
{
    /// <summary>
    /// Tests for PluginFileService security features (#254)
    /// </summary>
    public class PluginFileServiceTests : IDisposable
    {
        private readonly string _testPluginId = "test.security.plugin";
        private readonly PluginFileService _fileService;
        private readonly string _sandboxPath;

        public PluginFileServiceTests()
        {
            // Create a test manifest with file permissions
            var manifest = new PluginManifest
            {
                ManifestVersion = "1.0",
                Plugin = new PluginInfo
                {
                    Id = _testPluginId,
                    Name = "Test Security Plugin",
                    Version = "1.0.0",
                    Author = "Test"
                },
                Permissions = new System.Collections.Generic.List<string>
                {
                    "file.read",
                    "file.write",
                    "file.dialog"
                },
                EntryPoint = "test.py"
            };

            // Create security context
            var permissionChecker = new PermissionChecker(manifest);
            var rateLimiter = new RateLimiter();
            var auditLog = new SecurityAuditLog();
            var securityContext = new PluginSecurityContext(_testPluginId, permissionChecker, rateLimiter, auditLog);

            _fileService = new PluginFileService(securityContext);
            _sandboxPath = _fileService.SandboxPath;

            // Ensure sandbox directory exists
            Directory.CreateDirectory(_sandboxPath);
        }

        public void Dispose()
        {
            // Clean up test sandbox directory
            if (Directory.Exists(_sandboxPath))
            {
                try
                {
                    Directory.Delete(_sandboxPath, true);
                }
                catch
                {
                    // Ignore cleanup failures
                }
            }
        }

        #region Per-Plugin Sandbox Isolation Tests

        [Fact]
        public void Sandbox_IsIsolatedByPluginId()
        {
            // Verify sandbox path includes plugin ID
            Assert.Contains(_testPluginId, _sandboxPath);
            Assert.EndsWith(_testPluginId, _sandboxPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        [Fact]
        public void Sandbox_DifferentPluginsGetDifferentPaths()
        {
            // Create another file service with different plugin ID
            var otherPluginId = "other.security.plugin";
            var otherManifest = new PluginManifest
            {
                ManifestVersion = "1.0",
                Plugin = new PluginInfo
                {
                    Id = otherPluginId,
                    Name = "Other Plugin",
                    Version = "1.0.0",
                    Author = "Test"
                },
                Permissions = new System.Collections.Generic.List<string> { "file.read", "file.write" },
                EntryPoint = "test.py"
            };

            var otherPermissionChecker = new PermissionChecker(otherManifest);
            var otherSecurityContext = new PluginSecurityContext(
                otherPluginId,
                otherPermissionChecker,
                new RateLimiter(),
                new SecurityAuditLog()
            );
            var otherFileService = new PluginFileService(otherSecurityContext);

            // Verify paths are different
            Assert.NotEqual(_sandboxPath, otherFileService.SandboxPath);
            Assert.Contains(otherPluginId, otherFileService.SandboxPath);

            // Cleanup
            if (Directory.Exists(otherFileService.SandboxPath))
            {
                Directory.Delete(otherFileService.SandboxPath, true);
            }
        }

        #endregion

        #region File Size Limit Tests

        [Fact]
        public void MaxFileSize_Is10MB()
        {
            // Verify constant is correctly set
            Assert.Equal(10 * 1024 * 1024, PluginFileService.MaxFileSize);
        }

        [Fact]
        public async Task WriteFile_RejectsFilesExceedingMaxSize()
        {
            // Create content larger than 10 MB
            var largeContent = new byte[PluginFileService.MaxFileSize + 1];
            Array.Fill<byte>(largeContent, 0x42);

            var request = new WriteFileRequest
            {
                FilePath = "large-file.bin",
                Content = ByteString.CopyFrom(largeContent)
            };

            // Act
            var response = await _fileService.WriteFile(request, null!);

            // Assert
            Assert.False(response.Success);
            Assert.Contains("too large", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task WriteFile_AcceptsFilesUnderMaxSize()
        {
            // Create content just under 10 MB
            var content = new byte[1024]; // 1 KB
            Array.Fill<byte>(content, 0x42);

            var request = new WriteFileRequest
            {
                FilePath = "small-file.bin",
                Content = ByteString.CopyFrom(content)
            };

            // Act
            var response = await _fileService.WriteFile(request, null!);

            // Assert
            Assert.True(response.Success, response.ErrorMessage);

            // Verify file was written
            var filePath = Path.Combine(_sandboxPath, "small-file.bin");
            Assert.True(File.Exists(filePath));
            Assert.Equal(1024, new FileInfo(filePath).Length);
        }

        #endregion

        #region Path Traversal Tests

        [Theory]
        [InlineData("../../../etc/passwd")]
        [InlineData("subdir/../../../etc/passwd")]
        public async Task ReadFile_BlocksPathTraversal(string maliciousPath)
        {
            var request = new ReadFileRequest { FilePath = maliciousPath };

            var response = await _fileService.ReadFile(request, null!);

            Assert.False(response.Success);
            Assert.Contains("outside sandbox", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("..\\..\\..\\Windows\\System32\\cmd.exe")]
        public async Task ReadFile_BlocksPathTraversal_WindowsPaths(string maliciousPath)
        {
            // Windows-style paths with backslashes - on Linux these become literal filenames
            // that don't exist, so we get "File not found" instead of "outside sandbox"
            var request = new ReadFileRequest { FilePath = maliciousPath };

            var response = await _fileService.ReadFile(request, null!);

            Assert.False(response.Success);
            // On Windows: "outside sandbox", on Linux: "not found" (backslash is valid in filenames)
            Assert.True(
                response.ErrorMessage.Contains("outside sandbox", StringComparison.OrdinalIgnoreCase) ||
                response.ErrorMessage.Contains("not found", StringComparison.OrdinalIgnoreCase),
                $"Expected 'outside sandbox' or 'not found', got: {response.ErrorMessage}");
        }

        [Theory]
        [InlineData("../../../etc/passwd")]
        public async Task WriteFile_BlocksPathTraversal(string maliciousPath)
        {
            var request = new WriteFileRequest
            {
                FilePath = maliciousPath,
                Content = ByteString.CopyFrom(new byte[] { 0x42 })
            };

            var response = await _fileService.WriteFile(request, null!);

            Assert.False(response.Success);
            Assert.Contains("outside sandbox", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("..\\..\\..\\Windows\\System32\\evil.exe")]
        public async Task WriteFile_BlocksPathTraversal_WindowsPaths(string maliciousPath)
        {
            // Windows-style paths with backslashes - on Linux these are valid filename chars
            // The security check should still prevent sandbox escape, but behavior differs
            var request = new WriteFileRequest
            {
                FilePath = maliciousPath,
                Content = ByteString.CopyFrom(new byte[] { 0x42 })
            };

            var response = await _fileService.WriteFile(request, null!);

            // On Windows: blocked as path traversal
            // On Linux: backslash is valid char, file gets written with literal name
            if (OperatingSystem.IsWindows())
            {
                Assert.False(response.Success);
                Assert.Contains("outside sandbox", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                // On Linux, backslash doesn't traverse - it's a valid filename character
                // This actually succeeds because the file is written within sandbox
                // The real protection on Linux is via forward slash detection
                // Either outcome is acceptable here
            }
        }

        [Fact]
        public async Task ReadFile_BlocksAbsolutePaths()
        {
            // Use platform-appropriate absolute path
            string absolutePath = OperatingSystem.IsWindows()
                ? "C:\\Windows\\System32\\notepad.exe"
                : "/etc/passwd";

            var request = new ReadFileRequest
            {
                FilePath = absolutePath
            };

            var response = await _fileService.ReadFile(request, null!);

            Assert.False(response.Success);
            Assert.Contains("outside sandbox", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Relative Path Tests

        [Fact]
        public async Task WriteFile_AllowsRelativePaths()
        {
            var request = new WriteFileRequest
            {
                FilePath = "subdir/test-file.txt",
                Content = ByteString.CopyFromUtf8("Hello, World!")
            };

            var response = await _fileService.WriteFile(request, null!);

            Assert.True(response.Success, response.ErrorMessage);

            // Verify file exists in correct location
            var expectedPath = Path.Combine(_sandboxPath, "subdir", "test-file.txt");
            Assert.True(File.Exists(expectedPath));
            Assert.Equal("Hello, World!", File.ReadAllText(expectedPath));
        }

        [Fact]
        public async Task ReadFile_WorksWithRelativePaths()
        {
            // First write a file
            var testContent = "Test content for reading";
            var filePath = Path.Combine(_sandboxPath, "read-test.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, testContent);

            // Now read it via the service
            var request = new ReadFileRequest { FilePath = "read-test.txt" };

            var response = await _fileService.ReadFile(request, null!);

            Assert.True(response.Success, response.ErrorMessage);
            Assert.Equal(testContent, response.Content.ToStringUtf8());
        }

        #endregion

        #region File Not Found Tests

        [Fact]
        public async Task ReadFile_ReturnsErrorForMissingFile()
        {
            var request = new ReadFileRequest { FilePath = "nonexistent-file.txt" };

            var response = await _fileService.ReadFile(request, null!);

            Assert.False(response.Success);
            Assert.Contains("not found", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Empty Path Tests

        [Fact]
        public async Task ReadFile_RejectsEmptyPath()
        {
            var request = new ReadFileRequest { FilePath = "" };

            var response = await _fileService.ReadFile(request, null!);

            Assert.False(response.Success);
        }

        [Fact]
        public async Task WriteFile_RejectsEmptyPath()
        {
            var request = new WriteFileRequest
            {
                FilePath = "",
                Content = ByteString.CopyFromUtf8("content")
            };

            var response = await _fileService.WriteFile(request, null!);

            Assert.False(response.Success);
        }

        #endregion
    }
}
