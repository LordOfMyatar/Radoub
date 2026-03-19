using System.Diagnostics;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;

namespace Radoub.UI.Services;

// TODO (#1730): Consider consolidating discovery logic with Trebuchet's ToolLauncherService
// in a future refactor. Currently duplicated because ToolLauncherService lives in
// RadoubLauncher namespace (Trebuchet-specific), not accessible from Radoub.UI.
public static class ItemEditorLauncher
{
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

    public static bool LaunchWithFile(string utiFilePath)
    {
        var exePath = FindItemEditorExe();
        if (string.IsNullOrEmpty(exePath))
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, "ItemEditor executable not found");
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
                $"Launched ItemEditor: {UnifiedLogger.SanitizePath(utiFilePath)}");
            return true;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to launch ItemEditor: {ex.Message}");
            return false;
        }
    }

    private static string? FindItemEditorExe()
    {
        // 1. Check RadoubSettings
        var settingsPath = RadoubSettings.Instance.ItemEditorPath;
        if (!string.IsNullOrEmpty(settingsPath) && File.Exists(settingsPath))
            return settingsPath;

        var exeName = OperatingSystem.IsWindows() ? "ItemEditor.exe" : "ItemEditor";
        var currentExeDir = AppContext.BaseDirectory;

        // 2. Check sibling dev directory (e.g. bin/Debug/net9.0/ → ../../ItemEditor/bin/Debug/net9.0/)
        var siblingDirs = new[]
        {
            Path.Combine(currentExeDir, "..", "..", "..", "..", "ItemEditor", "ItemEditor", "bin", "Debug", "net9.0"),
            Path.Combine(currentExeDir, "..", "..", "..", "..", "ItemEditor", "ItemEditor", "bin", "Release", "net9.0"),
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
