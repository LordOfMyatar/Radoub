using System.Collections.Generic;
using System.IO;
using System.Linq;
using Radoub.Formats.Common;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Radoub.UI.Services;

namespace RadoubLauncher.Services;

/// <summary>
/// Trebuchet's implementation of IScriptBrowserContext.
/// Provides module-specific paths for the shared ScriptBrowserWindow.
/// </summary>
public class TrebuchetScriptBrowserContext : IScriptBrowserContext
{
    private readonly string? _moduleWorkingDirectory;
    private readonly IGameDataService? _gameDataService;

    public TrebuchetScriptBrowserContext(string? moduleWorkingDirectory, IGameDataService? gameDataService = null)
    {
        _moduleWorkingDirectory = moduleWorkingDirectory;
        _gameDataService = gameDataService;
    }

    public string? CurrentFileDirectory
    {
        get
        {
            // First try the module's working directory
            if (!string.IsNullOrEmpty(_moduleWorkingDirectory) && Directory.Exists(_moduleWorkingDirectory))
                return _moduleWorkingDirectory;

            // Fall back to RadoubSettings.CurrentModulePath (set by Trebuchet)
            var modulePath = RadoubSettings.Instance.CurrentModulePath;
            if (!string.IsNullOrEmpty(modulePath))
            {
                // If it's a directory, use it directly
                if (Directory.Exists(modulePath))
                    return modulePath;

                // If it's a .mod file, find the working directory
                if (File.Exists(modulePath) && modulePath.EndsWith(".mod", System.StringComparison.OrdinalIgnoreCase))
                {
                    var workingDir = FindWorkingDirectory(modulePath);
                    if (workingDir != null)
                        return workingDir;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Find the unpacked working directory for a .mod file.
    /// Checks for module name folder, temp0, or temp1.
    /// </summary>
    private static string? FindWorkingDirectory(string modFilePath)
    {
        var moduleName = Path.GetFileNameWithoutExtension(modFilePath);
        var moduleDir = Path.GetDirectoryName(modFilePath);

        if (string.IsNullOrEmpty(moduleDir))
            return null;

        // Check in priority order (same as Trebuchet module editor)
        var candidates = new[]
        {
            Path.Combine(moduleDir, moduleName),
            Path.Combine(moduleDir, "temp0"),
            Path.Combine(moduleDir, "temp1")
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
                return candidate;
        }

        return null;
    }

    public string? NeverwinterNightsPath => RadoubSettings.Instance.NeverwinterNightsPath;

    public string? ExternalEditorPath => null; // Trebuchet doesn't have external editor setting yet

    public bool GameResourcesAvailable => _gameDataService?.IsConfigured ?? false;

    public IEnumerable<(string ResRef, string SourcePath)> ListBuiltInScripts()
    {
        if (_gameDataService == null || !_gameDataService.IsConfigured)
            yield break;

        // List .nss resources from game BIFs
        var resources = _gameDataService.ListResources(ResourceTypes.Nss);
        foreach (var resource in resources)
        {
            yield return (resource.ResRef, resource.SourcePath ?? "");
        }
    }

    public byte[]? FindBuiltInResource(string resRef, ushort resourceType)
    {
        return _gameDataService?.FindResource(resRef, resourceType);
    }
}
