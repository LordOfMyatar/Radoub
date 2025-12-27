using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using DialogEditor.Plugins.Proto;
using DialogEditor.Services;
using Radoub.Formats.Logging;

namespace DialogEditor.Plugins
{
    /// <summary>
    /// Manages a single plugin process lifecycle
    /// </summary>
    public class PluginProcess : IDisposable
    {
        private readonly string _pluginId;
        private readonly string _pythonPath;
        private readonly string _entryPoint;
        private readonly string _pipeName;
        private Process? _process;
        private GrpcChannel? _channel;
        private Plugin.PluginClient? _client;
        private CancellationTokenSource? _healthCheckCts;
        private bool _isDisposed;

        public string PluginId => _pluginId;
        public bool IsRunning
        {
            get
            {
                if (_process == null) return false;
                try
                {
                    return !_process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    // Process was created but never started, or already disposed
                    return false;
                }
            }
        }
        public PluginStatus Status { get; private set; } = PluginStatus.Stopped;

        public event EventHandler<PluginCrashedEventArgs>? Crashed;
        public event EventHandler<PluginHealthChangedEventArgs>? HealthChanged;

        public PluginProcess(string pluginId, string pythonPath, string entryPoint)
        {
            _pluginId = pluginId ?? throw new ArgumentNullException(nameof(pluginId));
            _pythonPath = pythonPath ?? throw new ArgumentNullException(nameof(pythonPath));
            _entryPoint = entryPoint ?? throw new ArgumentNullException(nameof(entryPoint));
            _pipeName = GeneratePipeName(pluginId);
        }

        /// <summary>
        /// Start the plugin process
        /// </summary>
        public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
        {
            if (IsRunning)
            {
                UnifiedLogger.LogPlugin(LogLevel.WARN, $"Plugin {_pluginId} already running");
                return false;
            }

            try
            {
                Status = PluginStatus.Starting;
                UnifiedLogger.LogPlugin(LogLevel.INFO, $"Starting plugin process: {_pluginId}");

                // Create process
                _process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _pythonPath,
                        // -u = unbuffered stdout/stderr for real-time log capture
                        Arguments = $"-u {_entryPoint} --pipe {_pipeName}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    },
                    EnableRaisingEvents = true
                };

                _process.OutputDataReceived += OnProcessOutput;
                _process.ErrorDataReceived += OnProcessError;
                _process.Exited += OnProcessExited;

                // Start process
                if (!_process.Start())
                {
                    UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Failed to start plugin process: {_pluginId}");
                    Status = PluginStatus.Failed;
                    return false;
                }

                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                // Wait for process to initialize and create pipe
                await Task.Delay(1000, cancellationToken);

                // Connect gRPC channel
                var pipeAddress = GetPipeAddress(_pipeName);
                _channel = GrpcChannel.ForAddress(pipeAddress, new GrpcChannelOptions
                {
                    Credentials = ChannelCredentials.Insecure
                });

                _client = new Plugin.PluginClient(_channel);

                // Test connection with ping
                var pingResponse = await _client.PingAsync(
                    new PingRequest { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                    cancellationToken: cancellationToken
                );

                if (pingResponse.Status != "ok")
                {
                    UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Plugin {_pluginId} ping failed: {pingResponse.Status}");
                    Status = PluginStatus.Failed;
                    return false;
                }

                Status = PluginStatus.Running;
                UnifiedLogger.LogPlugin(LogLevel.INFO, $"Plugin {_pluginId} started successfully");

                // Start health monitoring
                StartHealthMonitoring();

                return true;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Exception starting plugin {_pluginId}: {ex.Message}");
                Status = PluginStatus.Failed;
                await StopAsync();
                return false;
            }
        }

        /// <summary>
        /// Stop the plugin process
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (!IsRunning)
            {
                return;
            }

            try
            {
                Status = PluginStatus.Stopping;
                UnifiedLogger.LogPlugin(LogLevel.INFO, $"Stopping plugin process: {_pluginId}");

                // Stop health monitoring
                _healthCheckCts?.Cancel();
                _healthCheckCts?.Dispose();
                _healthCheckCts = null;

                // Send shutdown message
                if (_client != null)
                {
                    try
                    {
                        // Note: Plugin.PluginClient doesn't have Shutdown - it's for Python → C#
                        // We just close the connection gracefully
                        // await _client.ShutdownAsync(...); // Not available in this direction
                    }
                    catch (RpcException)
                    {
                        // Plugin may already be unresponsive
                    }
                }

                // Clean up gRPC
                _client = null;
                if (_channel != null)
                {
                    await _channel.ShutdownAsync();
                    _channel.Dispose();
                    _channel = null;
                }

                // Kill process if still running
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill();
                    _process.WaitForExit(5000);
                }

                // Clean up process
                if (_process != null)
                {
                    _process.OutputDataReceived -= OnProcessOutput;
                    _process.ErrorDataReceived -= OnProcessError;
                    _process.Exited -= OnProcessExited;
                    _process.Dispose();
                    _process = null;
                }

                Status = PluginStatus.Stopped;
                UnifiedLogger.LogPlugin(LogLevel.INFO, $"Plugin {_pluginId} stopped");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Exception stopping plugin {_pluginId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Send ping to check plugin health
        /// </summary>
        public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
        {
            if (_client == null || Status != PluginStatus.Running)
            {
                return false;
            }

            try
            {
                var response = await _client.PingAsync(
                    new PingRequest { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                    deadline: DateTime.UtcNow.AddSeconds(5),
                    cancellationToken: cancellationToken
                );

                return response.Status == "ok";
            }
            catch (RpcException)
            {
                return false;
            }
        }

        private void StartHealthMonitoring()
        {
            _healthCheckCts = new CancellationTokenSource();
            var token = _healthCheckCts.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested && IsRunning)
                {
                    try
                    {
                        await Task.Delay(10000, token); // Check every 10 seconds

                        if (!token.IsCancellationRequested)
                        {
                            var healthy = await PingAsync(token);

                            if (!healthy && Status == PluginStatus.Running)
                            {
                                UnifiedLogger.LogPlugin(LogLevel.WARN, $"Plugin {_pluginId} health check failed");
                                HealthChanged?.Invoke(this, new PluginHealthChangedEventArgs(_pluginId, false));

                                // Kill unresponsive process
                                await StopAsync(token);

                                // Notify crash
                                Crashed?.Invoke(this, new PluginCrashedEventArgs(_pluginId, "Unresponsive"));
                            }
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Health check error for {_pluginId}: {ex.Message}");
                    }
                }
            }, token);
        }

        private void OnProcessOutput(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                UnifiedLogger.LogPlugin(LogLevel.INFO, $"[Plugin {_pluginId}] {e.Data}");
            }
        }

        private void OnProcessError(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"[Plugin {_pluginId}] {e.Data}");
            }
        }

        private void OnProcessExited(object? sender, EventArgs e)
        {
            var exitCode = _process?.ExitCode ?? -1;
            UnifiedLogger.LogPlugin(LogLevel.WARN, $"Plugin {_pluginId} process exited with code {exitCode}");

            if (Status == PluginStatus.Running)
            {
                // Unexpected exit
                Crashed?.Invoke(this, new PluginCrashedEventArgs(_pluginId, $"Exit code {exitCode}"));
            }

            Status = PluginStatus.Stopped;
        }

        private static string GeneratePipeName(string pluginId)
        {
            var guid = Guid.NewGuid().ToString("N");
            return $"parley-plugin-{pluginId}-{guid}";
        }

        private static string GetPipeAddress(string pipeName)
        {
            // Windows: \\.\pipe\name
            // Unix: /tmp/name
            if (OperatingSystem.IsWindows())
            {
                return $"npipe://{pipeName}";
            }
            else
            {
                return $"unix:/tmp/{pipeName}";
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            StopAsync().Wait();
            _isDisposed = true;

            GC.SuppressFinalize(this);
        }
    }

    public enum PluginStatus
    {
        Stopped,
        Starting,
        Running,
        Stopping,
        Failed
    }

    public class PluginCrashedEventArgs : EventArgs
    {
        public string PluginId { get; }
        public string Reason { get; }

        public PluginCrashedEventArgs(string pluginId, string reason)
        {
            PluginId = pluginId;
            Reason = reason;
        }
    }

    public class PluginHealthChangedEventArgs : EventArgs
    {
        public string PluginId { get; }
        public bool IsHealthy { get; }

        public PluginHealthChangedEventArgs(string pluginId, bool isHealthy)
        {
            PluginId = pluginId;
            IsHealthy = isHealthy;
        }
    }
}
