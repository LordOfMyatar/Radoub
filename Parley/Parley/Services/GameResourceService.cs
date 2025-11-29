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
        // Reinitialize resolver when relevant paths or TLK settings change
        if (e.PropertyName is nameof(SettingsService.BaseGameInstallPath) or
            nameof(SettingsService.NeverwinterNightsPath) or
            nameof(SettingsService.CurrentModulePath) or
            nameof(SettingsService.TlkLanguage) or
            nameof(SettingsService.TlkUseFemale))
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

                // Verify TLK is accessible by testing a known StrRef
                var testString = _resolver.GetTlkString(0); // StrRef 0 = "Bad Strref"
                if (testString != null)
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"TLK loaded successfully. StrRef 0 = '{testString}'");
                }
                else
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, "TLK load verification failed - GetTlkString(0) returned null");
                }
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
        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"BuildConfig: BaseGameInstallPath = '{UnifiedLogger.SanitizePath(gameDataPath ?? "(null)")}'");

        // Fall back to NeverwinterNightsPath if no explicit base path
        if (string.IsNullOrEmpty(gameDataPath))
        {
            gameDataPath = settings.NeverwinterNightsPath;
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"BuildConfig: Falling back to NeverwinterNightsPath = '{UnifiedLogger.SanitizePath(gameDataPath ?? "(null)")}'");
        }

        // Need at least a game data path
        if (string.IsNullOrEmpty(gameDataPath))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "BuildConfig: No game path configured");
            return null;
        }

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

        // Set TLK path - NWN:EE stores dialog.tlk in lang/XX/data/ folders
        // Check user language and gender preference first, then auto-detect
        var tlkLanguage = settings.TlkLanguage;
        var tlkUseFemale = settings.TlkUseFemale;
        var tlkFilename = tlkUseFemale ? "dialogf.tlk" : "dialog.tlk";
        string? tlkPath = null;

        // Supported languages in priority order for auto-detection
        var languages = new[] { "en", "de", "fr", "es", "it", "pl" };

        if (!string.IsNullOrEmpty(tlkLanguage))
        {
            // User specified a language preference
            var preferredPath = Path.Combine(gameDataPath, "lang", tlkLanguage, "data", tlkFilename);
            if (File.Exists(preferredPath))
            {
                tlkPath = preferredPath;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Using preferred TLK: {tlkLanguage} ({(tlkUseFemale ? "female" : "male/default")})");
            }
            else if (tlkUseFemale)
            {
                // Female TLK not found, fall back to male/default
                var fallbackPath = Path.Combine(gameDataPath, "lang", tlkLanguage, "data", "dialog.tlk");
                if (File.Exists(fallbackPath))
                {
                    tlkPath = fallbackPath;
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Female TLK not found for '{tlkLanguage}', using male/default");
                }
            }

            if (tlkPath == null)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Preferred TLK language '{tlkLanguage}' not found at: {UnifiedLogger.SanitizePath(Path.Combine(gameDataPath, "lang", tlkLanguage, "data"))}");
            }
        }

        // Auto-detect if no preference or preference not found
        if (tlkPath == null)
        {
            // Try classic NWN first
            var classicPath = Path.Combine(dataPath, tlkFilename);
            if (File.Exists(classicPath))
            {
                tlkPath = classicPath;
            }
            else if (tlkUseFemale)
            {
                // Try male/default for classic
                classicPath = Path.Combine(dataPath, "dialog.tlk");
                if (File.Exists(classicPath))
                {
                    tlkPath = classicPath;
                    UnifiedLogger.LogApplication(LogLevel.WARN, "Female TLK not found for classic NWN, using male/default");
                }
            }

            if (tlkPath == null)
            {
                // Try NWN:EE language folders in priority order
                foreach (var lang in languages)
                {
                    var langPath = Path.Combine(gameDataPath, "lang", lang, "data", tlkFilename);
                    if (File.Exists(langPath))
                    {
                        tlkPath = langPath;
                        UnifiedLogger.LogApplication(LogLevel.INFO, $"Auto-detected TLK: {lang} ({(tlkUseFemale ? "female" : "male/default")})");
                        break;
                    }
                    else if (tlkUseFemale)
                    {
                        // Try male/default fallback
                        langPath = Path.Combine(gameDataPath, "lang", lang, "data", "dialog.tlk");
                        if (File.Exists(langPath))
                        {
                            tlkPath = langPath;
                            UnifiedLogger.LogApplication(LogLevel.INFO, $"Auto-detected TLK: {lang} (female not available, using male/default)");
                            break;
                        }
                    }
                }
            }
        }

        if (tlkPath != null)
        {
            config.TlkPath = tlkPath;
            UnifiedLogger.LogApplication(LogLevel.INFO, $"TLK path configured: {UnifiedLogger.SanitizePath(tlkPath)}");
        }
        else
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "dialog.tlk not found in any known location");
        }

        // Add override path if it exists
        // NWN:EE uses "ovr", classic uses "override"
        var ovrPath = Path.Combine(gameDataPath, "ovr");
        var overridePath = Path.Combine(gameDataPath, "override");
        if (Directory.Exists(ovrPath))
        {
            config.OverridePath = ovrPath;
        }
        else if (Directory.Exists(overridePath))
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
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"GetTlkString({strRef}): Resolver not available");
            return null;
        }

        try
        {
            var result = resolver.GetTlkString(strRef);
            if (result == null)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"GetTlkString({strRef}): TLK returned null");
            }
            return result;
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
