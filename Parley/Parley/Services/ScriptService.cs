using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DialogEditor.Utils;

namespace DialogEditor.Services
{
    public class ScriptService
    {
        public static ScriptService Instance { get; } = new ScriptService();

        private Dictionary<string, string> _scriptCache = new Dictionary<string, string>();
        private DateTime _lastCacheUpdate = DateTime.MinValue;
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);

        private ScriptService()
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, "ScriptService initialized");
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
                // Check cache first
                var cacheKey = scriptName.ToLower();
                if (_scriptCache.ContainsKey(cacheKey) && DateTime.Now - _lastCacheUpdate < _cacheExpiry)
                {
                    return _scriptCache[cacheKey];
                }

                // Search for script in known locations
                var scriptContent = await SearchForScriptAsync(scriptName);
                
                if (scriptContent != null)
                {
                    _scriptCache[cacheKey] = scriptContent;
                    _lastCacheUpdate = DateTime.Now;
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

            // Add LOM project paths (from user's development environment)
            var lomPaths = new[]
            {
                @"D:\LOM\Modules\Lords-Neverwinter-Scripts",
                @"D:\LOM\Modules\LordOfMyatar",
                @"D:\LOM\Modules\LNS"
            };

            foreach (var lomPath in lomPaths)
            {
                if (Directory.Exists(lomPath))
                    paths.Add(lomPath);
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
        /// Clears the script cache
        /// </summary>
        public void ClearCache()
        {
            _scriptCache.Clear();
            _lastCacheUpdate = DateTime.MinValue;
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Script cache cleared");
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