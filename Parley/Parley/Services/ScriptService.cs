using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

                    var scriptFiles = Directory.GetFiles(searchPath, scriptFileName, SearchOption.AllDirectories);

                    if (scriptFiles.Length > 0)
                    {
                        return scriptFiles[0]; // Return the first match
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
            
            foreach (var searchPath in searchPaths)
            {
                if (!Directory.Exists(searchPath))
                    continue;

                var scriptFiles = Directory.GetFiles(searchPath, scriptFileName, SearchOption.AllDirectories);
                
                if (scriptFiles.Length > 0)
                {
                    var scriptFile = scriptFiles[0]; // Take the first match
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

            UnifiedLogger.LogApplication(LogLevel.WARN, $"Script '{scriptName}' not found in any configured path");
            return null;
        }

        /// <summary>
        /// Gets all available script search paths
        /// </summary>
        private List<string> GetScriptSearchPaths()
        {
            var paths = new List<string>();

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