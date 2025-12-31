using System;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DialogEditor.Utils;
using DialogEditor.Models;
using DialogEditor.Parsers;

namespace DialogEditor.Services
{
    public class ScriptService
    {
        public static ScriptService Instance { get; } = new ScriptService();

        private class CacheEntry<T>
        {
            public T Value { get; set; }
            public DateTime Timestamp { get; set; }

            public CacheEntry(T value)
            {
                Value = value;
                Timestamp = DateTime.Now;
            }
        }

        private Dictionary<string, CacheEntry<string>> _scriptCache = new Dictionary<string, CacheEntry<string>>();
        private Dictionary<string, CacheEntry<ScriptParameterDeclarations>> _parameterCache = new Dictionary<string, CacheEntry<ScriptParameterDeclarations>>();
        private readonly ScriptParameterParser _parameterParser = new ScriptParameterParser();

        private ScriptService()
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, "ScriptService initialized");
        }

        /// <summary>
        /// Gets the file path of a script by name
        /// </summary>
        /// <param name="scriptName">Name of the script (without .nss extension)</param>
        /// <returns>Full file path or null if not found</returns>
        public string? GetScriptFilePath(string scriptName)
        {
            if (string.IsNullOrWhiteSpace(scriptName))
                return null;

            try
            {
                var scriptFileName = scriptName.EndsWith(".nss", StringComparison.OrdinalIgnoreCase)
                    ? scriptName
                    : $"{scriptName}.nss";

                var searchPaths = GetScriptSearchPaths();

                foreach (var searchPath in searchPaths)
                {
                    if (!Directory.Exists(searchPath))
                        continue;

                    // Use case-insensitive file matching (required for Linux compatibility)
                    var matchingFile = FindFileCaseInsensitive(searchPath, scriptFileName);
                    if (matchingFile != null)
                    {
                        return matchingFile;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error finding script file path for '{scriptName}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Finds a file by name using case-insensitive matching.
        /// Required for cross-platform compatibility (Linux filesystems are case-sensitive).
        /// </summary>
        private string? FindFileCaseInsensitive(string searchPath, string fileName)
        {
            try
            {
                // Get all .nss files and compare case-insensitively
                var allFiles = Directory.EnumerateFiles(searchPath, "*.nss", SearchOption.AllDirectories);
                return allFiles.FirstOrDefault(f =>
                    Path.GetFileName(f).Equals(fileName, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Error searching for file '{fileName}' in '{searchPath}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the content of a script by name
        /// </summary>
        /// <param name="scriptName">Name of the script (without .nss extension)</param>
        /// <returns>Script content or null if not found</returns>
        public async Task<string?> GetScriptContentAsync(string scriptName)
        {
            if (string.IsNullOrWhiteSpace(scriptName))
                return null;

            try
            {
                // Check cache first (session-duration cache, no expiry)
                var cacheKey = scriptName.ToLower();
                if (_scriptCache.TryGetValue(cacheKey, out var cachedEntry))
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"ScriptService: Returning cached script content for '{scriptName}' ({cachedEntry.Value.Length} bytes)");
                    return cachedEntry.Value;
                }

                // Search for script in known locations
                var scriptContent = await SearchForScriptAsync(scriptName);

                if (scriptContent != null)
                {
                    _scriptCache[cacheKey] = new CacheEntry<string>(scriptContent);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"ScriptService: Cached script content for '{scriptName}' ({scriptContent.Length} bytes)");
                }

                return scriptContent;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error getting script content for '{scriptName}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Searches for a script file in the configured game and module directories
        /// </summary>
        private async Task<string?> SearchForScriptAsync(string scriptName)
        {
            var scriptFileName = scriptName.EndsWith(".nss", StringComparison.OrdinalIgnoreCase)
                ? scriptName
                : $"{scriptName}.nss";

            var searchPaths = GetScriptSearchPaths();

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"ScriptService: Searching for '{scriptFileName}' in {searchPaths.Count} paths:");
            foreach (var path in searchPaths)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"  Search path: {UnifiedLogger.SanitizePath(path)} (exists: {Directory.Exists(path)})");
            }

            foreach (var searchPath in searchPaths)
            {
                if (!Directory.Exists(searchPath))
                    continue;

                // Use case-insensitive file matching (required for Linux compatibility)
                var scriptFile = FindFileCaseInsensitive(searchPath, scriptFileName);

                if (scriptFile != null)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Found script '{scriptName}' at: {UnifiedLogger.SanitizePath(scriptFile)}");

                    try
                    {
                        return await File.ReadAllTextAsync(scriptFile);
                    }
                    catch (Exception ex)
                    {
                        UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error reading script file '{scriptFile}': {ex.Message}");
                    }
                }
            }

            // Script not found in filesystem - try built-in game scripts
            var builtInContent = await LoadBuiltInScriptAsync(scriptName);
            if (builtInContent != null)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Found built-in script '{scriptName}' in game BIF files");
                return builtInContent;
            }

            UnifiedLogger.LogApplication(LogLevel.WARN, $"Script '{scriptName}' not found in any configured path or game files");
            return null;
        }

        /// <summary>
        /// Attempts to load a script from the game's built-in BIF files.
        /// </summary>
        private async Task<string?> LoadBuiltInScriptAsync(string scriptName)
        {
            try
            {
                // Check if GameResourceService is available
                if (!GameResourceService.Instance.IsAvailable)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"LoadBuiltInScriptAsync: GameResourceService not available for '{scriptName}'");
                    return null;
                }

                // Extract script name without extension
                var resRef = scriptName.EndsWith(".nss", StringComparison.OrdinalIgnoreCase)
                    ? Path.GetFileNameWithoutExtension(scriptName)
                    : scriptName;

                // Try to find the .nss source file in game BIFs
                var scriptData = await Task.Run(() =>
                    GameResourceService.Instance.FindResource(resRef, ResourceTypes.Nss));

                if (scriptData == null || scriptData.Length == 0)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"LoadBuiltInScriptAsync: No .nss source found for '{scriptName}' in game files");
                    return null;
                }

                // NWScript source files are plain text
                var content = Encoding.UTF8.GetString(scriptData);

                // Add header for context
                var header = $"// Built-in game script: {resRef}\n" +
                            "// Source: NWN game files (BIF archive)\n" +
                            "//\n";

                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"LoadBuiltInScriptAsync: Extracted '{scriptName}' from game BIF ({scriptData.Length} bytes)");

                return header + content;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"LoadBuiltInScriptAsync: Error loading built-in script '{scriptName}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets all available script search paths
        /// </summary>
        public List<string> GetScriptSearchPaths()
        {
            var paths = new List<string>();

            // Add current dialog file's directory first (highest priority - matches Script Browser behavior)
            var currentFilePath = DialogContextService.Instance.CurrentFilePath;
            if (!string.IsNullOrEmpty(currentFilePath))
            {
                var dialogDir = Path.GetDirectoryName(currentFilePath);
                if (!string.IsNullOrEmpty(dialogDir) && Directory.Exists(dialogDir))
                {
                    paths.Add(dialogDir);
                }
            }

            // Add Neverwinter Nights installation path
            var nwnPath = SettingsService.Instance.NeverwinterNightsPath;
            if (!string.IsNullOrEmpty(nwnPath) && Directory.Exists(nwnPath))
            {
                // Add common NWN script directories
                paths.Add(Path.Combine(nwnPath, "scripts"));
                paths.Add(Path.Combine(nwnPath, "nwm"));
                paths.Add(Path.Combine(nwnPath, "override"));
            }

            // Add current module path
            var currentModulePath = SettingsService.Instance.CurrentModulePath;
            if (!string.IsNullOrEmpty(currentModulePath) && Directory.Exists(currentModulePath))
            {
                paths.Add(currentModulePath);

                // Add common module subdirectories
                var scriptsDir = Path.Combine(currentModulePath, "scripts");
                if (Directory.Exists(scriptsDir))
                    paths.Add(scriptsDir);
            }

            // Add all configured module paths
            foreach (var modulePath in SettingsService.Instance.ModulePaths)
            {
                if (!string.IsNullOrEmpty(modulePath) && Directory.Exists(modulePath))
                {
                    paths.Add(modulePath);

                    var scriptsDir = Path.Combine(modulePath, "scripts");
                    if (Directory.Exists(scriptsDir))
                        paths.Add(scriptsDir);
                }
            }

            // Remove duplicates and return
            return paths.Distinct().ToList();
        }

        /// <summary>
        /// Gets all available scripts in the configured directories
        /// </summary>
        public Task<List<string>> GetAvailableScriptsAsync()
        {
            var scripts = new List<string>();
            var searchPaths = GetScriptSearchPaths();

            try
            {
                foreach (var searchPath in searchPaths)
                {
                    if (!Directory.Exists(searchPath))
                        continue;

                    var scriptFiles = Directory.GetFiles(searchPath, "*.nss", SearchOption.AllDirectories);
                    
                    foreach (var scriptFile in scriptFiles)
                    {
                        var scriptName = Path.GetFileNameWithoutExtension(scriptFile);
                        if (!scripts.Contains(scriptName))
                            scripts.Add(scriptName);
                    }
                }

                scripts.Sort();
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Found {scripts.Count} scripts in {searchPaths.Count} search paths");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning for scripts: {ex.Message}");
            }

            return Task.FromResult(scripts);
        }

        /// <summary>
        /// Gets parameter declarations from a script's comment blocks
        /// </summary>
        /// <param name="scriptName">Name of the script (without .nss extension)</param>
        /// <returns>Parameter declarations or empty if not found</returns>
        public async Task<ScriptParameterDeclarations> GetParameterDeclarationsAsync(string scriptName)
        {
            if (string.IsNullOrWhiteSpace(scriptName))
                return ScriptParameterDeclarations.Empty;

            try
            {
                // Check cache first (session-duration cache, no expiry)
                var cacheKey = scriptName.ToLower();
                if (_parameterCache.TryGetValue(cacheKey, out var cachedEntry))
                {
                    var keysCount = cachedEntry.Value.Keys.Count;
                    var valuesCount = cachedEntry.Value.ValuesByKey.Values.Sum(list => list.Count);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"ScriptService: Returning cached parameter declarations for '{scriptName}' ({keysCount} keys, {valuesCount} values)");
                    return cachedEntry.Value;
                }

                // Get script content
                var scriptContent = await GetScriptContentAsync(scriptName);
                if (string.IsNullOrEmpty(scriptContent))
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"No script content found for '{scriptName}' - cannot extract parameter declarations");
                    return ScriptParameterDeclarations.Empty;
                }

                // Parse parameter declarations
                var declarations = _parameterParser.Parse(scriptContent);

                // Cache the result
                _parameterCache[cacheKey] = new CacheEntry<ScriptParameterDeclarations>(declarations);

                if (declarations.HasDeclarations)
                {
                    var totalKeyedValues = declarations.ValuesByKey.Values.Sum(list => list.Count);
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"Extracted and cached parameter declarations from '{scriptName}': {declarations.Keys.Count} keys, {declarations.ValuesByKey.Count} keyed value lists ({totalKeyedValues} total values), {declarations.Values.Count} legacy values");
                }

                return declarations;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"Error getting parameter declarations for '{scriptName}': {ex.Message}");
                return ScriptParameterDeclarations.Empty;
            }
        }

        /// <summary>
        /// Clears the script and parameter caches
        /// </summary>
        public void ClearCache()
        {
            var scriptCount = _scriptCache.Count;
            var paramCount = _parameterCache.Count;

            _scriptCache.Clear();
            _parameterCache.Clear();

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"ScriptService: Cleared caches - {scriptCount} script(s), {paramCount} parameter declaration(s)");
        }

        /// <summary>
        /// Gets cache statistics for debugging
        /// </summary>
        public (int ScriptCount, int ParameterCount) GetCacheStats()
        {
            return (_scriptCache.Count, _parameterCache.Count);
        }

        /// <summary>
        /// Generates a realistic script preview based on the script name and parameters
        /// </summary>
        public async Task<string> GenerateScriptPreviewAsync(string scriptName, Dictionary<string, string> parameters, bool isConditional = true)
        {
            try
            {
                // First try to get actual script content
                var actualContent = await GetScriptContentAsync(scriptName);
                if (!string.IsNullOrEmpty(actualContent))
                {
                    return $"// === ACTUAL SCRIPT CONTENT ===\n// File: {scriptName}.nss\n\n{actualContent}";
                }

                // Fall back to generated preview
                var preview = new System.Text.StringBuilder();
                preview.AppendLine("// === GENERATED SCRIPT PREVIEW ===");
                preview.AppendLine($"// Script: {scriptName}.nss");
                preview.AppendLine("// This is a generated preview - actual script not found");
                preview.AppendLine();

                if (isConditional)
                {
                    preview.AppendLine($"int {scriptName}()");
                    preview.AppendLine("{");
                    
                    foreach (var param in parameters)
                    {
                        if (param.Key.Equals("VAR", StringComparison.OrdinalIgnoreCase))
                        {
                            preview.AppendLine($"    string sCONDITION = GetScriptParam(\"{param.Key}\");");
                            preview.AppendLine($"    if (sCONDITION == \"{param.Value}\") return {param.Value.Equals("TRUE", StringComparison.OrdinalIgnoreCase)};");
                        }
                        else
                        {
                            preview.AppendLine($"    // Parameter: {param.Key} = {param.Value}");
                        }
                    }
                    
                    preview.AppendLine("    return TRUE;");
                }
                else
                {
                    preview.AppendLine($"void {scriptName}()");
                    preview.AppendLine("{");
                    
                    foreach (var param in parameters)
                    {
                        preview.AppendLine($"    // Parameter: {param.Key} = {param.Value}");
                    }
                    
                    preview.AppendLine("    // Action script execution");
                }
                
                preview.AppendLine("}");
                return preview.ToString();
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error generating script preview for '{scriptName}': {ex.Message}");
                return $"// Error generating preview for {scriptName}: {ex.Message}";
            }
        }
    }
}