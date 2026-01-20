using Radoub.Formats.Bif;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Key;
using Radoub.Formats.Logging;
using Radoub.Formats.Tlk;

namespace Radoub.Formats.Resolver;

/// <summary>
/// Resolves game resources using NWN's standard search order:
/// 1. Override folder (loose files)
/// 2. HAK files (in configured order)
/// 3. Base game BIF files (via KEY index)
///
/// Reference: NWN resource loading order documentation
/// </summary>
public class GameResourceResolver : IDisposable
{
    private readonly GameResourceConfig _config;
    private KeyFile? _keyFile;
    private CachedKeyIndex? _cachedKeyIndex;
    private readonly Dictionary<string, BifFile> _bifCache = new();
    private readonly Dictionary<string, ErfFile> _hakCache = new();
    private TlkFile? _baseTlk;
    private TlkFile? _customTlk;
    private bool _disposed;
    private bool _keyIndexLoaded;

    /// <summary>
    /// Create a new resolver with the specified configuration.
    /// </summary>
    public GameResourceResolver(GameResourceConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Find a resource by ResRef and type, returning just the data.
    /// Returns null if not found.
    /// </summary>
    public byte[]? FindResource(string resRef, ushort resourceType)
    {
        return FindResourceWithSource(resRef, resourceType)?.Data;
    }

    /// <summary>
    /// Find a resource by ResRef and type, including source information.
    /// Returns null if not found.
    /// </summary>
    public ResourceResult? FindResourceWithSource(string resRef, ushort resourceType)
    {
        // 1. Check override folder
        var overrideResult = FindInOverride(resRef, resourceType);
        if (overrideResult != null)
            return overrideResult;

        // 2. Check HAK files (in order)
        var hakResult = FindInHaks(resRef, resourceType);
        if (hakResult != null)
            return hakResult;

        // 3. Check BIF files via KEY
        var bifResult = FindInBif(resRef, resourceType);
        if (bifResult != null)
            return bifResult;

        return null;
    }

    /// <summary>
    /// List all resources of a specific type across all sources.
    /// Later sources are hidden by earlier ones (override > HAK > BIF).
    /// </summary>
    public IEnumerable<ResourceInfo> ListResources(ushort resourceType)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<ResourceInfo>();

        // Override files
        foreach (var info in ListOverrideResources(resourceType))
        {
            if (seen.Add(info.ResRef))
                results.Add(info);
        }

        // HAK files
        foreach (var info in ListHakResources(resourceType))
        {
            if (seen.Add(info.ResRef))
                results.Add(info);
        }

        // BIF files
        foreach (var info in ListBifResources(resourceType))
        {
            if (seen.Add(info.ResRef))
                results.Add(info);
        }

        return results;
    }

    /// <summary>
    /// Get a TLK string by StrRef.
    /// StrRefs >= 16777216 (0x1000000) use custom TLK, others use base TLK.
    /// </summary>
    public string? GetTlkString(uint strRef)
    {
        const uint CustomTlkOffset = 16777216; // 0x1000000

        if (strRef >= CustomTlkOffset)
        {
            EnsureCustomTlkLoaded();
            return _customTlk?.GetString(strRef - CustomTlkOffset);
        }

        EnsureBaseTlkLoaded();
        return _baseTlk?.GetString(strRef);
    }

    /// <summary>
    /// Get a TLK entry by StrRef (includes sound info).
    /// </summary>
    public TlkEntry? GetTlkEntry(uint strRef)
    {
        const uint CustomTlkOffset = 16777216;

        if (strRef >= CustomTlkOffset)
        {
            EnsureCustomTlkLoaded();
            return _customTlk?.GetEntry(strRef - CustomTlkOffset);
        }

        EnsureBaseTlkLoaded();
        return _baseTlk?.GetEntry(strRef);
    }

    #region Override Resolution

    private ResourceResult? FindInOverride(string resRef, ushort resourceType)
    {
        var overridePath = GetOverridePath();
        if (overridePath == null || !Directory.Exists(overridePath))
            return null;

        var extension = ResourceTypes.GetExtension(resourceType);
        var fileName = resRef + extension;
        var filePath = Path.Combine(overridePath, fileName);

        // Case-insensitive search (required for cross-platform support)
        if (!File.Exists(filePath))
        {
            // Directory.GetFiles pattern matching is case-sensitive on Linux
            // Need to enumerate all files and compare case-insensitively
            var matchingFile = Directory.EnumerateFiles(overridePath, "*" + extension, SearchOption.TopDirectoryOnly)
                .FirstOrDefault(f => Path.GetFileName(f).Equals(fileName, StringComparison.OrdinalIgnoreCase));

            if (matchingFile == null)
                return null;
            filePath = matchingFile;
        }

        var data = File.ReadAllBytes(filePath);
        return new ResourceResult(data, ResourceSource.Override, filePath, resRef, resourceType);
    }

    private IEnumerable<ResourceInfo> ListOverrideResources(ushort resourceType)
    {
        var overridePath = GetOverridePath();
        if (overridePath == null || !Directory.Exists(overridePath))
            yield break;

        var extension = ResourceTypes.GetExtension(resourceType);
        var pattern = "*" + extension;

        foreach (var file in Directory.GetFiles(overridePath, pattern, SearchOption.TopDirectoryOnly))
        {
            var resRef = Path.GetFileNameWithoutExtension(file);
            yield return new ResourceInfo
            {
                ResRef = resRef,
                ResourceType = resourceType,
                Source = ResourceSource.Override,
                SourcePath = file
            };
        }
    }

    private string? GetOverridePath()
    {
        if (!string.IsNullOrEmpty(_config.OverridePath))
            return _config.OverridePath;

        if (!string.IsNullOrEmpty(_config.GameDataPath))
            return Path.Combine(_config.GameDataPath, "override");

        return null;
    }

    #endregion

    #region HAK Resolution

    private ResourceResult? FindInHaks(string resRef, ushort resourceType)
    {
        foreach (var hakPath in _config.HakPaths)
        {
            if (!File.Exists(hakPath))
                continue;

            var hak = GetOrLoadHak(hakPath);
            if (hak == null)
                continue;

            var entry = hak.FindResource(resRef, resourceType);
            if (entry != null)
            {
                var data = ErfReader.ExtractResource(hakPath, entry);
                return new ResourceResult(data, ResourceSource.Hak, hakPath, resRef, resourceType);
            }
        }

        return null;
    }

    private IEnumerable<ResourceInfo> ListHakResources(ushort resourceType)
    {
        foreach (var hakPath in _config.HakPaths)
        {
            if (!File.Exists(hakPath))
                continue;

            var hak = GetOrLoadHak(hakPath);
            if (hak == null)
                continue;

            foreach (var entry in hak.GetResourcesByType(resourceType))
            {
                yield return new ResourceInfo
                {
                    ResRef = entry.ResRef,
                    ResourceType = resourceType,
                    Source = ResourceSource.Hak,
                    SourcePath = hakPath
                };
            }
        }
    }

    private ErfFile? GetOrLoadHak(string hakPath)
    {
        if (_config.CacheArchives && _hakCache.TryGetValue(hakPath, out var cached))
            return cached;

        try
        {
            var hak = ErfReader.Read(hakPath);
            if (_config.CacheArchives)
                _hakCache[hakPath] = hak;
            return hak;
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.ERROR, $"Failed to load HAK '{hakPath}': {ex.GetType().Name}: {ex.Message}", "GameResourceResolver", "Resolver");
            return null;
        }
    }

    #endregion

    #region BIF Resolution

    private ResourceResult? FindInBif(string resRef, ushort resourceType)
    {
        EnsureKeyLoaded();
        if (_cachedKeyIndex == null)
            return null;

        // Find resource in cached index
        var entry = _cachedKeyIndex.Resources.FirstOrDefault(r =>
            r.ResRef.Equals(resRef, StringComparison.OrdinalIgnoreCase) &&
            r.ResourceType == resourceType);
        if (entry == null)
            return null;

        // Get BIF filename from cached index
        if (entry.BifIndex < 0 || entry.BifIndex >= _cachedKeyIndex.BifFiles.Count)
            return null;

        var bifFilename = _cachedKeyIndex.BifFiles[entry.BifIndex].Filename;
        var bifPath = ResolveBifPath(bifFilename);
        if (bifPath == null || !File.Exists(bifPath))
            return null;

        var bif = GetOrLoadBif(bifPath);
        if (bif == null)
            return null;

        var data = bif.ExtractVariableResource(entry.VariableTableIndex);
        if (data == null)
            return null;

        return new ResourceResult(data, ResourceSource.Bif, bifPath, resRef, resourceType);
    }

    private IEnumerable<ResourceInfo> ListBifResources(ushort resourceType)
    {
        EnsureKeyLoaded();
        if (_cachedKeyIndex == null)
            yield break;

        foreach (var entry in _cachedKeyIndex.Resources.Where(r => r.ResourceType == resourceType))
        {
            var bifFilename = entry.BifIndex >= 0 && entry.BifIndex < _cachedKeyIndex.BifFiles.Count
                ? _cachedKeyIndex.BifFiles[entry.BifIndex].Filename
                : null;
            var bifPath = bifFilename != null ? ResolveBifPath(bifFilename) : null;

            yield return new ResourceInfo
            {
                ResRef = entry.ResRef,
                ResourceType = resourceType,
                Source = ResourceSource.Bif,
                SourcePath = bifPath ?? ""
            };
        }
    }

    private void EnsureKeyLoaded()
    {
        if (_keyIndexLoaded)
            return;

        _keyIndexLoaded = true;

        var keyPath = _config.KeyFilePath;
        if (string.IsNullOrEmpty(keyPath) && !string.IsNullOrEmpty(_config.GameDataPath))
            keyPath = Path.Combine(_config.GameDataPath, "nwn_base.key");

        if (string.IsNullOrEmpty(keyPath) || !File.Exists(keyPath))
            return;

        // Try to load from persistent cache first
        _cachedKeyIndex = KeyIndexCache.TryLoad(keyPath);
        if (_cachedKeyIndex != null)
        {
            // Cache hit - we have the index, no need to parse KEY file
            return;
        }

        // Cache miss - parse KEY file and save to cache
        try
        {
            _keyFile = KeyReader.Read(keyPath);

            // Build cache from parsed KEY file
            _cachedKeyIndex = new CachedKeyIndex();

            foreach (var bif in _keyFile.BifEntries)
            {
                _cachedKeyIndex.BifFiles.Add(new CachedBifEntry
                {
                    Filename = bif.Filename,
                    FileSize = bif.FileSize
                });
            }

            foreach (var entry in _keyFile.ResourceEntries)
            {
                _cachedKeyIndex.Resources.Add(new CachedResourceEntry
                {
                    ResRef = entry.ResRef,
                    ResourceType = entry.ResourceType,
                    BifIndex = entry.BifIndex,
                    VariableTableIndex = entry.VariableTableIndex
                });
            }

            // Save cache for next time
            KeyIndexCache.Save(keyPath, _cachedKeyIndex);

            // Clear the full KeyFile - we only need the cached index now
            _keyFile = null;
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.ERROR, $"Failed to load KEY '{keyPath}': {ex.GetType().Name}: {ex.Message}", "GameResourceResolver", "Resolver");
        }
    }

    private BifFile? GetOrLoadBif(string bifPath)
    {
        if (_config.CacheArchives && _bifCache.TryGetValue(bifPath, out var cached))
            return cached;

        try
        {
            var bif = BifReader.Read(bifPath);
            if (_config.CacheArchives)
                _bifCache[bifPath] = bif;
            return bif;
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.ERROR, $"Failed to load BIF '{bifPath}': {ex.GetType().Name}: {ex.Message}", "GameResourceResolver", "Resolver");
            return null;
        }
    }

    private string? ResolveBifPath(string bifFilename)
    {
        if (string.IsNullOrEmpty(_config.GameDataPath))
            return null;

        // BIF filename may include "data\" prefix
        var normalized = bifFilename.Replace("\\", "/").Replace("/", Path.DirectorySeparatorChar.ToString());

        // Try relative to game data parent (for "data\file.bif" paths)
        var parentPath = Path.GetDirectoryName(_config.GameDataPath);
        if (parentPath != null)
        {
            var fullPath = Path.Combine(parentPath, normalized);
            if (File.Exists(fullPath))
                return fullPath;
        }

        // Try relative to game data path
        var directPath = Path.Combine(_config.GameDataPath, Path.GetFileName(normalized));
        if (File.Exists(directPath))
            return directPath;

        return null;
    }

    #endregion

    #region TLK Resolution

    private void EnsureBaseTlkLoaded()
    {
        if (_baseTlk != null)
            return;

        var tlkPath = _config.TlkPath;
        if (string.IsNullOrEmpty(tlkPath) && !string.IsNullOrEmpty(_config.GameDataPath))
            tlkPath = Path.Combine(_config.GameDataPath, "dialog.tlk");

        if (string.IsNullOrEmpty(tlkPath) || !File.Exists(tlkPath))
            return;

        try
        {
            _baseTlk = TlkReader.Read(tlkPath);
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.ERROR, $"Failed to load TLK '{tlkPath}': {ex.GetType().Name}: {ex.Message}", "GameResourceResolver", "Resolver");
        }
    }

    private void EnsureCustomTlkLoaded()
    {
        if (_customTlk != null || string.IsNullOrEmpty(_config.CustomTlkPath))
            return;

        if (!File.Exists(_config.CustomTlkPath))
            return;

        try
        {
            _customTlk = TlkReader.Read(_config.CustomTlkPath);
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.ERROR, $"Failed to load custom TLK '{_config.CustomTlkPath}': {ex.GetType().Name}: {ex.Message}", "GameResourceResolver", "Resolver");
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed)
            return;

        _bifCache.Clear();
        _hakCache.Clear();
        _keyFile = null;
        _cachedKeyIndex = null;
        _baseTlk = null;
        _customTlk = null;
        _disposed = true;
    }

    #endregion
}
