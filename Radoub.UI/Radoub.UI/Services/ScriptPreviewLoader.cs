using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;

namespace Radoub.UI.Services;

/// <summary>
/// Loads script content for preview display in ScriptBrowserWindow.
/// Handles module scripts, HAK scripts, and built-in game scripts.
/// </summary>
public class ScriptPreviewLoader
{
    private readonly IScriptBrowserContext? _context;
    private readonly HakScriptScanner _hakScanner;

    public ScriptPreviewLoader(IScriptBrowserContext? context, HakScriptScanner hakScanner)
    {
        _context = context;
        _hakScanner = hakScanner;
    }

    /// <summary>
    /// Loads script content based on script source (module, HAK, or built-in).
    /// </summary>
    /// <param name="scriptEntry">The script to load</param>
    /// <param name="overridePath">Optional override path for module scripts</param>
    /// <returns>Script content with source header, or error message if not found</returns>
    public async Task<string> LoadScriptContentAsync(ScriptEntry scriptEntry, string? overridePath = null)
    {
        try
        {
            string? content = null;

            if (scriptEntry.IsBuiltIn)
            {
                content = await LoadFromGameFilesAsync(scriptEntry);
            }
            else if (scriptEntry.IsFromHak)
            {
                content = await _hakScanner.ExtractScriptFromHakAsync(scriptEntry);
            }
            else
            {
                content = await LoadFromFileSystemAsync(scriptEntry, overridePath);
            }

            if (!string.IsNullOrEmpty(content))
            {
                return content;
            }

            return GetNotFoundMessage(scriptEntry);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error loading script preview: {ex.Message}");
            return $"// Error loading script preview: {ex.Message}";
        }
    }

    private async Task<string?> LoadFromFileSystemAsync(ScriptEntry scriptEntry, string? overridePath)
    {
        // Try direct file path first
        if (!string.IsNullOrEmpty(scriptEntry.FilePath) && File.Exists(scriptEntry.FilePath))
        {
            return await File.ReadAllTextAsync(scriptEntry.FilePath);
        }

        // Try to find in current directory
        var searchPath = overridePath ?? _context?.CurrentFileDirectory;
        if (!string.IsNullOrEmpty(searchPath))
        {
            return await FindAndLoadFromPathAsync(scriptEntry.Name, searchPath);
        }

        return null;
    }

    private async Task<string?> FindAndLoadFromPathAsync(string scriptName, string basePath)
    {
        try
        {
            var scriptFileName = scriptName.EndsWith(".nss", StringComparison.OrdinalIgnoreCase)
                ? scriptName
                : $"{scriptName}.nss";

            if (Directory.Exists(basePath))
            {
                // Use case-insensitive file matching (required for Linux compatibility)
                var matchingFile = Directory.EnumerateFiles(basePath, "*.nss", SearchOption.AllDirectories)
                    .FirstOrDefault(f => Path.GetFileName(f).Equals(scriptFileName, StringComparison.OrdinalIgnoreCase));

                if (matchingFile != null)
                {
                    return await File.ReadAllTextAsync(matchingFile);
                }
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error loading script from path: {ex.Message}");
        }

        return null;
    }

    private async Task<string?> LoadFromGameFilesAsync(ScriptEntry scriptEntry)
    {
        if (_context == null)
            return null;

        try
        {
            // Extract script data from game BIFs on background thread
            var scriptData = await Task.Run(() =>
                _context.FindBuiltInResource(scriptEntry.Name, ResourceTypes.Nss));

            if (scriptData == null || scriptData.Length == 0)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"No .nss source found for built-in script '{scriptEntry.Name}'");
                return $"// Built-in game script: {scriptEntry.Name}\n" +
                       $"// Source: {scriptEntry.Source}\n" +
                       "//\n" +
                       "// Source code (.nss) not found in game files.\n" +
                       "// This may be a compiled-only script.\n" +
                       "// The script name can still be referenced.";
            }

            // NWScript source files are plain text
            var content = System.Text.Encoding.UTF8.GetString(scriptData);

            // Add source header for context
            var header = $"// Built-in script from: {scriptEntry.Source}\n" +
                        $"// ResRef: {scriptEntry.Name}\n" +
                        "//\n";

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Extracted built-in script '{scriptEntry.Name}' ({scriptData.Length} bytes)");

            return header + content;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"Error extracting built-in script: {ex.Message}");
            return null;
        }
    }

    private static string GetNotFoundMessage(ScriptEntry scriptEntry)
    {
        return $"// Script '{scriptEntry.Name}.nss' not found or could not be loaded.\n" +
               "// This may be a compiled game resource (.ncs) without source available.\n" +
               "// Use nwnnsscomp to decompile .ncs files: github.com/niv/neverwinter.nim\n" +
               "// The script name will still be saved to the file.";
    }
}
