using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Radoub.Formats.Common;
using Radoub.Formats.Resolver;
using Radoub.Formats.Tlk;

namespace DialogEditor.Services;

/// <summary>
/// Provides access to NWN game resources (TLK, scripts, etc.) using Radoub.Formats.
/// Wraps GameResourceResolver with Parley-specific configuration and caching.
/// </summary>
public class GameResourceService : IDisposable
{
    private static GameResourceService? _instance;
    private static readonly object _lock = new();

    private GameResourceResolver? _resolver;
    private GameResourceConfig? _currentConfig;
    private bool _disposed;

    public static GameResourceService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new GameResourceService();
                }
            }
            return _instance;
        }
    }

    private GameResourceService()
    {
        // Subscribe to settings changes to reinitialize resolver
        SettingsService.Instance.PropertyChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Reinitialize resolver when relevant paths change
        if (e.PropertyName is nameof(SettingsService.BaseGameInstallPath) or
            nameof(SettingsService.NeverwinterNightsPath) or
            nameof(SettingsService.CurrentModulePath))
        {
            InvalidateResolver();
        }
    }

    /// <summary>
    /// Invalidate the current resolver to force reinitialization.
    /// Call this when game paths change.
    /// </summary>
    public void InvalidateResolver()
    {
        lock (_lock)
        {
            _resolver?.Dispose();
            _resolver = null;
            _currentConfig = null;
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "GameResourceResolver invalidated");
        }
    }

    /// <summary>
    /// Get or create the GameResourceResolver with current settings.
    /// </summary>
    private GameResourceResolver? GetResolver()
    {
        if (_resolver != null)
            return _resolver;

        lock (_lock)
        {
            if (_resolver != null)
                return _resolver;

            var config = BuildConfig();
            if (config == null)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Cannot create GameResourceResolver: no game paths configured");
                return null;
            }

            try
            {
                _resolver = new GameResourceResolver(config);
                _currentConfig = config;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"GameResourceResolver initialized with game data: {UnifiedLogger.SanitizePath(config.GameDataPath ?? "")}");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to create GameResourceResolver: {ex.Message}");
                return null;
            }

            return _resolver;
        }
    }

    private GameResourceConfig? BuildConfig()
    {
        var settings = SettingsService.Instance;

        // Try BaseGameInstallPath first (explicit setting)
        var gameDataPath = settings.BaseGameInstallPath;

        // Fall back to NeverwinterNightsPath if no explicit base path
        if (string.IsNullOrEmpty(gameDataPath))
        {
            gameDataPath = settings.NeverwinterNightsPath;
        }

        // Need at least a game data path
        if (string.IsNullOrEmpty(gameDataPath))
            return null;

        // Look for the data folder
        var dataPath = Path.Combine(gameDataPath, "data");
        if (!Directory.Exists(dataPath))
        {
            // Maybe gameDataPath IS the data folder
            if (File.Exists(Path.Combine(gameDataPath, "nwn_base.key")))
            {
                dataPath = gameDataPath;
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Game data folder not found: {UnifiedLogger.SanitizePath(dataPath)}");
                return null;
            }
        }

        var config = new GameResourceConfig
        {
            GameDataPath = dataPath,
            CacheArchives = true
        };

        // Add override path if it exists
        var overridePath = Path.Combine(gameDataPath, "override");
        if (Directory.Exists(overridePath))
        {
            config.OverridePath = overridePath;
        }

        // Add module HAK files if module path is set
        var modulePath = settings.CurrentModulePath;
        if (!string.IsNullOrEmpty(modulePath) && Directory.Exists(modulePath))
        {
            var hakFiles = Directory.GetFiles(modulePath, "*.hak", SearchOption.TopDirectoryOnly);
            config.HakPaths = hakFiles.ToList();
        }

        return config;
    }

    /// <summary>
    /// Check if game resources are available.
    /// </summary>
    public bool IsAvailable => GetResolver() != null;

    /// <summary>
    /// Get a TLK string by StrRef.
    /// Returns null if TLK not available or StrRef not found.
    /// </summary>
    public string? GetTlkString(uint strRef)
    {
        // 0xFFFFFFFF means "no StrRef" (custom text)
        if (strRef == 0xFFFFFFFF)
            return null;

        var resolver = GetResolver();
        if (resolver == null)
            return null;

        try
        {
            return resolver.GetTlkString(strRef);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Failed to get TLK string for StrRef {strRef}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Get a TLK entry by StrRef (includes sound info).
    /// </summary>
    public TlkEntry? GetTlkEntry(uint strRef)
    {
        if (strRef == 0xFFFFFFFF)
            return null;

        var resolver = GetResolver();
        if (resolver == null)
            return null;

        try
        {
            return resolver.GetTlkEntry(strRef);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Failed to get TLK entry for StrRef {strRef}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// List all scripts available in the game (from BIF files).
    /// </summary>
    public IEnumerable<ResourceInfo> ListBuiltInScripts()
    {
        var resolver = GetResolver();
        if (resolver == null)
            return Enumerable.Empty<ResourceInfo>();

        try
        {
            // Nss = 2009 (script source), Ncs = 2010 (compiled script)
            // We want Ncs since built-in scripts are compiled
            return resolver.ListResources(ResourceTypes.Ncs);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Failed to list built-in scripts: {ex.Message}");
            return Enumerable.Empty<ResourceInfo>();
        }
    }

    /// <summary>
    /// Find a resource by ResRef and type.
    /// </summary>
    public byte[]? FindResource(string resRef, ushort resourceType)
    {
        var resolver = GetResolver();
        if (resolver == null)
            return null;

        try
        {
            return resolver.FindResource(resRef, resourceType);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Failed to find resource {resRef}: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        SettingsService.Instance.PropertyChanged -= OnSettingsChanged;
        _resolver?.Dispose();
        _resolver = null;
        _disposed = true;
    }
}
