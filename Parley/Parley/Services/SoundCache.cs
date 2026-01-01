using System;
using System.Collections.Generic;
using System.IO;
using DialogEditor.Models.Sound;
using Radoub.Formats.Bif;
using Radoub.Formats.Key;
using Radoub.Formats.Logging;

namespace DialogEditor.Services
{
    /// <summary>
    /// Thread-safe cache for sound resources (HAK, KEY, BIF files).
    /// Persists across SoundBrowserWindow instances for performance.
    /// </summary>
    public class SoundCache
    {
        private static readonly Lazy<SoundCache> _instance = new(() => new SoundCache());
        public static SoundCache Instance => _instance.Value;

        private readonly object _lock = new();
        private readonly Dictionary<string, HakCacheEntry> _hakCache = new();
        private readonly Dictionary<string, KeyCacheEntry> _keyCache = new();
        private readonly Dictionary<string, BifFile> _bifCache = new();

        private SoundCache() { }

        /// <summary>
        /// Gets cached HAK entry if valid (file unchanged), otherwise returns null.
        /// </summary>
        public HakCacheEntry? GetHakCache(string hakPath)
        {
            lock (_lock)
            {
                if (!_hakCache.TryGetValue(hakPath, out var cached))
                    return null;

                var lastModified = File.GetLastWriteTimeUtc(hakPath);
                if (cached.LastModified != lastModified)
                {
                    _hakCache.Remove(hakPath);
                    return null;
                }

                return cached;
            }
        }

        /// <summary>
        /// Stores HAK cache entry.
        /// </summary>
        public void SetHakCache(string hakPath, HakCacheEntry entry)
        {
            lock (_lock)
            {
                _hakCache[hakPath] = entry;
            }
        }

        /// <summary>
        /// Gets or loads a KEY file, using cache if valid.
        /// </summary>
        public KeyFile? GetOrLoadKeyFile(string keyPath)
        {
            lock (_lock)
            {
                try
                {
                    var lastModified = File.GetLastWriteTimeUtc(keyPath);

                    if (_keyCache.TryGetValue(keyPath, out var cached) && cached.LastModified == lastModified)
                    {
                        return cached.KeyFile;
                    }

                    var keyFile = KeyReader.Read(keyPath);
                    _keyCache[keyPath] = new KeyCacheEntry
                    {
                        KeyPath = keyPath,
                        LastModified = lastModified,
                        KeyFile = keyFile
                    };

                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"Sound Browser: Loaded KEY file with {keyFile.ResourceEntries.Count} resources from {keyFile.BifEntries.Count} BIFs");

                    return keyFile;
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error loading KEY file {keyPath}: {ex.Message}");
                    return null;
                }
            }
        }

        /// <summary>
        /// Gets or loads a BIF file (metadata only), using cache.
        /// </summary>
        public BifFile? GetOrLoadBifFile(string bifPath)
        {
            lock (_lock)
            {
                try
                {
                    if (_bifCache.TryGetValue(bifPath, out var cached))
                    {
                        return cached;
                    }

                    var bifFile = BifReader.ReadMetadataOnly(bifPath);
                    _bifCache[bifPath] = bifFile;

                    return bifFile;
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error loading BIF file {bifPath}: {ex.Message}");
                    return null;
                }
            }
        }

        /// <summary>
        /// Tries to get a cached BIF file without loading.
        /// </summary>
        public bool TryGetBifFile(string bifPath, out BifFile? bifFile)
        {
            lock (_lock)
            {
                return _bifCache.TryGetValue(bifPath, out bifFile);
            }
        }

        /// <summary>
        /// Clears all caches. Useful for forcing a refresh.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _hakCache.Clear();
                _keyCache.Clear();
                _bifCache.Clear();
            }
        }
    }
}
