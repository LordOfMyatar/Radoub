using System;
using System.Diagnostics;
using System.IO;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;

namespace Radoub.UI.Services;

/// <summary>
/// Shared helper for opening NWScript (.nss) files in the user's editor. The editor is the
/// single cross-tool <see cref="RadoubSettings.CodeEditorPath"/> (configured in Trebuchet); when
/// unset or missing, the file opens with the OS default handler (VS Code / Cursor / Notepad — the
/// user's .nss association). #2295.
/// </summary>
public static class ExternalEditorService
{
    /// <summary>
    /// Resolve a script ResRef to a .nss file path, searching the current file's directory first,
    /// then the module directory. Returns null if the name is blank or no file is found. Pure —
    /// no side effects (testable seam).
    /// </summary>
    public static string? ResolveScriptPath(string? scriptName, string? currentFileDir, string? moduleDir)
    {
        if (string.IsNullOrWhiteSpace(scriptName)) return null;

        var name = scriptName.Replace(".nss", "", StringComparison.OrdinalIgnoreCase);
        var fileName = $"{name}.nss";

        foreach (var dir in new[] { currentFileDir, moduleDir })
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            var candidate = Path.Combine(dir, fileName);
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    /// <summary>
    /// Decide which editor executable to launch. Returns the configured path when it exists, else
    /// null to signal "use the OS default handler". Pure (the file-existence check is injected).
    /// </summary>
    public static string? ChooseEditor(string? configuredPath, Func<string, bool> fileExists)
    {
        if (string.IsNullOrWhiteSpace(configuredPath)) return null;
        return fileExists(configuredPath) ? configuredPath : null;
    }

    /// <summary>
    /// Open a script in the user's editor. Resolves the .nss near the current file / module, then
    /// launches the configured editor or the OS default. Returns false if the script can't be
    /// found or the launch throws.
    /// </summary>
    public static bool OpenScript(string? scriptName, string? currentFileDir, string? moduleDir = null)
    {
        var scriptPath = ResolveScriptPath(scriptName, currentFileDir, moduleDir);
        if (scriptPath is null)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"OpenScript: '{scriptName}.nss' not found near the open file or module.");
            return false;
        }

        try
        {
            var editor = ChooseEditor(RadoubSettings.Instance.CodeEditorPath, File.Exists);
            if (editor != null)
            {
                var psi = new ProcessStartInfo { FileName = editor, UseShellExecute = false };
                psi.ArgumentList.Add(scriptPath); // verbatim — no manual quoting
                Process.Start(psi);
            }
            else
            {
                // OS default handler for .nss
                Process.Start(new ProcessStartInfo(scriptPath) { UseShellExecute = true });
            }
            return true;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"OpenScript: failed to open '{UnifiedLogger.SanitizePath(scriptPath)}': {ex.Message}");
            return false;
        }
    }
}
