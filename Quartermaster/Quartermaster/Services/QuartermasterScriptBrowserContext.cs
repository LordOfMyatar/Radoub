using System.Collections.Generic;
using System.IO;
using System.Linq;
using Radoub.Formats.Common;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Radoub.UI.Services;

namespace Quartermaster.Services;

/// <summary>
/// Quartermaster's implementation of IScriptBrowserContext.
/// Provides creature file-specific paths and settings for the shared ScriptBrowserWindow.
/// </summary>
public class QuartermasterScriptBrowserContext : IScriptBrowserContext
{
    private readonly string? _creatureFilePath;
    private readonly IGameDataService? _gameDataService;

    public QuartermasterScriptBrowserContext(string? creatureFilePath, IGameDataService? gameDataService = null)
    {
        _creatureFilePath = creatureFilePath;
        _gameDataService = gameDataService;
    }

    public string? CurrentFileDirectory
    {
        get
        {
            // First try the open file's directory
            if (!string.IsNullOrEmpty(_creatureFilePath))
            {
                var dir = Path.GetDirectoryName(_creatureFilePath);
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

                // If it's a .mod file, find the working directory (#2355: shared helper)
                if (File.Exists(modulePath) && modulePath.EndsWith(".mod", System.StringComparison.OrdinalIgnoreCase))
                {
                    var workingDir = PathHelper.FindWorkingDirectoryWithFallbacks(modulePath);
                    if (workingDir != null)
                        return workingDir;
                }
            }

            return null;
        }
    }

    public string? NeverwinterNightsPath => RadoubSettings.Instance.NeverwinterNightsPath;

    public string? ExternalEditorPath => null; // Quartermaster doesn't have external editor setting yet

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
