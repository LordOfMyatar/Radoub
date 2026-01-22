using System.Collections.Generic;
using System.IO;
using DialogEditor.Services;
using Radoub.Formats.Common;
using Radoub.Formats.Services;
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
            if (!string.IsNullOrEmpty(_dialogFilePath))
            {
                var dir = Path.GetDirectoryName(_dialogFilePath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    return dir;
            }
            return null;
        }
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
