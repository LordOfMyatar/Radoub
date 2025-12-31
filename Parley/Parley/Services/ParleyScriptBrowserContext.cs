using System.Collections.Generic;
using System.IO;
using DialogEditor.Services;
using Radoub.Formats.Common;
using Radoub.UI.Services;

namespace DialogEditor.Services;

/// <summary>
/// Parley's implementation of IScriptBrowserContext.
/// Provides dialog-specific paths and settings for the shared ScriptBrowserWindow.
/// </summary>
public class ParleyScriptBrowserContext : IScriptBrowserContext
{
    private readonly string? _dialogFilePath;

    public ParleyScriptBrowserContext(string? dialogFilePath)
    {
        _dialogFilePath = dialogFilePath;
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

    public bool GameResourcesAvailable => GameResourceService.Instance.IsAvailable;

    public IEnumerable<(string ResRef, string SourcePath)> ListBuiltInScripts()
    {
        var resources = GameResourceService.Instance.ListBuiltInScripts();
        foreach (var resource in resources)
        {
            yield return (resource.ResRef, resource.SourcePath);
        }
    }

    public byte[]? FindBuiltInResource(string resRef, ushort resourceType)
    {
        return GameResourceService.Instance.FindResource(resRef, resourceType);
    }
}
