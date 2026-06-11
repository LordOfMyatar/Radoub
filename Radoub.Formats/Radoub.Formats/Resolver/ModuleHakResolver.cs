using Radoub.Formats.Ifo;
using Radoub.Formats.Logging;

namespace Radoub.Formats.Resolver;

/// <summary>
/// Resolves HAK file paths for a module by reading its module.ifo HakList.
/// Only returns HAK files the module actually references, avoiding the performance
/// penalty of scanning all HAK files in the hak folder (80+ files, 15+ seconds).
/// </summary>
public static class ModuleHakResolver
{
    /// <summary>
    /// Read module.ifo from the module directory and resolve HAK names to file paths.
    /// Searches the provided HAK directories in order for each HAK name.
    /// Returns resolved paths in module.ifo priority order (first = highest priority).
    /// </summary>
    /// <param name="moduleDirectory">Path to the unpacked module directory containing module.ifo.</param>
    /// <param name="hakSearchPaths">Directories to search for HAK files (default hak folder + additional paths).</param>
    /// <returns>Resolved HAK file paths in priority order, or empty list if no module.ifo or no HAKs.</returns>
    public static List<string> ResolveModuleHakPaths(string moduleDirectory, IEnumerable<string> hakSearchPaths)
    {
        if (string.IsNullOrEmpty(moduleDirectory) || !Directory.Exists(moduleDirectory))
            return new List<string>();

        var ifoPath = Path.Combine(moduleDirectory, "module.ifo");
        if (!File.Exists(ifoPath))
        {
            UnifiedLogger.Log(LogLevel.DEBUG, $"No module.ifo found in module directory", "ModuleHakResolver", "GameData");
            return new List<string>();
        }

        IfoFile ifo;
        try
        {
            ifo = IfoReader.Read(ifoPath);
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.ERROR, $"Failed to read module.ifo: {ex.GetType().Name}: {ex.Message}", "ModuleHakResolver", "GameData");
            return new List<string>();
        }

        if (ifo.HakList.Count == 0)
            return new List<string>();

        var searchDirs = hakSearchPaths.Where(Directory.Exists).ToList();
        if (searchDirs.Count == 0)
        {
            UnifiedLogger.Log(LogLevel.WARN, $"No valid HAK search directories found for {ifo.HakList.Count} HAK references", "ModuleHakResolver", "GameData");
            return new List<string>();
        }

        var resolvedPaths = new List<string>();

        foreach (var hakName in ifo.HakList)
        {
            var resolved = FindHakFile(hakName, searchDirs);
            if (resolved != null)
            {
                resolvedPaths.Add(resolved);
            }
            else
            {
                UnifiedLogger.Log(LogLevel.WARN, $"HAK '{hakName}' referenced in module.ifo not found in search paths", "ModuleHakResolver", "GameData");
            }
        }

        UnifiedLogger.Log(LogLevel.INFO, $"Resolved {resolvedPaths.Count}/{ifo.HakList.Count} HAK files from module.ifo", "ModuleHakResolver", "GameData");

        return resolvedPaths;
    }

    /// <summary>
    /// Resolve a list of HAK names (in priority order) to file paths, without
    /// reading module.ifo. Use this when the caller already has the HAK list in
    /// memory (e.g. an editor's working copy with unsaved reorders). Names may
    /// include a trailing ".hak" extension, which is stripped. Returns resolved
    /// paths in input order plus the names that could not be found.
    /// </summary>
    public static HakNameResolution ResolveHakNames(IEnumerable<string> hakNames, IEnumerable<string> hakSearchPaths)
    {
        var searchDirs = hakSearchPaths.Where(Directory.Exists).ToList();
        var resolved = new List<string>();
        var unresolved = new List<string>();

        foreach (var rawName in hakNames)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                continue;

            var name = rawName.Trim();
            if (name.EndsWith(".hak", StringComparison.OrdinalIgnoreCase))
                name = name[..^4];

            var path = searchDirs.Count > 0 ? FindHakFile(name, searchDirs) : null;
            if (path != null)
                resolved.Add(path);
            else
                unresolved.Add(name);
        }

        return new HakNameResolution(resolved, unresolved);
    }

    /// <summary>
    /// Find a HAK file by name (without extension) in the search directories.
    /// Case-insensitive search for cross-platform compatibility.
    /// </summary>
    private static string? FindHakFile(string hakName, List<string> searchDirs)
    {
        var fileName = $"{hakName}.hak";

        foreach (var dir in searchDirs)
        {
            // Try exact match first (fast path)
            var exactPath = Path.Combine(dir, fileName);
            if (File.Exists(exactPath))
                return exactPath;

            // Case-insensitive fallback (needed on Linux/case-sensitive filesystems)
            try
            {
                var match = Directory.GetFiles(dir, "*.hak")
                    .FirstOrDefault(f => Path.GetFileName(f)
                        .Equals(fileName, StringComparison.OrdinalIgnoreCase));

                if (match != null)
                    return match;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                UnifiedLogger.Log(LogLevel.DEBUG, $"Could not search HAK directory: {ex.Message}", "ModuleHakResolver", "GameData");
            }
        }

        return null;
    }
}

/// <summary>
/// Result of resolving HAK names to file paths: the resolved paths (in input
/// order) and the names that could not be found in any search directory.
/// </summary>
public sealed record HakNameResolution(IReadOnlyList<string> Resolved, IReadOnlyList<string> Unresolved);
