using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Radoub.Formats.Logging;

namespace RadoubLauncher.Services;

/// <summary>
/// Helper class for detecting and validating Neverwinter Nights resource paths across platforms.
/// </summary>
public static class ResourcePathHelper
{
    /// <summary>
    /// Attempts to auto-detect the Neverwinter Nights user data path (Documents/Neverwinter Nights).
    /// </summary>
    public static string? AutoDetectGamePath()
    {
        var possiblePaths = GetPlatformGamePaths();

        foreach (var path in possiblePaths)
        {
            if (ValidateGamePath(path))
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Auto-detected user data path: {UnifiedLogger.SanitizePath(path)}");
                return path;
            }
        }

        UnifiedLogger.LogApplication(LogLevel.WARN, "Could not auto-detect user data path");
        return null;
    }

    /// <summary>
    /// Attempts to auto-detect the base game installation path (Steam/GOG).
    /// </summary>
    public static string? AutoDetectBaseGamePath()
    {
        var possiblePaths = GetPlatformBaseGamePaths();

        foreach (var path in possiblePaths)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Checking base game path: {path}");
            if (ValidateBaseGamePath(path))
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Auto-detected base game path: {UnifiedLogger.SanitizePath(path)}");
                return path;
            }
        }

        UnifiedLogger.LogApplication(LogLevel.WARN, "Could not auto-detect base game installation");
        return null;
    }

    /// <summary>
    /// Validates that a path is a valid NWN user data directory.
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
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Game path validation failed: missing '{dir}' directory");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates that a path is a valid NWN base game installation (Steam/GOG).
    /// Base game installation should have data\ folder.
    /// </summary>
    public static bool ValidateBaseGamePath(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return false;

        var dataPath = Path.Combine(path, "data");
        bool valid = Directory.Exists(dataPath);

        if (valid)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Base game path validated: found data\\ folder");
        }
        else
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Base game path validation failed: missing data\\ folder");
        }

        return valid;
    }

    /// <summary>
    /// Returns validation result with message for UI display.
    /// </summary>
    public static PathValidationResult ValidateGamePathWithMessage(string path)
    {
        if (string.IsNullOrEmpty(path))
            return new PathValidationResult(false, "");

        if (ValidateGamePath(path))
            return new PathValidationResult(true, "Valid NWN user data directory");

        return new PathValidationResult(false, "Invalid - missing required directories (ambient, music)");
    }

    /// <summary>
    /// Returns validation result with message for UI display.
    /// </summary>
    public static PathValidationResult ValidateBaseGamePathWithMessage(string path)
    {
        if (string.IsNullOrEmpty(path))
            return new PathValidationResult(false, "");

        if (ValidateBaseGamePath(path))
            return new PathValidationResult(true, "Valid base game installation (contains data\\ folder)");

        return new PathValidationResult(false, "Invalid - missing data\\ folder");
    }

    /// <summary>
    /// Validates that a path is a valid NWN module working directory.
    /// </summary>
    public static bool ValidateModulePath(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return false;

        // A valid module directory should contain module.ifo (the module info file)
        var moduleIfo = Path.Combine(path, "module.ifo");
        if (File.Exists(moduleIfo))
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Module path validated: found module.ifo");
            return true;
        }

        // Also check for common GFF files that indicate a module
        var gffExtensions = new[] { ".are", ".git", ".utc", ".uti", ".dlg", ".nss" };
        foreach (var ext in gffExtensions)
        {
            var files = Directory.GetFiles(path, $"*{ext}", SearchOption.TopDirectoryOnly);
            if (files.Length > 0)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Module path validated: found {ext} files");
                return true;
            }
        }

        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Module path validation failed: no module files found");
        return false;
    }

    /// <summary>
    /// Returns validation result with detailed message for UI display.
    /// </summary>
    public static PathValidationResult ValidateModulePathWithMessage(string path)
    {
        if (string.IsNullOrEmpty(path))
            return new PathValidationResult(false, "");

        if (!Directory.Exists(path))
            return new PathValidationResult(false, "Directory does not exist");

        // Check for module.ifo
        var moduleIfo = Path.Combine(path, "module.ifo");
        if (File.Exists(moduleIfo))
        {
            // Get some stats about the module
            var fileCount = Directory.GetFiles(path).Length;
            return new PathValidationResult(true, $"Valid module ({fileCount} files)");
        }

        // Check for GFF files
        var gffExtensions = new[] { ".are", ".git", ".utc", ".uti", ".dlg", ".nss" };
        foreach (var ext in gffExtensions)
        {
            var files = Directory.GetFiles(path, $"*{ext}", SearchOption.TopDirectoryOnly);
            if (files.Length > 0)
            {
                var fileCount = Directory.GetFiles(path).Length;
                return new PathValidationResult(true, $"Module directory ({fileCount} files, no module.ifo)");
            }
        }

        return new PathValidationResult(false, "Not a module - no module.ifo or GFF files found");
    }

    /// <summary>
    /// Gets platform-specific default user data paths.
    /// </summary>
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

    /// <summary>
    /// Gets platform-specific base game installation paths (Steam, GOG, etc.)
    /// </summary>
    private static List<string> GetPlatformBaseGamePaths()
    {
        var paths = new List<string>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Try Steam registry first
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                var steamPath = key?.GetValue("SteamPath") as string;
                if (!string.IsNullOrEmpty(steamPath))
                {
                    var nwnPath = Path.Combine(steamPath, "steamapps", "common", "Neverwinter Nights");
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Found Steam path from registry: {steamPath}");
                    paths.Add(nwnPath);
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Could not read Steam registry: {ex.Message}");
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
}

/// <summary>
/// Result of a path validation with message for UI display.
/// </summary>
public record PathValidationResult(bool IsValid, string Message);
