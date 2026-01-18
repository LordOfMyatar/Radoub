using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;

namespace RadoubLauncher.Services;

/// <summary>
/// Information about a Radoub tool.
/// </summary>
public class ToolInfo
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string FileTypes { get; init; }
    public string? ExecutablePath { get; set; }
    public bool IsAvailable => !string.IsNullOrEmpty(ExecutablePath) && File.Exists(ExecutablePath);
    public string StatusText => IsAvailable ? "Ready" : "Not Found";
}

/// <summary>
/// Service for discovering and launching Radoub tools.
/// </summary>
public class ToolLauncherService
{
    private static ToolLauncherService? _instance;
    private static readonly object _lock = new();

    public static ToolLauncherService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ToolLauncherService();
                }
            }
            return _instance;
        }
    }

    private readonly List<ToolInfo> _tools;

    private ToolLauncherService()
    {
        _tools = new List<ToolInfo>
        {
            new ToolInfo
            {
                Name = "Parley",
                Description = "Dialog Editor",
                FileTypes = ".dlg"
            },
            new ToolInfo
            {
                Name = "Manifest",
                Description = "Journal Editor",
                FileTypes = ".jrl"
            },
            new ToolInfo
            {
                Name = "Quartermaster",
                Description = "Creature Editor",
                FileTypes = ".utc, .bic"
            },
            new ToolInfo
            {
                Name = "Fence",
                Description = "Merchant Editor",
                FileTypes = ".utm"
            }
        };

        DiscoverTools();
    }

    /// <summary>
    /// Gets all known tools with their availability status.
    /// </summary>
    public IReadOnlyList<ToolInfo> Tools => _tools.AsReadOnly();

    /// <summary>
    /// Gets only the tools that are currently available (installed).
    /// </summary>
    public IEnumerable<ToolInfo> AvailableTools => _tools.Where(t => t.IsAvailable);

    /// <summary>
    /// Refresh tool discovery - checks for newly installed tools.
    /// </summary>
    public void RefreshTools()
    {
        DiscoverTools();
    }

    /// <summary>
    /// Launch a tool by name.
    /// </summary>
    /// <param name="toolName">Name of the tool to launch</param>
    /// <param name="arguments">Optional command line arguments</param>
    /// <returns>True if launched successfully</returns>
    public bool LaunchTool(string toolName, string? arguments = null)
    {
        var tool = _tools.FirstOrDefault(t => t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));
        if (tool == null)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Unknown tool: {toolName}");
            return false;
        }

        return LaunchTool(tool, arguments);
    }

    /// <summary>
    /// Launch a tool.
    /// </summary>
    /// <param name="tool">Tool to launch</param>
    /// <param name="arguments">Optional command line arguments</param>
    /// <returns>True if launched successfully</returns>
    public bool LaunchTool(ToolInfo tool, string? arguments = null)
    {
        if (!tool.IsAvailable)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Tool not available: {tool.Name}");
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = tool.ExecutablePath!,
                Arguments = arguments ?? "",
                UseShellExecute = false
            };

            Process.Start(startInfo);
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Launched {tool.Name}: {UnifiedLogger.SanitizePath(tool.ExecutablePath!)}");
            return true;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to launch {tool.Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Launch a tool with a file to open.
    /// </summary>
    /// <param name="tool">Tool to launch</param>
    /// <param name="filePath">File path to open</param>
    /// <returns>True if launched successfully</returns>
    public bool LaunchToolWithFile(ToolInfo tool, string filePath)
    {
        return LaunchTool(tool, $"--file \"{filePath}\"");
    }

    private void DiscoverTools()
    {
        var trebuchetDir = GetTrebuchetDirectory();

        foreach (var tool in _tools)
        {
            tool.ExecutablePath = DiscoverToolPath(tool.Name, trebuchetDir);

            if (tool.IsAvailable)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Found {tool.Name}: {UnifiedLogger.SanitizePath(tool.ExecutablePath!)}");
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"{tool.Name} not found");
            }
        }
    }

    private string? DiscoverToolPath(string toolName, string? trebuchetDir)
    {
        // 1. Check RadoubSettings (tool registered its path when it last ran)
        var settingsPath = GetToolPathFromSettings(toolName);
        if (!string.IsNullOrEmpty(settingsPath) && File.Exists(settingsPath))
        {
            return settingsPath;
        }

        // 2. Check same directory as Trebuchet
        if (!string.IsNullOrEmpty(trebuchetDir))
        {
            var sameDirPath = Path.Combine(trebuchetDir, GetExecutableName(toolName));
            if (File.Exists(sameDirPath))
            {
                return sameDirPath;
            }
        }

        // 3. Check development paths (sibling project directories)
        var devPath = DiscoverDevelopmentPath(toolName);
        if (!string.IsNullOrEmpty(devPath))
        {
            return devPath;
        }

        // 4. Check common installation locations
        var commonPaths = GetCommonInstallPaths(toolName);
        foreach (var path in commonPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private string? DiscoverDevelopmentPath(string toolName)
    {
        // In development, tools are in sibling directories like:
        // Radoub/Parley/Parley/bin/Debug/net9.0/Parley.exe
        // Radoub/Trebuchet/Trebuchet/bin/Debug/net9.0/Trebuchet.exe

        try
        {
            // Get the source directory from the assembly location
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrEmpty(assemblyLocation))
                return null;

            var assemblyDir = Path.GetDirectoryName(assemblyLocation);
            if (string.IsNullOrEmpty(assemblyDir))
                return null;

            // Navigate up from bin/Debug/net9.0 to the Radoub root
            // assemblyDir = Radoub/Trebuchet/Trebuchet/bin/Debug/net9.0
            var radoubRoot = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "..", ".."));

            // Check for the tool in its project directory
            var exeName = GetExecutableName(toolName);
            var configs = new[] { "Debug", "Release" };
            var frameworks = new[] { "net9.0", "net8.0", "net7.0" };

            foreach (var config in configs)
            {
                foreach (var framework in frameworks)
                {
                    var toolPath = Path.Combine(radoubRoot, toolName, toolName, "bin", config, framework, exeName);
                    if (File.Exists(toolPath))
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Found {toolName} in development path: {UnifiedLogger.SanitizePath(toolPath)}");
                        return toolPath;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Error discovering development path for {toolName}: {ex.Message}");
        }

        return null;
    }

    private string? GetToolPathFromSettings(string toolName)
    {
        return toolName.ToLowerInvariant() switch
        {
            "parley" => RadoubSettings.Instance.ParleyPath,
            "manifest" => RadoubSettings.Instance.ManifestPath,
            "quartermaster" => RadoubSettings.Instance.QuartermasterPath,
            "fence" => RadoubSettings.Instance.FencePath,
            _ => null
        };
    }

    private string GetExecutableName(string toolName)
    {
        var ext = OperatingSystem.IsWindows() ? ".exe" : "";
        return $"{toolName}{ext}";
    }

    private string? GetTrebuchetDirectory()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                return Path.GetDirectoryName(exePath);
            }
        }
        catch
        {
            // Ignore errors getting process path
        }
        return null;
    }

    private IEnumerable<string> GetCommonInstallPaths(string toolName)
    {
        var exeName = GetExecutableName(toolName);

        // Check Program Files on Windows
        if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            yield return Path.Combine(programFiles, "Radoub", exeName);
            yield return Path.Combine(programFilesX86, "Radoub", exeName);
            yield return Path.Combine(programFiles, "Radoub", toolName, exeName);
            yield return Path.Combine(programFilesX86, "Radoub", toolName, exeName);
        }

        // Check user's home directory
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(userHome, "Radoub", exeName);
        yield return Path.Combine(userHome, "Radoub", toolName, exeName);
        yield return Path.Combine(userHome, ".local", "bin", exeName);

        // Check /usr/local/bin on Unix
        if (!OperatingSystem.IsWindows())
        {
            yield return Path.Combine("/usr/local/bin", exeName);
            yield return Path.Combine("/opt/radoub", exeName);
        }
    }
}
