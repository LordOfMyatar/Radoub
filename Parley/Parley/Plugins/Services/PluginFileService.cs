using System;
using System.IO;
using System.Threading.Tasks;
using Grpc.Core;
using DialogEditor.Plugins.Proto;
using DialogEditor.Plugins.Security;
using DialogEditor.Services;

namespace DialogEditor.Plugins.Services
{
    /// <summary>
    /// File service implementation for plugins
    /// </summary>
    public class PluginFileService : FileService.FileServiceBase
    {
        private readonly PluginSecurityContext _security;
        private readonly string _sandboxPath;

        public PluginFileService(PluginSecurityContext security)
        {
            _security = security ?? throw new ArgumentNullException(nameof(security));

            // Sandbox plugins to their own data directory
            var userDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Parley", "PluginData"
            );
            _sandboxPath = userDataDir;

            if (!Directory.Exists(_sandboxPath))
            {
                Directory.CreateDirectory(_sandboxPath);
            }
        }

        public override Task<OpenFileDialogResponse> OpenFileDialog(OpenFileDialogRequest request, ServerCallContext context)
        {
            try
            {
                // Check security (permission + rate limit)
                _security.CheckSecurity("file.dialog", "OpenFileDialog");

                // TODO: Implement actual file dialog when UI framework is available
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

                // TODO: Implement actual file dialog when UI framework is available
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
        /// Sanitize file path to ensure it's within the plugin sandbox
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

                return fullPath;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Error sanitizing path: {ex.Message}");
                return null;
            }
        }
    }
}
