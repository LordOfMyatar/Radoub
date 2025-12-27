using System;
using Radoub.Formats.Logging;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace DialogEditor.Services
{
    /// <summary>
    /// Service for opening script files in external editors
    /// </summary>
    public class ExternalEditorService
    {
        private static readonly Lazy<ExternalEditorService> _instance = new Lazy<ExternalEditorService>(() => new ExternalEditorService());
        public static ExternalEditorService Instance => _instance.Value;

        /// <summary>
        /// Opens a script file in the configured external editor
        /// </summary>
        /// <param name="scriptName">Name of the script (without .nss extension)</param>
        /// <param name="dialogFilePath">Path to the current dialog file (to find script location)</param>
        /// <returns>True if script was opened successfully, false otherwise</returns>
        public bool OpenScript(string scriptName, string? dialogFilePath = null)
        {
            if (string.IsNullOrWhiteSpace(scriptName))
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, "OpenScript: No script name provided");
                return false;
            }

            // Find the script file
            string? scriptPath = FindScriptPath(scriptName, dialogFilePath);

            if (scriptPath == null || !File.Exists(scriptPath))
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"OpenScript: Script '{scriptName}.nss' not found");
                return false;
            }

            try
            {
                // Get configured editor or detect default
                var settings = SettingsService.Instance;
                string? editorPath = settings.ExternalEditorPath;

                if (string.IsNullOrWhiteSpace(editorPath))
                {
                    editorPath = DetectDefaultEditor();
                }

                if (!string.IsNullOrWhiteSpace(editorPath) && File.Exists(editorPath))
                {
                    // Use configured/detected editor
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"OpenScript: Opening '{UnifiedLogger.SanitizePath(scriptPath)}' with '{UnifiedLogger.SanitizePath(editorPath)}'");

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = editorPath,
                        Arguments = $"\"{scriptPath}\"",
                        UseShellExecute = false
                    });
                }
                else
                {
                    // Fallback to system default
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"OpenScript: Opening '{UnifiedLogger.SanitizePath(scriptPath)}' with system default editor");

                    Process.Start(new ProcessStartInfo(scriptPath)
                    {
                        UseShellExecute = true
                    });
                }

                return true;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"OpenScript: Error opening script '{scriptName}' - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Finds the path to a script file by searching configured locations
        /// </summary>
        private string? FindScriptPath(string scriptName, string? dialogFilePath)
        {
            // Remove .nss extension if present
            scriptName = scriptName.Replace(".nss", "", StringComparison.OrdinalIgnoreCase);

            var settings = SettingsService.Instance;

            // 1. Check same directory as dialog file
            if (!string.IsNullOrWhiteSpace(dialogFilePath))
            {
                string dialogDir = Path.GetDirectoryName(dialogFilePath) ?? "";
                string scriptPath = Path.Combine(dialogDir, $"{scriptName}.nss");

                if (File.Exists(scriptPath))
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"FindScriptPath: Found script in dialog directory: {UnifiedLogger.SanitizePath(scriptPath)}");
                    return scriptPath;
                }
            }

            // 2. Check configured script search paths
            if (settings.ScriptSearchPaths != null)
            {
                foreach (string searchPath in settings.ScriptSearchPaths)
                {
                    if (string.IsNullOrWhiteSpace(searchPath) || !Directory.Exists(searchPath))
                        continue;

                    string scriptPath = Path.Combine(searchPath, $"{scriptName}.nss");

                    if (File.Exists(scriptPath))
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG,
                            $"FindScriptPath: Found script in search path: {UnifiedLogger.SanitizePath(scriptPath)}");
                        return scriptPath;
                    }
                }
            }

            // 3. Check current module path
            if (!string.IsNullOrWhiteSpace(settings.CurrentModulePath))
            {
                string scriptPath = Path.Combine(settings.CurrentModulePath, $"{scriptName}.nss");

                if (File.Exists(scriptPath))
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"FindScriptPath: Found script in module path: {UnifiedLogger.SanitizePath(scriptPath)}");
                    return scriptPath;
                }
            }

            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"FindScriptPath: Script '{scriptName}.nss' not found in any search location");
            return null;
        }

        /// <summary>
        /// Detects a suitable default editor based on platform
        /// </summary>
        private string? DetectDefaultEditor()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Check for VS Code (most common)
                    string[] vscodeLocations = new[]
                    {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "Programs", "Microsoft VS Code", "Code.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                            "Microsoft VS Code", "Code.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                            "Microsoft VS Code", "Code.exe")
                    };

                    foreach (string location in vscodeLocations)
                    {
                        if (File.Exists(location))
                        {
                            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"DetectDefaultEditor: Found VS Code at {UnifiedLogger.SanitizePath(location)}");
                            return location;
                        }
                    }

                    // Check for Notepad++
                    string[] notepadppLocations = new[]
                    {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                            "Notepad++", "notepad++.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                            "Notepad++", "notepad++.exe")
                    };

                    foreach (string location in notepadppLocations)
                    {
                        if (File.Exists(location))
                        {
                            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"DetectDefaultEditor: Found Notepad++ at {UnifiedLogger.SanitizePath(location)}");
                            return location;
                        }
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // Check for VS Code on macOS
                    if (File.Exists("/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code"))
                    {
                        return "/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code";
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // Check for common Linux editors
                    string[] editors = new[] { "code", "gedit", "kate", "nano" };
                    foreach (string editor in editors)
                    {
                        try
                        {
                            var result = Process.Start(new ProcessStartInfo
                            {
                                FileName = "which",
                                Arguments = editor,
                                RedirectStandardOutput = true,
                                UseShellExecute = false
                            });

                            if (result != null)
                            {
                                string? path = result.StandardOutput.ReadLine();
                                if (!string.IsNullOrWhiteSpace(path))
                                {
                                    return path;
                                }
                            }
                        }
                        catch
                        {
                            // Ignore and try next editor
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"DetectDefaultEditor: Error detecting editor - {ex.Message}");
            }

            return null; // No editor detected, will use system default
        }
    }
}
