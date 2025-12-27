using System;
using System.IO;
using System.Threading.Tasks;
using Grpc.Core;
using DialogEditor.Plugins.Proto;
using DialogEditor.Plugins.Security;
using DialogEditor.Services;
using Radoub.Formats.Logging;

namespace DialogEditor.Plugins.Services
{
    /// <summary>
    /// File service implementation for plugins with security sandboxing
    /// </summary>
    public class PluginFileService : FileService.FileServiceBase
    {
        private readonly PluginSecurityContext _security;
        private readonly string _sandboxPath;

        /// <summary>
        /// Maximum file size allowed for plugin writes (10 MB default).
        /// Prevents disk fill DoS attacks.
        /// </summary>
        public const long MaxFileSize = 10 * 1024 * 1024;

        public PluginFileService(PluginSecurityContext security)
        {
            _security = security ?? throw new ArgumentNullException(nameof(security));

            // Sandbox plugins to their own data directory, isolated by plugin ID
            // New location: ~/Radoub/Parley (matches toolset structure)
            var userDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Radoub", "Parley", "PluginData", _security.PluginId
            );
            _sandboxPath = userDataDir;

            if (!Directory.Exists(_sandboxPath))
            {
                Directory.CreateDirectory(_sandboxPath);
            }
        }

        /// <summary>
        /// Gets the sandbox path for this plugin (for testing)
        /// </summary>
        public string SandboxPath => _sandboxPath;

        public override Task<OpenFileDialogResponse> OpenFileDialog(OpenFileDialogRequest request, ServerCallContext context)
        {
            try
            {
                // Check security (permission + rate limit)
                _security.CheckSecurity("file.dialog", "OpenFileDialog");

                // See #104 - Implement sandboxed file I/O for plugins (file dialogs)
                // For now, return cancelled

                UnifiedLogger.LogPlugin(LogLevel.INFO, $"Plugin requested file open dialog: {request.Title}");

                return Task.FromResult(new OpenFileDialogResponse
                {
                    Cancelled = true,
                    FilePath = string.Empty
                });
            }
            catch (PermissionDeniedException ex)
            {
                // Permission denial already logged by CheckSecurity
                throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
            }
            catch (RateLimitExceededException ex)
            {
                throw new RpcException(new Status(StatusCode.ResourceExhausted, ex.Message));
            }
        }

        public override Task<SaveFileDialogResponse> SaveFileDialog(SaveFileDialogRequest request, ServerCallContext context)
        {
            try
            {
                // Check security (permission + rate limit)
                _security.CheckSecurity("file.dialog", "SaveFileDialog");

                // See #104 - Implement sandboxed file I/O for plugins (file dialogs)
                // For now, return cancelled

                UnifiedLogger.LogPlugin(LogLevel.INFO, $"Plugin requested file save dialog: {request.Title}");

                return Task.FromResult(new SaveFileDialogResponse
                {
                    Cancelled = true,
                    FilePath = string.Empty
                });
            }
            catch (PermissionDeniedException ex)
            {
                // Permission denial already logged by CheckSecurity
                throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
            }
            catch (RateLimitExceededException ex)
            {
                throw new RpcException(new Status(StatusCode.ResourceExhausted, ex.Message));
            }
        }

        public override async Task<ReadFileResponse> ReadFile(ReadFileRequest request, ServerCallContext context)
        {
            try
            {
                // Check security (permission + rate limit)
                _security.CheckSecurity("file.read", "ReadFile");

                // Validate and sanitize path
                var sanitizedPath = SanitizePath(request.FilePath);
                if (sanitizedPath == null)
                {
                    _security.LogSandboxViolation(request.FilePath);
                    return new ReadFileResponse
                    {
                        Success = false,
                        ErrorMessage = "Invalid file path (outside sandbox)"
                    };
                }

                if (!File.Exists(sanitizedPath))
                {
                    return new ReadFileResponse
                    {
                        Success = false,
                        ErrorMessage = "File not found"
                    };
                }

                var content = await File.ReadAllBytesAsync(sanitizedPath);

                UnifiedLogger.LogPlugin(LogLevel.INFO, $"Plugin read file: {UnifiedLogger.SanitizePath(sanitizedPath)}");

                return new ReadFileResponse
                {
                    Success = true,
                    Content = Google.Protobuf.ByteString.CopyFrom(content)
                };
            }
            catch (PermissionDeniedException ex)
            {
                // Permission denial already logged by CheckSecurity
                throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
            }
            catch (RateLimitExceededException ex)
            {
                throw new RpcException(new Status(StatusCode.ResourceExhausted, ex.Message));
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Error reading file: {ex.Message}");
                return new ReadFileResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public override async Task<WriteFileResponse> WriteFile(WriteFileRequest request, ServerCallContext context)
        {
            try
            {
                // Check security (permission + rate limit)
                _security.CheckSecurity("file.write", "WriteFile");

                // Check file size limit (prevent disk fill DoS)
                if (request.Content.Length > MaxFileSize)
                {
                    UnifiedLogger.LogPlugin(LogLevel.WARN,
                        $"Plugin {_security.PluginId} attempted to write file exceeding size limit: {request.Content.Length} bytes > {MaxFileSize} bytes");
                    return new WriteFileResponse
                    {
                        Success = false,
                        ErrorMessage = $"File too large (max {MaxFileSize / 1024 / 1024} MB)"
                    };
                }

                // Validate and sanitize path
                var sanitizedPath = SanitizePath(request.FilePath);
                if (sanitizedPath == null)
                {
                    _security.LogSandboxViolation(request.FilePath);
                    return new WriteFileResponse
                    {
                        Success = false,
                        ErrorMessage = "Invalid file path (outside sandbox)"
                    };
                }

                // Ensure directory exists
                var directory = Path.GetDirectoryName(sanitizedPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllBytesAsync(sanitizedPath, request.Content.ToByteArray());

                UnifiedLogger.LogPlugin(LogLevel.INFO, $"Plugin wrote file: {UnifiedLogger.SanitizePath(sanitizedPath)}");

                return new WriteFileResponse
                {
                    Success = true
                };
            }
            catch (PermissionDeniedException ex)
            {
                // Permission denial already logged by CheckSecurity
                throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
            }
            catch (RateLimitExceededException ex)
            {
                throw new RpcException(new Status(StatusCode.ResourceExhausted, ex.Message));
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Error writing file: {ex.Message}");
                return new WriteFileResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Sanitize file path to ensure it's within the plugin sandbox.
        /// Also blocks symlinks to prevent sandbox escape.
        /// </summary>
        private string? SanitizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                // If path is relative, make it relative to sandbox
                if (!Path.IsPathRooted(path))
                {
                    path = Path.Combine(_sandboxPath, path);
                }

                // Get full path and ensure it's within sandbox
                var fullPath = Path.GetFullPath(path);
                var sandboxFullPath = Path.GetFullPath(_sandboxPath);

                if (!fullPath.StartsWith(sandboxFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    UnifiedLogger.LogPlugin(LogLevel.WARN, $"Plugin attempted to access path outside sandbox: {path}");
                    return null;
                }

                // Check for symlinks (ReparsePoint) to prevent sandbox escape
                // Check all path components for symlinks
                if (ContainsSymlink(fullPath, sandboxFullPath))
                {
                    UnifiedLogger.LogPlugin(LogLevel.WARN,
                        $"Plugin attempted to access path containing symlink: {UnifiedLogger.SanitizePath(path)}");
                    return null;
                }

                return fullPath;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Error sanitizing path: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if any component of the path (within sandbox) is a symlink.
        /// This prevents symlink-based sandbox escape attacks.
        /// </summary>
        private bool ContainsSymlink(string fullPath, string sandboxPath)
        {
            try
            {
                // Check if the file itself exists and is a symlink
                if (File.Exists(fullPath))
                {
                    var fileInfo = new FileInfo(fullPath);
                    if (fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        return true;
                    }
                }

                // Check each directory component within the sandbox
                var currentPath = sandboxPath;
                var relativePath = fullPath.Substring(sandboxPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var components = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                foreach (var component in components)
                {
                    if (string.IsNullOrEmpty(component))
                        continue;

                    currentPath = Path.Combine(currentPath, component);

                    if (Directory.Exists(currentPath))
                    {
                        var dirInfo = new DirectoryInfo(currentPath);
                        if (dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            catch
            {
                // If we can't determine symlink status, fail safe
                return true;
            }
        }
    }
}
