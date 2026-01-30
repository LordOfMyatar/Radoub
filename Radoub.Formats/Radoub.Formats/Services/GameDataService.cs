using Radoub.Formats.Common;
using Radoub.Formats.Itp;
using Radoub.Formats.Logging;
using Radoub.Formats.Resolver;
using Radoub.Formats.Settings;
using Radoub.Formats.Ssf;
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

        if (_resolver == null)
        {
            UnifiedLogger.Log(LogLevel.DEBUG, $"FindResource({resRef}, {resourceType}): resolver is null", "GameDataService", "GameData");
            return null;
        }

        var result = _resolver.FindResource(resRef, resourceType);
        UnifiedLogger.Log(LogLevel.DEBUG, $"FindResource({resRef}, {resourceType}): {(result != null ? $"{result.Length} bytes" : "not found")}", "GameDataService", "GameData");
        return result;
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

        UnifiedLogger.Log(LogLevel.INFO, $"InitializeFromSettings: HasGamePaths={settings.HasGamePaths}", "GameDataService", "GameData");

        if (!settings.HasGamePaths)
        {
            UnifiedLogger.Log(LogLevel.WARN, "No game paths configured - BIF lookup disabled", "GameDataService", "GameData");
            return;
        }

        var config = BuildConfig(settings);
        _resolver = new GameResourceResolver(config);

        // Load TLK
        var tlkPath = settings.GetTlkPath(settings.EffectiveLanguage, settings.PreferredGender);
        LoadBaseTlk(tlkPath);

        // Load custom TLK if configured
        LoadCustomTlk(config.CustomTlkPath);

        UnifiedLogger.Log(LogLevel.INFO, $"GameDataService initialized: IsConfigured={IsConfigured}", "GameDataService", "GameData");
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

        // HAK paths - scan all configured search paths
        foreach (var hakSearchPath in settings.GetAllHakSearchPaths())
        {
            var hakFiles = Directory.GetFiles(hakSearchPath, "*.hak", SearchOption.TopDirectoryOnly);
            config.HakPaths.AddRange(hakFiles);
            UnifiedLogger.Log(LogLevel.INFO, $"Found {hakFiles.Length} HAK files in {hakSearchPath}", "GameDataService", "GameData");
        }

        // TLK path
        var tlkPath = settings.GetTlkPath(settings.EffectiveLanguage, settings.PreferredGender);
        if (!string.IsNullOrEmpty(tlkPath) && File.Exists(tlkPath))
        {
            config.TlkPath = tlkPath;
        }

        // Custom TLK path (module-specific)
        if (!string.IsNullOrEmpty(settings.CustomTlkPath) && File.Exists(settings.CustomTlkPath))
        {
            config.CustomTlkPath = settings.CustomTlkPath;
            UnifiedLogger.Log(LogLevel.INFO, $"Custom TLK configured: {settings.CustomTlkPath}", "GameDataService", "GameData");
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
            UnifiedLogger.Log(LogLevel.DEBUG, $"2DA not found: {name}", "GameDataService", "GameData");
            return null;
        }

        try
        {
            return TwoDAReader.Read(data);
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.WARN, $"Failed to parse 2DA '{name}': {ex.GetType().Name}: {ex.Message}", "GameDataService", "GameData");
            return null;
        }
    }

    private void LoadBaseTlk(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            UnifiedLogger.Log(LogLevel.DEBUG, $"Base TLK not found: {path}", "GameDataService", "GameData");
            return;
        }

        try
        {
            _baseTlk = TlkReader.Read(path);
            UnifiedLogger.Log(LogLevel.INFO, $"Loaded base TLK: {path} ({_baseTlk.Count} strings)", "GameDataService", "GameData");
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.ERROR, $"Failed to load base TLK '{path}': {ex.GetType().Name}: {ex.Message}", "GameDataService", "GameData");
        }
    }

    private void LoadCustomTlk(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return;

        try
        {
            _customTlk = TlkReader.Read(path);
            UnifiedLogger.Log(LogLevel.INFO, $"Loaded custom TLK: {path} ({_customTlk.Count} strings)", "GameDataService", "GameData");
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.ERROR, $"Failed to load custom TLK '{path}': {ex.GetType().Name}: {ex.Message}", "GameDataService", "GameData");
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

    #region Soundset Access

    private readonly Dictionary<string, SsfFile?> _ssfCache = new(StringComparer.OrdinalIgnoreCase);

    public SsfFile? GetSoundset(int soundsetId)
    {
        var resRef = GetSoundsetResRef(soundsetId);
        if (string.IsNullOrEmpty(resRef))
            return null;

        return GetSoundsetByResRef(resRef);
    }

    public SsfFile? GetSoundsetByResRef(string resRef)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrEmpty(resRef))
            return null;

        lock (_lock)
        {
            if (_ssfCache.TryGetValue(resRef, out var cached))
                return cached;

            // Load SSF from game resources (ResourceTypes.Ssf = 2060)
            var data = FindResource(resRef, ResourceTypes.Ssf);
            SsfFile? ssf = null;

            if (data != null)
            {
                ssf = SsfReader.Read(data);
                if (ssf != null)
                {
                    UnifiedLogger.LogParser(LogLevel.DEBUG, $"Loaded soundset: {resRef}");
                }
            }

            _ssfCache[resRef] = ssf;
            return ssf;
        }
    }

    public string? GetSoundsetResRef(int soundsetId)
    {
        if (soundsetId < 0)
            return null;

        return Get2DAValue("soundset", soundsetId, "RESREF");
    }

    #endregion

    #region Palette Access

    private readonly Dictionary<ushort, List<PaletteCategory>> _paletteCache = new();

    public IEnumerable<PaletteCategory> GetPaletteCategories(ushort resourceType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (_paletteCache.TryGetValue(resourceType, out var cached))
                return cached;

            var categories = LoadPaletteCategories(resourceType);
            _paletteCache[resourceType] = categories;
            return categories;
        }
    }

    public string? GetPaletteCategoryName(ushort resourceType, byte categoryId)
    {
        var categories = GetPaletteCategories(resourceType);
        return categories.FirstOrDefault(c => c.Id == categoryId)?.Name;
    }

    private List<PaletteCategory> LoadPaletteCategories(ushort resourceType)
    {
        var paletteName = GetPaletteNameForResource(resourceType);
        if (string.IsNullOrEmpty(paletteName))
        {
            UnifiedLogger.Log(LogLevel.DEBUG, $"No palette name for resource type {resourceType}", "GameDataService", "GameData");
            return new List<PaletteCategory>();
        }

        // Load the skeleton palette ITP
        var data = FindResource(paletteName, ResourceTypes.Itp);
        if (data == null)
        {
            UnifiedLogger.Log(LogLevel.DEBUG, $"Palette not found: {paletteName}.itp", "GameDataService", "GameData");
            return new List<PaletteCategory>();
        }

        var itp = ItpReader.Read(data);
        if (itp == null)
        {
            UnifiedLogger.Log(LogLevel.WARN, $"Failed to parse palette: {paletteName}.itp", "GameDataService", "GameData");
            return new List<PaletteCategory>();
        }

        // Extract categories with resolved names
        var categories = new List<PaletteCategory>();
        ExtractCategories(itp.MainNodes, categories, null);

        UnifiedLogger.Log(LogLevel.DEBUG, $"Loaded {categories.Count} palette categories from {paletteName}.itp", "GameDataService", "GameData");
        return categories;
    }

    // Names that indicate invalid/placeholder palette entries
    private static readonly HashSet<string> InvalidCategoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "DELETED", "DELETE_ME", "BadStrRef", "BadStreff", "Padding", "PAdding",
        "XP2SpecReq", "Reserved", "Unused", "INVALID", "NULL", "NONE", "N/A",
        "****", "---", "???"
    };

    private void ExtractCategories(List<PaletteNode> nodes, List<PaletteCategory> categories, string? parentPath)
    {
        foreach (var node in nodes)
        {
            // Skip nodes that should never display
            if (node.DisplayType == PaletteDisplayType.DisplayNever)
                continue;

            // Get node name
            var name = GetNodeName(node);

            // Skip invalid/placeholder names
            if (string.IsNullOrEmpty(name) || IsInvalidCategoryName(name))
                continue;

            if (node is PaletteCategoryNode category)
            {
                // Skip duplicate category IDs (keep first occurrence)
                if (categories.Any(c => c.Id == category.Id))
                    continue;

                categories.Add(new PaletteCategory
                {
                    Id = category.Id,
                    Name = name,
                    ParentPath = parentPath
                });
            }
            else if (node is PaletteBranchNode branch)
            {
                // Build path for children
                var currentPath = string.IsNullOrEmpty(parentPath)
                    ? name
                    : (string.IsNullOrEmpty(name) ? parentPath : $"{parentPath}/{name}");

                ExtractCategories(branch.Children, categories, currentPath);
            }
        }
    }

    private string? GetNodeName(PaletteNode node)
    {
        // Try direct name first
        if (!string.IsNullOrEmpty(node.Name))
            return node.Name;

        // Try TLK resolution
        if (node.StrRef.HasValue && node.StrRef.Value != 0xFFFFFFFF)
        {
            var tlkString = GetString(node.StrRef.Value);
            if (!string.IsNullOrEmpty(tlkString))
                return tlkString;
        }

        // Don't fall back to DELETE_ME - it often contains garbage like "BadStrRef", "DELETED", etc.
        // If we can't get a proper name from Name field or TLK, skip this node
        return null;
    }

    private static bool IsInvalidCategoryName(string name)
    {
        // Check against known invalid names
        if (InvalidCategoryNames.Contains(name))
            return true;

        // Check for names that are just numbers or whitespace
        if (string.IsNullOrWhiteSpace(name))
            return true;

        // Check for names that start with common invalid prefixes
        if (name.StartsWith("Bad", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Delete", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Unused", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static string? GetPaletteNameForResource(ushort resourceType)
    {
        // Map resource types to their skeleton palette names
        return resourceType switch
        {
            ResourceTypes.Utc => "creaturepal",   // Creature
            ResourceTypes.Uti => "itempal",       // Item
            ResourceTypes.Utp => "placeablepal",  // Placeable
            ResourceTypes.Utd => "doorpal",       // Door
            ResourceTypes.Utm => "storepal",      // Store/Merchant
            ResourceTypes.Utt => "triggerpal",    // Trigger
            ResourceTypes.Ute => "encounterpal",  // Encounter
            ResourceTypes.Uts => "soundpal",      // Sound
            ResourceTypes.Utw => "waypointpal",   // Waypoint
            _ => null
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
        _ssfCache.Clear();
        _paletteCache.Clear();
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    #endregion
}
