using System.Collections.Generic;
using System.IO;
using Radoub.Formats.Common;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Radoub.UI.Services;

namespace PlaceableEditor.Services;

/// <summary>
/// Reliquary's <see cref="IScriptBrowserContext"/>: supplies the open placeable's directory,
/// the NWN path, and built-in script lookup for the shared ScriptBrowserWindow. Mirrors
/// Quartermaster's context, swapping the creature file for the placeable (.utp) file.
/// </summary>
public class ReliquaryScriptBrowserContext : IScriptBrowserContext
{
    private readonly string? _placeableFilePath;
    private readonly IGameDataService? _gameDataService;

    public ReliquaryScriptBrowserContext(string? placeableFilePath, IGameDataService? gameDataService = null)
    {
        _placeableFilePath = placeableFilePath;
        _gameDataService = gameDataService;
    }

    public string? CurrentFileDirectory
    {
        get
        {
            if (!string.IsNullOrEmpty(_placeableFilePath))
            {
                var dir = Path.GetDirectoryName(_placeableFilePath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    return dir;
            }

            var modulePath = RadoubSettings.Instance.CurrentModulePath;
            if (!string.IsNullOrEmpty(modulePath))
            {
                if (Directory.Exists(modulePath))
                    return modulePath;

                if (File.Exists(modulePath) && modulePath.EndsWith(".mod", System.StringComparison.OrdinalIgnoreCase))
                    return FindWorkingDirectory(modulePath);
            }

            return null;
        }
    }

    private static string? FindWorkingDirectory(string modFilePath)
    {
        var moduleName = Path.GetFileNameWithoutExtension(modFilePath);
        var moduleDir = Path.GetDirectoryName(modFilePath);
        if (string.IsNullOrEmpty(moduleDir)) return null;

        var candidates = new[]
        {
            Path.Combine(moduleDir, moduleName),
            Path.Combine(moduleDir, "temp0"),
            Path.Combine(moduleDir, "temp1")
        };

        foreach (var candidate in candidates)
            if (Directory.Exists(candidate)) return candidate;

        return null;
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
