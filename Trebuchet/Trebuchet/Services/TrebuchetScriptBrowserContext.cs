using System.Collections.Generic;
using System.IO;
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
            return ModulePathHelper.FindWorkingDirectoryWithFallbacks(RadoubSettings.Instance.CurrentModulePath);
        }
    }

    public string? NeverwinterNightsPath => RadoubSettings.Instance.NeverwinterNightsPath;

    public string? ExternalEditorPath
    {
        get
        {
            var path = SettingsService.Instance.CodeEditorPath;
            return string.IsNullOrEmpty(path) ? null : path;
        }
    }

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
