using System;
using System.Diagnostics;
using System.IO;
using DialogEditor.Plugins.Security;
using DialogEditor.Services;
using DialogEditor.Utils;

namespace DialogEditor.Plugins
{
    /// <summary>
    /// Manages the lifecycle of a single plugin process
    /// </summary>
    public class PluginHost : IDisposable
    {
        private readonly DiscoveredPlugin _plugin;
        private Process? _process;
        private bool _isRunning;

        public string PluginId => _plugin.Manifest.Plugin.Id;
        public bool IsRunning => _isRunning;

        public PluginHost(DiscoveredPlugin plugin)
        {
            _plugin = plugin;
        }

        /// <summary>
        /// Start the plugin process
        /// </summary>
        public bool Start()
        {
            if (_isRunning)
            {
                UnifiedLogger.LogPlugin(LogLevel.WARN, $"Plugin {PluginId} is already running");
                return false;
            }

            try
            {
                // Find Python executable
                var pythonExe = FindPythonExecutable();
                if (string.IsNullOrEmpty(pythonExe))
                {
                    UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Python not found for plugin {PluginId}");
                    return false;
                }

                // Set up Python path to include parley_plugin library
                var pythonLibPath = GetParleyPythonLibPath();

                // Create process
                _process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = pythonExe,
                        Arguments = $"\"{_plugin.EntryPointPath}\"",
                        WorkingDirectory = _plugin.PluginDirectory,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                // Set PYTHONPATH environment variable
                if (!string.IsNullOrEmpty(pythonLibPath))
                {
                    var existingPythonPath = Environment.GetEnvironmentVariable("PYTHONPATH") ?? "";
                    var newPythonPath = string.IsNullOrEmpty(existingPythonPath)
                        ? pythonLibPath
                        : $"{pythonLibPath}{Path.PathSeparator}{existingPythonPath}";
                    _process.StartInfo.EnvironmentVariables["PYTHONPATH"] = newPythonPath;
                }

                // Wire up output handlers
                _process.OutputDataReceived += OnOutputReceived;
                _process.ErrorDataReceived += OnErrorReceived;
                _process.Exited += OnProcessExited;
                _process.EnableRaisingEvents = true;

                // Start the process
                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                _isRunning = true;

                UnifiedLogger.LogPlugin(LogLevel.INFO,
                    $"Started plugin {_plugin.Manifest.Plugin.Name} (PID: {_process.Id})");

                return true;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR,
                    $"Failed to start plugin {PluginId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Stop the plugin process
        /// </summary>
        public void Stop()
        {
            if (!_isRunning || _process == null)
                return;

            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill();
                    _process.WaitForExit(5000); // Wait up to 5 seconds
                }

                UnifiedLogger.LogPlugin(LogLevel.INFO, $"Stopped plugin {PluginId}");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR,
                    $"Error stopping plugin {PluginId}: {ex.Message}");
            }
            finally
            {
                _isRunning = false;
                _process?.Dispose();
                _process = null;
            }
        }

        private void OnOutputReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                UnifiedLogger.LogPlugin(LogLevel.INFO, $"[{PluginId}] {e.Data}");
            }
        }

        private void OnErrorReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"[{PluginId}] {e.Data}");
            }
        }

        private void OnProcessExited(object? sender, EventArgs e)
        {
            if (_process == null)
                return;

            var exitCode = _process.ExitCode;
            _isRunning = false;

            if (exitCode != 0)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR,
                    $"Plugin {PluginId} exited with code {exitCode}");
                PluginSettingsService.Instance.RecordPluginCrash(PluginId);
            }
            else
            {
                UnifiedLogger.LogPlugin(LogLevel.INFO, $"Plugin {PluginId} exited normally");
            }
        }

        /// <summary>
        /// Find Python executable on the system
        /// </summary>
        private string? FindPythonExecutable()
        {
            // Try common Python commands
            string[] pythonCommands = { "python", "python3", "py" };

            foreach (var cmd in pythonCommands)
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = cmd,
                            Arguments = "--version",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    process.WaitForExit(1000);

                    if (process.ExitCode == 0)
                    {
                        return cmd;
                    }
                }
                catch
                {
                    // Command not found, try next one
                }
            }

            return null;
        }

        /// <summary>
        /// Get path to parley_plugin Python library
        /// </summary>
        private string? GetParleyPythonLibPath()
        {
            // Try to find Python library relative to Parley executable
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var pythonLibPath = Path.Combine(appDir, "Python");

            if (Directory.Exists(pythonLibPath))
            {
                return pythonLibPath;
            }

            // For development, try repository structure
            var repoPath = Path.Combine(appDir, "..", "..", "..", "..", "Python");
            if (Directory.Exists(repoPath))
            {
                return Path.GetFullPath(repoPath);
            }

            UnifiedLogger.LogPlugin(LogLevel.WARN,
                "Could not find parley_plugin library - plugins may fail to import");
            return null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
