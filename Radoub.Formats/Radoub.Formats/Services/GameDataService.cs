using System.Diagnostics;
using Radoub.Formats.Common;
using Radoub.Formats.Resolver;
using Radoub.Formats.Settings;
using Radoub.Formats.Tlk;
using Radoub.Formats.TwoDA;

namespace Radoub.Formats.Services;

/// <summary>
/// Implementation of IGameDataService providing cached access to game data.
/// Wraps GameResourceResolver with 2DA caching and TLK resolution.
/// </summary>
public class GameDataService : IGameDataService
{
    private GameResourceResolver? _resolver;
    private TlkFile? _baseTlk;
    private TlkFile? _customTlk;
    private string? _customTlkPath;
    private readonly Dictionary<string, TwoDAFile?> _twoDACache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Create a GameDataService using RadoubSettings for configuration.
    /// </summary>
    public GameDataService()
    {
        InitializeFromSettings();
    }

    /// <summary>
    /// Create a GameDataService with explicit configuration.
    /// </summary>
    public GameDataService(GameResourceConfig config)
    {
        _resolver = new GameResourceResolver(config);
        LoadBaseTlk(config.TlkPath);
        LoadCustomTlk(config.CustomTlkPath);
    }

    #region IGameDataService Implementation

    public bool IsConfigured => _resolver != null;

    public TwoDAFile? Get2DA(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrEmpty(name))
            return null;

        lock (_lock)
        {
            // Check cache first (including negative cache)
            if (_twoDACache.TryGetValue(name, out var cached))
                return cached;

            // Load from resolver
            var twoDA = LoadTwoDA(name);
            _twoDACache[name] = twoDA; // Cache even if null (negative cache)
            return twoDA;
        }
    }

    public string? Get2DAValue(string twoDAName, int rowIndex, string columnName)
    {
        var twoDA = Get2DA(twoDAName);
        return twoDA?.GetValue(rowIndex, columnName);
    }

    public bool Has2DA(string name)
    {
        if (string.IsNullOrEmpty(name) || _resolver == null)
            return false;

        // Check cache first
        lock (_lock)
        {
            if (_twoDACache.TryGetValue(name, out var cached))
                return cached != null;
        }

        // Check if resource exists without loading
        var data = _resolver.FindResource(name, ResourceTypes.TwoDA);
        return data != null;
    }

    public void ClearCache()
    {
        lock (_lock)
        {
            _twoDACache.Clear();
        }
    }

    public string? GetString(uint strRef)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        const uint CustomTlkOffset = 16777216; // 0x01000000

        if (strRef >= CustomTlkOffset)
        {
            return _customTlk?.GetString(strRef - CustomTlkOffset);
        }

        return _baseTlk?.GetString(strRef);
    }

    public string? GetString(string? strRefStr)
    {
        if (string.IsNullOrEmpty(strRefStr) || strRefStr == "****")
            return null;

        if (!uint.TryParse(strRefStr, out uint strRef))
            return null;

        return GetString(strRef);
    }

    public bool HasCustomTlk => _customTlk != null;

    public void SetCustomTlk(string? path)
    {
        if (_customTlkPath == path)
            return;

        _customTlkPath = path;
        _customTlk = null;
        LoadCustomTlk(path);
    }

    public byte[]? FindResource(string resRef, ushort resourceType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _resolver?.FindResource(resRef, resourceType);
    }

    public IEnumerable<GameResourceInfo> ListResources(ushort resourceType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_resolver == null)
            return Enumerable.Empty<GameResourceInfo>();

        return _resolver.ListResources(resourceType)
            .Select(r => new GameResourceInfo
            {
                ResRef = r.ResRef,
                ResourceType = r.ResourceType,
                Source = MapSource(r.Source),
                SourcePath = r.SourcePath
            });
    }

    public void ReloadConfiguration()
    {
        lock (_lock)
        {
            // Dispose existing resolver
            _resolver?.Dispose();
            _resolver = null;
            _baseTlk = null;
            _customTlk = null;
            _twoDACache.Clear();

            // Reinitialize from settings
            InitializeFromSettings();
        }
    }

    #endregion

    #region Private Methods

    private void InitializeFromSettings()
    {
        var settings = RadoubSettings.Instance;

        if (!settings.HasGamePaths)
        {
            Debug.WriteLine("[GameDataService] No game paths configured");
            return;
        }

        var config = BuildConfig(settings);
        _resolver = new GameResourceResolver(config);

        // Load TLK
        var tlkPath = settings.GetTlkPath(settings.EffectiveLanguage, settings.PreferredGender);
        LoadBaseTlk(tlkPath);
    }

    private static GameResourceConfig BuildConfig(RadoubSettings settings)
    {
        var config = new GameResourceConfig
        {
            CacheArchives = true
        };

        // Base game path
        if (!string.IsNullOrEmpty(settings.BaseGameInstallPath))
        {
            var dataPath = Path.Combine(settings.BaseGameInstallPath, "data");
            if (Directory.Exists(dataPath))
            {
                config.GameDataPath = dataPath;
                config.KeyFilePath = Path.Combine(dataPath, "nwn_base.key");
            }
        }

        // Override path
        if (!string.IsNullOrEmpty(settings.NeverwinterNightsPath))
        {
            var overridePath = Path.Combine(settings.NeverwinterNightsPath, "override");
            if (Directory.Exists(overridePath))
            {
                config.OverridePath = overridePath;
            }
        }

        // TLK path
        var tlkPath = settings.GetTlkPath(settings.EffectiveLanguage, settings.PreferredGender);
        if (!string.IsNullOrEmpty(tlkPath) && File.Exists(tlkPath))
        {
            config.TlkPath = tlkPath;
        }

        return config;
    }

    private TwoDAFile? LoadTwoDA(string name)
    {
        if (_resolver == null)
            return null;

        var data = _resolver.FindResource(name, ResourceTypes.TwoDA);
        if (data == null)
        {
            Debug.WriteLine($"[GameDataService] 2DA not found: {name}");
            return null;
        }

        try
        {
            return TwoDAReader.Read(data);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameDataService] Failed to parse 2DA '{name}': {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private void LoadBaseTlk(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Debug.WriteLine($"[GameDataService] Base TLK not found: {path}");
            return;
        }

        try
        {
            _baseTlk = TlkReader.Read(path);
            Debug.WriteLine($"[GameDataService] Loaded base TLK: {path} ({_baseTlk.Count} strings)");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameDataService] Failed to load base TLK '{path}': {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void LoadCustomTlk(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return;

        try
        {
            _customTlk = TlkReader.Read(path);
            Debug.WriteLine($"[GameDataService] Loaded custom TLK: {path} ({_customTlk.Count} strings)");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameDataService] Failed to load custom TLK '{path}': {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static GameResourceSource MapSource(ResourceSource source)
    {
        return source switch
        {
            ResourceSource.Override => GameResourceSource.Override,
            ResourceSource.Hak => GameResourceSource.Hak,
            ResourceSource.Bif => GameResourceSource.Bif,
            _ => GameResourceSource.Bif
        };
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed)
            return;

        _resolver?.Dispose();
        _resolver = null;
        _baseTlk = null;
        _customTlk = null;
        _twoDACache.Clear();
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    #endregion
}
