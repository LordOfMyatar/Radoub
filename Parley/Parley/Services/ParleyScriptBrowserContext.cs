using System.Collections.Generic;
using System.IO;
using DialogEditor.Services;
using Radoub.Formats.Common;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Radoub.UI.Services;

namespace DialogEditor.Services;

/// <summary>
/// Parley's implementation of IScriptBrowserContext.
/// Provides dialog-specific paths and settings for the shared ScriptBrowserWindow.
/// </summary>
public class ParleyScriptBrowserContext : IScriptBrowserContext
{
    private readonly string? _dialogFilePath;
    private readonly IGameDataService? _gameDataService;

    public ParleyScriptBrowserContext(string? dialogFilePath, IGameDataService? gameDataService = null)
    {
        _dialogFilePath = dialogFilePath;
        _gameDataService = gameDataService;
    }

    public string? CurrentFileDirectory
    {
        get
        {
            // First try the open file's directory
            if (!string.IsNullOrEmpty(_dialogFilePath))
            {
                var dir = Path.GetDirectoryName(_dialogFilePath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    return dir;
            }

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
    /// Checks for module name folder, temp0, or temp01.
    /// </summary>
    private static string? FindWorkingDirectory(string modFilePath)
    {
        var moduleName = Path.GetFileNameWithoutExtension(modFilePath);
        var moduleDir = Path.GetDirectoryName(modFilePath);

        if (string.IsNullOrEmpty(moduleDir))
            return null;

        // Check in priority order (same as Trebuchet)
        var candidates = new[]
        {
            Path.Combine(moduleDir, moduleName),
            Path.Combine(moduleDir, "temp0"),
            Path.Combine(moduleDir, "temp1")
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate) &&
                File.Exists(Path.Combine(candidate, "module.ifo")))
            {
                return candidate;
            }
        }

        return null;
    }

    public string? NeverwinterNightsPath => SettingsService.Instance.NeverwinterNightsPath;

    public string? ExternalEditorPath => SettingsService.Instance.ExternalEditorPath;

    public bool GameResourcesAvailable =>
        _gameDataService?.IsConfigured ?? GameResourceService.Instance.IsAvailable;

    public IEnumerable<(string ResRef, string SourcePath)> ListBuiltInScripts()
    {
        // Prefer IGameDataService if available (shared infrastructure with module support)
        if (_gameDataService != null && _gameDataService.IsConfigured)
        {
            var resources = _gameDataService.ListResources(ResourceTypes.Nss);
            foreach (var resource in resources)
            {
                yield return (resource.ResRef, resource.SourcePath ?? "");
            }
            yield break;
        }

        // Fallback to legacy GameResourceService
        var legacyResources = GameResourceService.Instance.ListBuiltInScripts();
        foreach (var resource in legacyResources)
        {
            yield return (resource.ResRef, resource.SourcePath);
        }
    }

    public byte[]? FindBuiltInResource(string resRef, ushort resourceType)
    {
        // Prefer IGameDataService if available
        if (_gameDataService != null && _gameDataService.IsConfigured)
        {
            return _gameDataService.FindResource(resRef, resourceType);
        }

        // Fallback to legacy GameResourceService
        return GameResourceService.Instance.FindResource(resRef, resourceType);
    }
}
