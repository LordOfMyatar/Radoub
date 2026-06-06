using System.Diagnostics;
using System.Reflection;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;

namespace Radoub.UI.Services.Search;

/// <summary>
/// Information about a Radoub tool for dispatch.
/// </summary>
public class DispatchableToolInfo
{
    /// <summary>Display name (e.g., "Parley")</summary>
    public required string ToolName { get; init; }

    /// <summary>Assembly/executable base name (e.g., "ItemEditor" for Relique)</summary>
    public required string AssemblyName { get; init; }

    /// <summary>Resolved executable path (null if not found)</summary>
    public string? ExecutablePath { get; set; }

    /// <summary>Whether the tool executable was found</summary>
    public bool IsAvailable => !string.IsNullOrEmpty(ExecutablePath) && File.Exists(ExecutablePath);
}

/// <summary>
/// Maps resource types to Radoub tool executables and launches them
/// with --file argument when user wants to edit a search result.
/// </summary>
public class ToolDispatchService
{
    private static readonly Dictionary<ushort, DispatchableToolInfo> ToolMap = new()
    {
        [ResourceTypes.Dlg] = new DispatchableToolInfo { ToolName = "Parley", AssemblyName = "Parley" },
        [ResourceTypes.Utc] = new DispatchableToolInfo { ToolName = "Quartermaster", AssemblyName = "Quartermaster" },
        [ResourceTypes.Bic] = new DispatchableToolInfo { ToolName = "Quartermaster", AssemblyName = "Quartermaster" },
        [ResourceTypes.Uti] = new DispatchableToolInfo { ToolName = "Relique", AssemblyName = "Relique" },
        [ResourceTypes.Utm] = new DispatchableToolInfo { ToolName = "Fence", AssemblyName = "Fence" },
        [ResourceTypes.Jrl] = new DispatchableToolInfo { ToolName = "Manifest", AssemblyName = "Manifest" },
        [ResourceTypes.Utp] = new DispatchableToolInfo { ToolName = "Reliquary", AssemblyName = "Reliquary" },
    };

    /// <summary>
    /// Get tool info for a resource type. Returns null if no tool handles this type.
    /// </summary>
    public DispatchableToolInfo? GetToolForFileType(ushort resourceType)
    {
        return ToolMap.TryGetValue(resourceType, out var info) ? info : null;
    }

    /// <summary>
    /// Returns true if a tool can handle this resource type.
    /// </summary>
    public bool CanDispatch(ushort resourceType)
    {
        return ToolMap.ContainsKey(resourceType);
    }

    /// <summary>
    /// Launch the appropriate tool for a file, opening it with --file argument.
    /// Discovers the tool executable relative to the executing assembly.
    /// </summary>
    /// <returns>True if launched successfully</returns>
    public bool LaunchTool(ushort resourceType, string filePath)
    {
        var info = GetToolForFileType(resourceType);
        if (info == null)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"No tool mapped for resource type: {resourceType}");
            return false;
        }

        if (!info.IsAvailable)
        {
            DiscoverTool(info);
            if (!info.IsAvailable)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Tool not found: {info.ToolName}");
                return false;
            }
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = info.ExecutablePath!,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("--file");
            startInfo.ArgumentList.Add(filePath);

            Process.Start(startInfo);
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Launched {info.ToolName} with file: {Path.GetFileName(filePath)}");
            return true;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"Failed to launch {info.ToolName}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Map a dispatchable tool to the shared RadoubSettings path it registers when it runs.
    /// Mirrors Parley's FindManifestPath: settings written by a tool on launch are the authoritative
    /// way one tool finds another in dev + portable layouts (where tools are NOT siblings on disk).
    /// </summary>
    private static string? GetRegisteredToolPath(string toolName)
    {
        var settings = Radoub.Formats.Settings.RadoubSettings.Instance;
        return toolName switch
        {
            "Parley" => settings.ParleyPath,
            "Manifest" => settings.ManifestPath,
            "Quartermaster" => settings.QuartermasterPath,
            "Fence" => settings.FencePath,
            "Relique" => settings.ReliquePath,
            "Reliquary" => settings.ReliquaryPath,
            "Trebuchet" => settings.TrebuchetPath,
            _ => null
        };
    }

    /// <summary>
    /// Try to discover a tool executable: first the path the tool registered in shared settings when
    /// it last ran, then locations relative to the executing assembly (sibling deployment layouts).
    /// </summary>
    private static void DiscoverTool(DispatchableToolInfo info)
    {
        // Settings-first: the tool writes its own path to RadoubSettings on launch. This is how
        // cross-tool dispatch works in dev (separate bin trees) — relative discovery only finds
        // siblings in a packaged deployment.
        var registered = GetRegisteredToolPath(info.ToolName);
        if (!string.IsNullOrEmpty(registered) && File.Exists(registered))
        {
            info.ExecutablePath = registered;
            return;
        }

        var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (baseDir == null) return;

        // Check sibling directories (tools are typically in parallel directories)
        var parentDir = Path.GetDirectoryName(baseDir);
        if (parentDir == null) return;

        var candidates = new[]
        {
            // Same directory (standalone deployment)
            Path.Combine(baseDir, $"{info.AssemblyName}.exe"),
            Path.Combine(baseDir, info.AssemblyName),
            // Sibling directory
            Path.Combine(parentDir, info.AssemblyName, $"{info.AssemblyName}.exe"),
            Path.Combine(parentDir, info.AssemblyName, info.AssemblyName),
            // Sibling by tool name (when assembly differs)
            Path.Combine(parentDir, info.ToolName, $"{info.AssemblyName}.exe"),
            Path.Combine(parentDir, info.ToolName, info.AssemblyName),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                info.ExecutablePath = candidate;
                return;
            }
        }
    }
}
