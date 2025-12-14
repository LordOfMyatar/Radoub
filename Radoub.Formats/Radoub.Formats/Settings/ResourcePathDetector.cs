using System.Runtime.InteropServices;

namespace Radoub.Formats.Settings;

/// <summary>
/// Helper class for detecting Neverwinter Nights resource paths across platforms.
/// Adapted from Parley's ResourcePathHelper for shared use.
/// </summary>
public static class ResourcePathDetector
{
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
                return path;
            }
        }

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
            if (ValidateBaseGamePath(path))
            {
                return path;
            }
        }

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
                return modulePath;
            }
        }

        var possiblePaths = GetPlatformModulePaths();
        foreach (var path in possiblePaths)
        {
            if (ValidateModulePath(path))
            {
                return path;
            }
        }

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
        return Directory.Exists(dataPath);
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
            return true;

        // Check subdirectories for .mod files
        try
        {
            foreach (var subDir in Directory.GetDirectories(path))
            {
                var subModFiles = Directory.GetFiles(subDir, "*.mod", SearchOption.TopDirectoryOnly);
                if (subModFiles.Length > 0)
                    return true;
            }
        }
        catch
        {
            // Ignore access errors
        }

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
            return new PathValidationResult(true, "Valid - found data\\ folder");

        return new PathValidationResult(false, "Invalid - missing data\\ folder");
    }

    /// <summary>
    /// Validate game documents path with UI message.
    /// </summary>
    public static PathValidationResult ValidateGamePathWithMessage(string path)
    {
        if (string.IsNullOrEmpty(path))
            return new PathValidationResult(false, "");

        if (ValidateGamePath(path))
            return new PathValidationResult(true, "Valid - found ambient and music folders");

        return new PathValidationResult(false, "Invalid - missing required folders");
    }

    /// <summary>
    /// Validate module path with UI message.
    /// </summary>
    public static PathValidationResult ValidateModulePathWithMessage(string path)
    {
        if (string.IsNullOrEmpty(path))
            return new PathValidationResult(false, "");

        if (ValidateModulePath(path))
            return new PathValidationResult(true, "Valid - found .mod files");

        return new PathValidationResult(false, "Invalid - no .mod files found");
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
