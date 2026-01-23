using System.Runtime.InteropServices;
using Radoub.Formats.Logging;

namespace Radoub.Formats.Settings;

/// <summary>
/// Helper class for detecting Neverwinter Nights resource paths across platforms.
/// Consolidated from tool-specific implementations for shared use.
/// </summary>
public static class ResourcePathDetector
{
    /// <summary>
    /// Whether to log validation results. Default true.
    /// Set to false if you need silent validation.
    /// </summary>
    public static bool EnableLogging { get; set; } = true;

    private static void Log(LogLevel level, string message)
    {
        if (EnableLogging)
            UnifiedLogger.LogApplication(level, message);
    }

    /// <summary>
    /// Attempts to auto-detect the NWN user documents path (~/Documents/Neverwinter Nights).
    /// This contains modules, override, portraits, etc.
    /// </summary>
    public static string? AutoDetectGamePath()
    {
        var possiblePaths = GetPlatformGamePaths();

        foreach (var path in possiblePaths)
        {
            if (ValidateGamePath(path))
            {
                Log(LogLevel.INFO, $"Auto-detected user data path: {UnifiedLogger.SanitizePath(path)}");
                return path;
            }
        }

        Log(LogLevel.WARN, "Could not auto-detect user data path");
        return null;
    }

    /// <summary>
    /// Attempts to auto-detect the base game installation (Steam/GOG).
    /// This contains the data\ folder with BIF/KEY files.
    /// </summary>
    public static string? AutoDetectBaseGamePath()
    {
        var possiblePaths = GetPlatformBaseGamePaths();

        foreach (var path in possiblePaths)
        {
            Log(LogLevel.DEBUG, $"Checking base game path: {path}");
            if (ValidateBaseGamePath(path))
            {
                Log(LogLevel.INFO, $"Auto-detected base game path: {UnifiedLogger.SanitizePath(path)}");
                return path;
            }
        }

        Log(LogLevel.WARN, "Could not auto-detect base game installation");
        return null;
    }

    /// <summary>
    /// Attempts to auto-detect the module directory path.
    /// </summary>
    public static string? AutoDetectModulePath(string? gamePath = null)
    {
        if (!string.IsNullOrEmpty(gamePath))
        {
            var modulePath = Path.Combine(gamePath, "modules");
            if (ValidateModulePath(modulePath))
            {
                Log(LogLevel.INFO, $"Found module path in game directory: {UnifiedLogger.SanitizePath(modulePath)}");
                return modulePath;
            }
        }

        var possiblePaths = GetPlatformModulePaths();
        foreach (var path in possiblePaths)
        {
            if (ValidateModulePath(path))
            {
                Log(LogLevel.INFO, $"Auto-detected module path: {UnifiedLogger.SanitizePath(path)}");
                return path;
            }
        }

        Log(LogLevel.WARN, "Could not auto-detect module path");
        return null;
    }

    /// <summary>
    /// Validates that a path is a valid NWN user documents folder.
    /// Should have ambient and music subdirectories.
    /// </summary>
    public static bool ValidateGamePath(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return false;

        // Check for characteristic NWN subdirectories
        var requiredDirs = new[] { "ambient", "music" };
        foreach (var dir in requiredDirs)
        {
            var fullPath = Path.Combine(path, dir);
            if (!Directory.Exists(fullPath))
            {
                Log(LogLevel.DEBUG, $"Game path validation failed: missing '{dir}' directory");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates that a path is a valid NWN base game installation (Steam/GOG).
    /// Should have data\ folder.
    /// </summary>
    public static bool ValidateBaseGamePath(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return false;

        var dataPath = Path.Combine(path, "data");
        bool valid = Directory.Exists(dataPath);

        if (valid)
            Log(LogLevel.DEBUG, "Base game path validated: found data\\ folder");
        else
            Log(LogLevel.DEBUG, "Base game path validation failed: missing data\\ folder");

        return valid;
    }

    /// <summary>
    /// Validates that a path is a valid module directory.
    /// Looks for .mod files.
    /// </summary>
    public static bool ValidateModulePath(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return false;

        // Check for .mod files
        var modFiles = Directory.GetFiles(path, "*.mod", SearchOption.TopDirectoryOnly);
        if (modFiles.Length > 0)
        {
            Log(LogLevel.DEBUG, $"Module path validated: found {modFiles.Length} .mod file(s)");
            return true;
        }

        // Check subdirectories for .mod files
        try
        {
            foreach (var subDir in Directory.GetDirectories(path))
            {
                var subModFiles = Directory.GetFiles(subDir, "*.mod", SearchOption.TopDirectoryOnly);
                if (subModFiles.Length > 0)
                {
                    Log(LogLevel.DEBUG, $"Module path validated: found .mod files in subdirectory {Path.GetFileName(subDir)}");
                    return true;
                }
            }
        }
        catch
        {
            // Ignore access errors
        }

        Log(LogLevel.DEBUG, $"Module path validation failed: no .mod files found in '{path}' or subdirectories");
        return false;
    }

    /// <summary>
    /// Validation result with message for UI display.
    /// </summary>
    public record PathValidationResult(bool IsValid, string Message);

    /// <summary>
    /// Validate base game path with UI message.
    /// </summary>
    public static PathValidationResult ValidateBaseGamePathWithMessage(string path)
    {
        if (string.IsNullOrEmpty(path))
            return new PathValidationResult(false, "");

        if (ValidateBaseGamePath(path))
            return new PathValidationResult(true, "✅ Valid base game installation (contains data\\ folder)");

        return new PathValidationResult(false, "❌ Invalid path - missing data\\ folder");
    }

    /// <summary>
    /// Validate game documents path with UI message.
    /// </summary>
    public static PathValidationResult ValidateGamePathWithMessage(string path)
    {
        if (string.IsNullOrEmpty(path))
            return new PathValidationResult(false, "");

        if (ValidateGamePath(path))
            return new PathValidationResult(true, "✅ Valid game installation path");

        return new PathValidationResult(false, "❌ Invalid path - missing required directories (ambient, music)");
    }

    /// <summary>
    /// Validate module path with UI message.
    /// </summary>
    public static PathValidationResult ValidateModulePathWithMessage(string path)
    {
        if (string.IsNullOrEmpty(path))
            return new PathValidationResult(false, "");

        if (ValidateModulePath(path))
            return new PathValidationResult(true, "✅ Valid module directory");

        return new PathValidationResult(false, "❌ Invalid path - no .mod files or module directories found");
    }

    /// <summary>
    /// Gets the sound directories relative to game path.
    /// </summary>
    public static List<string> GetSoundDirectories(string gamePath)
    {
        if (string.IsNullOrEmpty(gamePath))
            return new List<string>();

        var soundDirs = new List<string>();
        var categories = new[] { "ambient", "dialog", "music", "soundset" };

        foreach (var category in categories)
        {
            var path = Path.Combine(gamePath, category);
            if (Directory.Exists(path))
            {
                soundDirs.Add(path);
            }
        }

        return soundDirs;
    }

    private static List<string> GetPlatformGamePaths()
    {
        var paths = new List<string>();
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            paths.Add(Path.Combine(documents, "Neverwinter Nights"));
            paths.Add(Path.Combine(userProfile, "Documents", "Neverwinter Nights"));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            paths.Add(Path.Combine(userProfile, "Library", "Application Support", "Neverwinter Nights"));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            paths.Add(Path.Combine(userProfile, ".local", "share", "Neverwinter Nights"));
        }

        return paths;
    }

    private static List<string> GetPlatformBaseGamePaths()
    {
        var paths = new List<string>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Try Steam registry
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                var steamPath = key?.GetValue("SteamPath") as string;
                if (!string.IsNullOrEmpty(steamPath))
                {
                    paths.Add(Path.Combine(steamPath, "steamapps", "common", "Neverwinter Nights"));
                }
            }
            catch
            {
                // Ignore registry errors
            }

            // Common Steam locations
            paths.AddRange(new[]
            {
                @"C:\Program Files (x86)\Steam\steamapps\common\Neverwinter Nights",
                @"C:\Program Files\Steam\steamapps\common\Neverwinter Nights",
                @"D:\SteamLibrary\steamapps\common\Neverwinter Nights",
                @"E:\SteamLibrary\steamapps\common\Neverwinter Nights"
            });

            // GOG paths
            paths.AddRange(new[]
            {
                @"C:\Program Files (x86)\GOG Galaxy\Games\Neverwinter Nights Enhanced Edition",
                @"C:\GOG Games\Neverwinter Nights Enhanced Edition"
            });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            paths.Add("/Applications/Neverwinter Nights.app/Contents/Resources");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            paths.Add(Path.Combine(home, ".steam", "steam", "steamapps", "common", "Neverwinter Nights"));
        }

        return paths;
    }

    private static List<string> GetPlatformModulePaths()
    {
        var paths = new List<string>();

        foreach (var gamePath in GetPlatformGamePaths())
        {
            paths.Add(Path.Combine(gamePath, "modules"));
        }

        return paths;
    }
}
