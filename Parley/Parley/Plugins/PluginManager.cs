using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DialogEditor.Services;

namespace DialogEditor.Plugins
{
    /// <summary>
    /// Manages all plugin processes
    /// </summary>
    public class PluginManager : IDisposable
    {
        private readonly Dictionary<string, PluginProcess> _plugins = new();
        private readonly object _lock = new();
        private bool _isDisposed;

        public IReadOnlyList<string> LoadedPlugins
        {
            get
            {
                lock (_lock)
                {
                    return _plugins.Keys.ToList();
                }
            }
        }

        /// <summary>
        /// Load and start a plugin
        /// </summary>
        public async Task<bool> LoadPluginAsync(
            string pluginId,
            string pythonPath,
            string entryPoint,
            CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                if (_plugins.ContainsKey(pluginId))
                {
                    UnifiedLogger.LogPlugin(LogLevel.WARN, $"Plugin {pluginId} already loaded");
                    return false;
                }
            }

            var process = new PluginProcess(pluginId, pythonPath, entryPoint);
            process.Crashed += OnPluginCrashed;
            process.HealthChanged += OnPluginHealthChanged;

            var started = await process.StartAsync(cancellationToken);

            if (started)
            {
                lock (_lock)
                {
                    _plugins[pluginId] = process;
                }

                UnifiedLogger.LogPlugin(LogLevel.INFO, $"Plugin {pluginId} loaded successfully");
                return true;
            }
            else
            {
                process.Crashed -= OnPluginCrashed;
                process.HealthChanged -= OnPluginHealthChanged;
                process.Dispose();

                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Failed to load plugin {pluginId}");
                return false;
            }
        }

        /// <summary>
        /// Unload and stop a plugin
        /// </summary>
        public async Task<bool> UnloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
        {
            PluginProcess? process;

            lock (_lock)
            {
                if (!_plugins.TryGetValue(pluginId, out process))
                {
                    UnifiedLogger.LogPlugin(LogLevel.WARN, $"Plugin {pluginId} not loaded");
                    return false;
                }

                _plugins.Remove(pluginId);
            }

            process.Crashed -= OnPluginCrashed;
            process.HealthChanged -= OnPluginHealthChanged;

            await process.StopAsync(cancellationToken);
            process.Dispose();

            UnifiedLogger.LogPlugin(LogLevel.INFO, $"Plugin {pluginId} unloaded");
            return true;
        }

        /// <summary>
        /// Check if a plugin is loaded
        /// </summary>
        public bool IsPluginLoaded(string pluginId)
        {
            lock (_lock)
            {
                return _plugins.ContainsKey(pluginId);
            }
        }

        /// <summary>
        /// Get plugin status
        /// </summary>
        public PluginStatus GetPluginStatus(string pluginId)
        {
            lock (_lock)
            {
                if (_plugins.TryGetValue(pluginId, out var process))
                {
                    return process.Status;
                }

                return PluginStatus.Stopped;
            }
        }

        /// <summary>
        /// Restart a crashed plugin
        /// </summary>
        public async Task<bool> RestartPluginAsync(
            string pluginId,
            string pythonPath,
            string entryPoint,
            CancellationToken cancellationToken = default)
        {
            UnifiedLogger.LogPlugin(LogLevel.INFO, $"Restarting plugin {pluginId}");

            // Unload if loaded
            if (IsPluginLoaded(pluginId))
            {
                await UnloadPluginAsync(pluginId, cancellationToken);
            }

            // Reload
            return await LoadPluginAsync(pluginId, pythonPath, entryPoint, cancellationToken);
        }

        /// <summary>
        /// Shutdown all plugins
        /// </summary>
        public async Task ShutdownAllAsync(CancellationToken cancellationToken = default)
        {
            UnifiedLogger.LogPlugin(LogLevel.INFO, "Shutting down all plugins");

            List<PluginProcess> processes;

            lock (_lock)
            {
                processes = _plugins.Values.ToList();
                _plugins.Clear();
            }

            // Stop all plugins in parallel
            var tasks = processes.Select(async p =>
            {
                p.Crashed -= OnPluginCrashed;
                p.HealthChanged -= OnPluginHealthChanged;
                await p.StopAsync(cancellationToken);
                p.Dispose();
            });

            await Task.WhenAll(tasks);

            UnifiedLogger.LogPlugin(LogLevel.INFO, "All plugins shut down");
        }

        private void OnPluginCrashed(object? sender, PluginCrashedEventArgs e)
        {
            UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Plugin {e.PluginId} crashed: {e.Reason}");

            // TODO: Implement crash recovery strategy
            // For now, just log and remove
            lock (_lock)
            {
                if (_plugins.TryGetValue(e.PluginId, out var process))
                {
                    _plugins.Remove(e.PluginId);
                    process.Crashed -= OnPluginCrashed;
                    process.HealthChanged -= OnPluginHealthChanged;
                    process.Dispose();
                }
            }
        }

        private void OnPluginHealthChanged(object? sender, PluginHealthChangedEventArgs e)
        {
            if (e.IsHealthy)
            {
                UnifiedLogger.LogPlugin(LogLevel.INFO, $"Plugin {e.PluginId} health restored");
            }
            else
            {
                UnifiedLogger.LogPlugin(LogLevel.WARN, $"Plugin {e.PluginId} health check failed");
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            ShutdownAllAsync().Wait();
            _isDisposed = true;

            GC.SuppressFinalize(this);
        }
    }
}
