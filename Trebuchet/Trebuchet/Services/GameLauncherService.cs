using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;

namespace RadoubLauncher.Services;

/// <summary>
/// Service for discovering and launching Neverwinter Nights: Enhanced Edition.
/// </summary>
public class GameLauncherService
{
    private static GameLauncherService? _instance;
    private static readonly object _lock = new();

    public static GameLauncherService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new GameLauncherService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Path to the game executable (nwmain.exe or equivalent).
    /// </summary>
    public string? GameExecutablePath { get; private set; }

    /// <summary>
    /// Whether the game executable is available and can be launched.
    /// </summary>
    public bool IsGameAvailable => !string.IsNullOrEmpty(GameExecutablePath) && File.Exists(GameExecutablePath);

    private GameLauncherService()
    {
        DiscoverGame();
    }

    /// <summary>
    /// Refresh game discovery - call when settings change.
    /// </summary>
    public void RefreshDiscovery()
    {
        DiscoverGame();
    }

    /// <summary>
    /// Launch the game normally (no module).
    /// </summary>
    /// <returns>True if launched successfully</returns>
    public bool LaunchGame()
    {
        if (!IsGameAvailable)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "Cannot launch game: executable not found");
            return false;
        }

        return LaunchWithArguments(null);
    }

    /// <summary>
    /// Launch the game with a specific module.
    /// </summary>
    /// <param name="moduleName">Module name (folder name, not full path)</param>
    /// <param name="testMode">True for +TestNewModule (auto-select first character), false for +LoadNewModule (character select)</param>
    /// <returns>True if launched successfully</returns>
    public bool LaunchWithModule(string moduleName, bool testMode)
    {
        if (!IsGameAvailable)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "Cannot launch game: executable not found");
            return false;
        }

        if (string.IsNullOrWhiteSpace(moduleName))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "Cannot launch with module: no module name provided");
            return false;
        }

        var command = testMode ? "+TestNewModule" : "+LoadNewModule";
        var arguments = $"{command} \"{moduleName}\"";

        return LaunchWithArguments(arguments);
    }

    /// <summary>
    /// Extract the module name from a full module path.
    /// </summary>
    /// <param name="modulePath">Full path to module directory</param>
    /// <returns>Module name (folder name only)</returns>
    public static string? GetModuleNameFromPath(string? modulePath)
    {
        if (string.IsNullOrEmpty(modulePath))
            return null;

        // Get the folder name from the path
        var dirInfo = new DirectoryInfo(modulePath);
        return dirInfo.Name;
    }

    private bool LaunchWithArguments(string? arguments)
    {
        try
        {
            var gameDir = Path.GetDirectoryName(GameExecutablePath!);
            var startInfo = new ProcessStartInfo
            {
                FileName = GameExecutablePath!,
                Arguments = arguments ?? "",
                UseShellExecute = true,
                WorkingDirectory = gameDir ?? ""
            };

            Process.Start(startInfo);

            var logMessage = string.IsNullOrEmpty(arguments)
                ? "Launched NWN:EE"
                : $"Launched NWN:EE with: {arguments}";
            UnifiedLogger.LogApplication(LogLevel.INFO, logMessage);

            return true;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to launch NWN:EE: {ex.Message}");
            return false;
        }
    }

    private void DiscoverGame()
    {
        GameExecutablePath = null;

        var baseGamePath = RadoubSettings.Instance.BaseGameInstallPath;
        if (string.IsNullOrEmpty(baseGamePath))
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Game discovery: BaseGameInstallPath not configured");
            return;
        }

        if (!Directory.Exists(baseGamePath))
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Game discovery: BaseGameInstallPath does not exist");
            return;
        }

        // Find the executable based on platform
        var exePath = FindGameExecutable(baseGamePath);

        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
        {
            GameExecutablePath = exePath;
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Game discovery: Found {UnifiedLogger.SanitizePath(exePath)}");
        }
        else
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Game discovery: Executable not found in base game path");
        }
    }

    private static string? FindGameExecutable(string baseGamePath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // NWN:EE on Windows: bin/win32/nwmain.exe
            var paths = new[]
            {
                Path.Combine(baseGamePath, "bin", "win32", "nwmain.exe"),
                Path.Combine(baseGamePath, "nwmain.exe")
            };

            foreach (var path in paths)
            {
                if (File.Exists(path))
                    return path;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS: Neverwinter Nights.app/Contents/MacOS/nwmain
            var paths = new[]
            {
                Path.Combine(baseGamePath, "Neverwinter Nights.app", "Contents", "MacOS", "nwmain"),
                Path.Combine(baseGamePath, "nwmain")
            };

            foreach (var path in paths)
            {
                if (File.Exists(path))
                    return path;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Linux: bin/linux-x86/nwmain-linux
            var paths = new[]
            {
                Path.Combine(baseGamePath, "bin", "linux-x86", "nwmain-linux"),
                Path.Combine(baseGamePath, "nwmain-linux"),
                Path.Combine(baseGamePath, "nwmain")
            };

            foreach (var path in paths)
            {
                if (File.Exists(path))
                    return path;
            }
        }

        return null;
    }
}
