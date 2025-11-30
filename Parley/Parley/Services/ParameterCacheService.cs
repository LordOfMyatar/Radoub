using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DialogEditor.Services
{
    /// <summary>
    /// Manages parameter value caching with Most Recently Used (MRU) ordering.
    /// Stores cached parameter values organized by script name.
    /// </summary>
    public class ParameterCacheService
    {
        private static readonly Lazy<ParameterCacheService> _instance = new Lazy<ParameterCacheService>(() => new ParameterCacheService());
        public static ParameterCacheService Instance => _instance.Value;

        private const string CacheFileName = "parameter_cache.json";
        private const int DefaultMaxValuesPerParameter = 10;
        private const int DefaultMaxScriptsInCache = 1000;

        private ParameterCache _cache;
        private string _cacheFilePath;

        public int MaxValuesPerParameter { get; set; } = DefaultMaxValuesPerParameter;
        public int MaxScriptsInCache { get; set; } = DefaultMaxScriptsInCache;
        public bool EnableCaching { get; set; } = true;

        private ParameterCacheService()
        {
            // Determine platform-specific cache path
            _cacheFilePath = GetCacheFilePath();
            _cache = LoadCache();
            UnifiedLogger.LogApplication(LogLevel.INFO, $"ParameterCacheService initialized, cache path: {UnifiedLogger.SanitizePath(_cacheFilePath)}");
        }

        /// <summary>
        /// Gets platform-specific cache file path
        /// </summary>
        private string GetCacheFilePath()
        {
            // Use ~/Parley for cross-platform consistency
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var parleyDir = Path.Combine(userProfile, "Parley");
            Directory.CreateDirectory(parleyDir); // Ensure directory exists
            return Path.Combine(parleyDir, CacheFileName);
        }

        /// <summary>
        /// Loads cache from disk or creates new if not exists
        /// </summary>
        private ParameterCache LoadCache()
        {
            if (!File.Exists(_cacheFilePath))
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Parameter cache file not found, creating new cache");
                return new ParameterCache();
            }

            try
            {
                var json = File.ReadAllText(_cacheFilePath);
                var cache = JsonSerializer.Deserialize<ParameterCache>(json);
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded parameter cache: {cache?.Scripts.Count ?? 0} scripts");
                return cache ?? new ParameterCache();
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error loading parameter cache: {ex.Message}");
                return new ParameterCache();
            }
        }

        /// <summary>
        /// Saves cache to disk
        /// </summary>
        private void SaveCache()
        {
            if (!EnableCaching) return;

            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                var json = JsonSerializer.Serialize(_cache, options);
                File.WriteAllText(_cacheFilePath, json);
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Parameter cache saved");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error saving parameter cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Adds or updates a parameter value in cache with MRU ordering.
        /// If value already exists, moves it to front. Otherwise adds at front.
        /// </summary>
        public void AddValue(string scriptName, string parameterKey, string value)
        {
            if (!EnableCaching || string.IsNullOrWhiteSpace(scriptName) || string.IsNullOrWhiteSpace(parameterKey))
                return;

            // Normalize script name (case-insensitive)
            scriptName = scriptName.ToLowerInvariant();

            // Get or create script cache
            if (!_cache.Scripts.ContainsKey(scriptName))
            {
                _cache.Scripts[scriptName] = new ScriptParameterCache();
            }

            var scriptCache = _cache.Scripts[scriptName];

            // Get or create parameter cache
            if (!scriptCache.Parameters.ContainsKey(parameterKey))
            {
                scriptCache.Parameters[parameterKey] = new ParameterValueCache();
            }

            var paramCache = scriptCache.Parameters[parameterKey];

            // Remove existing instance if present
            paramCache.Values.Remove(value);

            // Add at position 0 (MRU)
            paramCache.Values.Insert(0, value);

            // Trim to max size
            if (paramCache.Values.Count > MaxValuesPerParameter)
            {
                paramCache.Values = paramCache.Values.Take(MaxValuesPerParameter).ToList();
            }

            // Trim global script count if needed
            if (_cache.Scripts.Count > MaxScriptsInCache)
            {
                var oldestScripts = _cache.Scripts.OrderBy(s => s.Value.LastUsed).Take(_cache.Scripts.Count - MaxScriptsInCache);
                foreach (var script in oldestScripts.ToList())
                {
                    _cache.Scripts.Remove(script.Key);
                }
            }

            // Update last used timestamp
            scriptCache.LastUsed = DateTime.UtcNow;

            SaveCache();

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Cached parameter: {scriptName}.{parameterKey}={value}");
        }

        /// <summary>
        /// Gets cached values for a parameter, in MRU order
        /// </summary>
        public List<string> GetValues(string scriptName, string parameterKey)
        {
            if (!EnableCaching || string.IsNullOrWhiteSpace(scriptName) || string.IsNullOrWhiteSpace(parameterKey))
                return new List<string>();

            scriptName = scriptName.ToLowerInvariant();

            if (!_cache.Scripts.ContainsKey(scriptName))
                return new List<string>();

            var scriptCache = _cache.Scripts[scriptName];
            if (!scriptCache.Parameters.ContainsKey(parameterKey))
                return new List<string>();

            return scriptCache.Parameters[parameterKey].Values;
        }

        /// <summary>
        /// Gets all parameter keys cached for a script
        /// </summary>
        public List<string> GetParameterKeys(string scriptName)
        {
            if (!EnableCaching || string.IsNullOrWhiteSpace(scriptName))
                return new List<string>();

            scriptName = scriptName.ToLowerInvariant();

            if (!_cache.Scripts.ContainsKey(scriptName))
                return new List<string>();

            return _cache.Scripts[scriptName].Parameters.Keys.ToList();
        }

        /// <summary>
        /// Clears cache for a specific script
        /// </summary>
        public void ClearScriptCache(string scriptName)
        {
            scriptName = scriptName.ToLowerInvariant();
            if (_cache.Scripts.Remove(scriptName))
            {
                SaveCache();
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Cleared cache for script: {scriptName}");
            }
        }

        /// <summary>
        /// Clears entire parameter cache
        /// </summary>
        public void ClearAllCache()
        {
            _cache = new ParameterCache();
            SaveCache();
            UnifiedLogger.LogApplication(LogLevel.INFO, "Cleared entire parameter cache");
        }

        /// <summary>
        /// Gets cache statistics
        /// </summary>
        public CacheStats GetStats()
        {
            int totalParameters = 0;
            int totalValues = 0;

            foreach (var script in _cache.Scripts.Values)
            {
                totalParameters += script.Parameters.Count;
                foreach (var param in script.Parameters.Values)
                {
                    totalValues += param.Values.Count;
                }
            }

            return new CacheStats
            {
                ScriptCount = _cache.Scripts.Count,
                ParameterCount = totalParameters,
                ValueCount = totalValues
            };
        }
    }

    /// <summary>
    /// Root cache structure
    /// </summary>
    public class ParameterCache
    {
        public Dictionary<string, ScriptParameterCache> Scripts { get; set; } = new Dictionary<string, ScriptParameterCache>();
    }

    /// <summary>
    /// Cache for a single script's parameters
    /// </summary>
    public class ScriptParameterCache
    {
        public Dictionary<string, ParameterValueCache> Parameters { get; set; } = new Dictionary<string, ParameterValueCache>();
        public DateTime LastUsed { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Cache for a single parameter's values (MRU ordered)
    /// </summary>
    public class ParameterValueCache
    {
        public List<string> Values { get; set; } = new List<string>();
    }

    /// <summary>
    /// Cache statistics
    /// </summary>
    public class CacheStats
    {
        public int ScriptCount { get; set; }
        public int ParameterCount { get; set; }
        public int ValueCount { get; set; }
    }
}
