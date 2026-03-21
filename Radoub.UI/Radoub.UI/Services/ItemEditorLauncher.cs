using System.Diagnostics;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;

namespace Radoub.UI.Services;

// TODO (#1730): Consider consolidating discovery logic with Trebuchet's ToolLauncherService
// in a future refactor. Currently duplicated because ToolLauncherService lives in
// RadoubLauncher namespace (Trebuchet-specific), not accessible from Radoub.UI.
public static class ItemEditorLauncher
{
    /// <summary>
    /// Resolves a UTI file path from module directory only (legacy method).
    /// Prefer ResolveAndLaunch for full resolution chain support.
    /// </summary>
    public static string? ResolveUtiPath(string resRef, string moduleDirectory)
    {
        if (string.IsNullOrEmpty(resRef) || string.IsNullOrEmpty(moduleDirectory))
            return null;
        if (!Directory.Exists(moduleDirectory))
            return null;

        var filename = resRef + ".uti";
        try
        {
            var files = Directory.GetFiles(moduleDirectory, "*.uti");
            return files.FirstOrDefault(f =>
                Path.GetFileName(f).Equals(filename, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Error searching for UTI: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Resolves a UTI blueprint from all sources and launches Relique.
    /// Resolution order: module directory → Override → HAK/BIF (extracted to module dir).
    /// </summary>
    /// <returns>Status message describing the result.</returns>
    public static string ResolveAndLaunch(string resRef, string? moduleDirectory, IGameDataService? gameDataService)
    {
        if (string.IsNullOrEmpty(resRef))
            return "Item has no ResRef";

        // 1. Check module directory (loose file)
        if (!string.IsNullOrEmpty(moduleDirectory) && Directory.Exists(moduleDirectory))
        {
            var modulePath = Path.Combine(moduleDirectory, resRef + ".uti");
            if (File.Exists(modulePath))
            {
                return LaunchWithFile(modulePath)
                    ? $"Opened '{resRef}.uti' in Relique"
                    : "Failed to launch Relique";
            }
        }

        // 2. Check Override folder (loose file — can open directly)
        var nwnPath = RadoubSettings.Instance.NeverwinterNightsPath;
        if (!string.IsNullOrEmpty(nwnPath))
        {
            var overridePath = Path.Combine(nwnPath, "override", resRef + ".uti");
            if (File.Exists(overridePath))
            {
                return LaunchWithFile(overridePath)
                    ? $"Opened '{resRef}.uti' from Override in Relique"
                    : "Failed to launch Relique";
            }
        }

        // 3. Try GameDataService (HAK → BIF) — extract to module directory
        if (gameDataService != null && gameDataService.IsConfigured)
        {
            try
            {
                var data = gameDataService.FindResource(resRef, ResourceTypes.Uti);
                if (data != null)
                {
                    if (string.IsNullOrEmpty(moduleDirectory) || !Directory.Exists(moduleDirectory))
                        return $"Found '{resRef}.uti' in game data but no module directory to extract to";

                    var extractedPath = Path.Combine(moduleDirectory, resRef + ".uti");
                    File.WriteAllBytes(extractedPath, data);
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"Extracted '{resRef}.uti' from game data to module directory");

                    return LaunchWithFile(extractedPath)
                        ? $"Extracted and opened '{resRef}.uti' in Relique"
                        : "Failed to launch Relique";
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to resolve '{resRef}' from game data: {ex.Message}");
                return $"Error resolving '{resRef}.uti': {ex.Message}";
            }
        }

        return $"Item blueprint '{resRef}.uti' not found in module, Override, or game data";
    }

    /// <summary>
    /// Resolves the module working directory from RadoubSettings.
    /// Handles .mod file paths by finding the unpacked directory alongside them.
    /// </summary>
    public static string? GetModuleWorkingDirectory()
    {
        var modulePath = RadoubSettings.Instance.CurrentModulePath;
        if (!RadoubSettings.IsValidModulePath(modulePath))
            return null;

        if (File.Exists(modulePath) && modulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
        {
            var moduleName = Path.GetFileNameWithoutExtension(modulePath);
            var parentDir = Path.GetDirectoryName(modulePath);
            if (!string.IsNullOrEmpty(parentDir))
            {
                var candidate = Path.Combine(parentDir, moduleName);
                if (Directory.Exists(candidate))
                    return candidate;
            }
        }

        if (Directory.Exists(modulePath))
            return modulePath;

        return null;
    }

    public static bool LaunchWithFile(string utiFilePath)
    {
        var exePath = FindItemEditorExe();
        if (string.IsNullOrEmpty(exePath))
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, "Relique executable not found");
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"--file \"{utiFilePath}\"",
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? ""
            };
            Process.Start(startInfo)?.Dispose();
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Launched Relique: {UnifiedLogger.SanitizePath(utiFilePath)}");
            return true;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to launch Relique: {ex.Message}");
            return false;
        }
    }

    private static string? FindItemEditorExe()
    {
        // 1. Check RadoubSettings
        var settingsPath = RadoubSettings.Instance.ReliquePath;
        if (!string.IsNullOrEmpty(settingsPath) && File.Exists(settingsPath))
            return settingsPath;

        var exeName = OperatingSystem.IsWindows() ? "ItemEditor.exe" : "ItemEditor";
        var currentExeDir = AppContext.BaseDirectory;

        // 2. Check sibling dev directory (e.g. bin/Debug/net9.0/ → ../../Relique/Relique/bin/Debug/net9.0/)
        var siblingDirs = new[]
        {
            Path.Combine(currentExeDir, "..", "..", "..", "..", "Relique", "Relique", "bin", "Debug", "net9.0"),
            Path.Combine(currentExeDir, "..", "..", "..", "..", "Relique", "Relique", "bin", "Release", "net9.0"),
        };
        foreach (var dir in siblingDirs)
        {
            var candidate = Path.GetFullPath(Path.Combine(dir, exeName));
            if (File.Exists(candidate))
                return candidate;
        }

        // 3. Check same directory as current exe
        var sameDirPath = Path.Combine(currentExeDir, exeName);
        if (File.Exists(sameDirPath))
            return sameDirPath;

        return null;
    }
}
