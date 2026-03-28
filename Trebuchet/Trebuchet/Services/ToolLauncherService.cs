using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;

namespace RadoubLauncher.Services;

/// <summary>
/// Maturity level for a Radoub tool.
/// </summary>
public enum ToolMaturity
{
    InDevelopment,
    Alpha,
    Beta,
    Stable
}

/// <summary>
/// Information about a Radoub tool.
/// </summary>
public class ToolInfo
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string FileTypes { get; init; }
    public required ToolMaturity Maturity { get; init; }
    /// <summary>
    /// Assembly/executable base name when it differs from the display Name.
    /// Used for exe discovery and directory lookup. Null means Name is used.
    /// </summary>
    public string? AssemblyName { get; init; }
    public string? ExecutablePath { get; set; }
    public bool IsAvailable => !string.IsNullOrEmpty(ExecutablePath) && File.Exists(ExecutablePath);
    public string StatusText => IsAvailable ? "Ready" : "Not Found";

    public string MaturityText => Maturity switch
    {
        ToolMaturity.InDevelopment => "In Development",
        ToolMaturity.Alpha => "Alpha",
        ToolMaturity.Beta => "Beta",
        ToolMaturity.Stable => "Stable",
        _ => "Unknown"
    };
}

/// <summary>
/// Information for launching a tool with a specific file.
/// Used as command parameter for launch-with-file actions.
/// </summary>
public class ToolFileLaunchInfo
{
    public required ToolInfo Tool { get; init; }
    public required string FilePath { get; init; }
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
                FileTypes = ".dlg",
                Maturity = ToolMaturity.Beta
            },
            new ToolInfo
            {
                Name = "Manifest",
                Description = "Journal Editor",
                FileTypes = ".jrl",
                Maturity = ToolMaturity.Beta
            },
            new ToolInfo
            {
                Name = "Quartermaster",
                Description = "Creature Editor",
                FileTypes = ".utc, .bic",
                Maturity = ToolMaturity.Alpha
            },
            new ToolInfo
            {
                Name = "Fence",
                Description = "Merchant Editor",
                FileTypes = ".utm",
                Maturity = ToolMaturity.Alpha
            },
            new ToolInfo
            {
                Name = "Relique",
                Description = "Item Blueprint Editor",
                FileTypes = ".uti",
                Maturity = ToolMaturity.Alpha,
                AssemblyName = "ItemEditor"
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
            var toolDir = Path.GetDirectoryName(tool.ExecutablePath!);
            var startInfo = new ProcessStartInfo
            {
                FileName = tool.ExecutablePath!,
                Arguments = arguments ?? "",
                UseShellExecute = true,
                WorkingDirectory = toolDir ?? ""
            };

            Process.Start(startInfo)?.Dispose();
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
            var assemblyName = tool.AssemblyName ?? tool.Name;
            tool.ExecutablePath = DiscoverToolPath(tool.Name, assemblyName, trebuchetDir);

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

    private string? DiscoverToolPath(string directoryName, string assemblyName, string? trebuchetDir)
    {
        // 1. Check RadoubSettings (tool registered its path when it last ran)
        var settingsPath = GetToolPathFromSettings(directoryName);
        if (!string.IsNullOrEmpty(settingsPath) && File.Exists(settingsPath))
        {
            return settingsPath;
        }

        // 2. Check same directory as Trebuchet
        if (!string.IsNullOrEmpty(trebuchetDir))
        {
            var sameDirPath = Path.Combine(trebuchetDir, GetExecutableName(assemblyName));
            if (File.Exists(sameDirPath))
            {
                return sameDirPath;
            }
        }

        // 3. Check development paths (sibling project directories)
        var devPath = DiscoverDevelopmentPath(directoryName, assemblyName);
        if (!string.IsNullOrEmpty(devPath))
        {
            CacheToolPath(directoryName, devPath);
            return devPath;
        }

        // 4. Check common installation locations
        var commonPaths = GetCommonInstallPaths(assemblyName);
        foreach (var path in commonPaths)
        {
            if (File.Exists(path))
            {
                CacheToolPath(directoryName, path);
                return path;
            }
        }

        return null;
    }

    private string? DiscoverDevelopmentPath(string directoryName, string assemblyName)
    {
        // In development, tools are in sibling directories like:
        // Radoub/Parley/Parley/bin/Debug/net9.0/Parley.exe
        // Radoub/Relique/Relique/bin/Debug/net9.0/ItemEditor.exe

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
            var exeName = GetExecutableName(assemblyName);
            var configs = new[] { "Debug", "Release" };
            var frameworks = new[] { "net9.0", "net8.0", "net7.0" };

            foreach (var config in configs)
            {
                foreach (var framework in frameworks)
                {
                    var toolPath = Path.Combine(radoubRoot, directoryName, directoryName, "bin", config, framework, exeName);
                    if (File.Exists(toolPath))
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Found {directoryName} in development path: {UnifiedLogger.SanitizePath(toolPath)}");
                        return toolPath;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Error discovering development path for {directoryName}: {ex.Message}");
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
            "relique" or "itemeditor" => RadoubSettings.Instance.ReliquePath,
            _ => null
        };
    }

    /// <summary>
    /// Save a discovered tool path to RadoubSettings so future startups hit the fast path.
    /// </summary>
    private void CacheToolPath(string toolName, string path)
    {
        try
        {
            var settings = RadoubSettings.Instance;
            switch (toolName.ToLowerInvariant())
            {
                case "parley": settings.ParleyPath = path; break;
                case "manifest": settings.ManifestPath = path; break;
                case "quartermaster": settings.QuartermasterPath = path; break;
                case "fence": settings.FencePath = path; break;
                case "relique": settings.ReliquePath = path; break;
            }
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Cached {toolName} path to settings: {UnifiedLogger.SanitizePath(path)}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Could not cache {toolName} path: {ex.Message}");
        }
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
        catch (Exception)
        {
            // Environment.ProcessPath can throw if process info is unavailable
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
