using System.Collections.Generic;
using System.IO;
using Radoub.Formats.Common;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Radoub.UI.Services;

namespace ItemEditor.Services;

/// <summary>
/// Relique's <see cref="IScriptBrowserContext"/>: supplies the open item's directory and
/// the NWN path for the shared SaveBlueprintWindow (#2515). Mirrors Reliquary's context,
/// swapping the placeable (.utp) file for the item (.uti) file. Relique has no script
/// browser, so built-in script lookup is a no-op — only CurrentFileDirectory is consumed.
/// </summary>
public class ReliqueScriptBrowserContext : IScriptBrowserContext
{
    private readonly string? _itemFilePath;
    private readonly IGameDataService? _gameDataService;

    public ReliqueScriptBrowserContext(string? itemFilePath, IGameDataService? gameDataService = null)
    {
        _itemFilePath = itemFilePath;
        _gameDataService = gameDataService;
    }

    public string? CurrentFileDirectory
    {
        get
        {
            if (!string.IsNullOrEmpty(_itemFilePath))
            {
                var dir = Path.GetDirectoryName(_itemFilePath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    return dir;
            }

            var modulePath = RadoubSettings.Instance.CurrentModulePath;
            if (!string.IsNullOrEmpty(modulePath))
            {
                if (Directory.Exists(modulePath))
                    return modulePath;

                // #2355: shared helper
                if (File.Exists(modulePath) && modulePath.EndsWith(".mod", System.StringComparison.OrdinalIgnoreCase))
                    return PathHelper.FindWorkingDirectoryWithFallbacks(modulePath);
            }

            return null;
        }
    }

    public string? NeverwinterNightsPath => RadoubSettings.Instance.NeverwinterNightsPath;

    public string? ExternalEditorPath => null;

    public bool GameResourcesAvailable => _gameDataService?.IsConfigured ?? false;

    public IEnumerable<(string ResRef, string SourcePath)> ListBuiltInScripts()
    {
        if (_gameDataService == null || !_gameDataService.IsConfigured)
            yield break;

        foreach (var resource in _gameDataService.ListResources(ResourceTypes.Nss))
            yield return (resource.ResRef, resource.SourcePath ?? "");
    }

    public byte[]? FindBuiltInResource(string resRef, ushort resourceType)
        => _gameDataService?.FindResource(resRef, resourceType);
}
