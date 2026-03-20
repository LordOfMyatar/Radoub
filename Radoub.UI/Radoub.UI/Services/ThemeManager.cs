using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.UI.Models;

namespace Radoub.UI.Services;

/// <summary>
/// Manages theme loading, application, and switching for Radoub tools.
/// Supports JSON-based theme plugins with tool-specific user directories.
/// </summary>
public partial class ThemeManager
{
    private static ThemeManager? _instance;
    private static readonly object _lock = new();

    private readonly Dictionary<string, ThemeManifest> _themes = new();
    private ThemeManifest? _currentTheme;
    private readonly string _toolName;
    private readonly bool _useSharedTheme;

    /// <summary>
    /// Event raised when a theme is successfully applied.
    /// UI components can subscribe to refresh their visuals.
    /// </summary>
    public event EventHandler? ThemeApplied;

    /// <summary>
    /// Theme directories (official and user)
    /// </summary>
    private readonly List<string> _themeDirectories = new();

    /// <summary>
    /// Available themes discovered from theme directories
    /// </summary>
    public IReadOnlyList<ThemeManifest> AvailableThemes { get { lock (_lock) { return _themes.Values.ToList(); } } }

    /// <summary>
    /// Currently active theme
    /// </summary>
    public ThemeManifest? CurrentTheme => _currentTheme;

    /// <summary>
    /// Gets the singleton instance. Must call Initialize() first.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if Initialize() has not been called.</exception>
    public static ThemeManager Instance
    {
        get
        {
            if (_instance == null)
            {
                throw new InvalidOperationException(
                    "ThemeManager not initialized. Call ThemeManager.Initialize(toolName) first.");
            }
            return _instance;
        }
    }

    /// <summary>
    /// Initializes the ThemeManager singleton for the specified tool.
    /// Call once at application startup before accessing Instance.
    /// </summary>
    /// <param name="toolName">Tool name for user theme directory (e.g., "Parley", "Manifest", "Quartermaster")</param>
    /// <param name="useSharedTheme">If true, prefer shared Radoub-level theme over tool-specific. Default: true</param>
    /// <returns>The initialized ThemeManager instance</returns>
    public static ThemeManager Initialize(string toolName, bool useSharedTheme = true)
    {
        lock (_lock)
        {
            if (_instance != null)
            {
                // Already initialized - return existing instance
                // (supports safe re-initialization with same tool name)
                return _instance;
            }
            _instance = new ThemeManager(toolName, useSharedTheme);
            return _instance;
        }
    }

    /// <summary>
    /// Creates a new ThemeManager for the specified tool.
    /// Use Initialize() for singleton access or construct directly for testing.
    /// </summary>
    /// <param name="toolName">Tool name for user theme directory (e.g., "Parley", "Manifest", "Quartermaster")</param>
    /// <param name="useSharedTheme">If true, prefer shared Radoub-level theme over tool-specific. Default: true</param>
    public ThemeManager(string toolName, bool useSharedTheme = true)
    {
        _toolName = toolName;
        _useSharedTheme = useSharedTheme;
        InitializeThemeDirectories();
    }

    /// <summary>
    /// Initialize theme search directories.
    /// Order of precedence (last wins for same theme ID):
    /// 1. Official themes (shipped with app)
    /// 2. Radoub-level shared themes (~/Radoub/Themes/)
    /// 3. Tool-specific user themes (~/Radoub/{ToolName}/Themes/)
    /// </summary>
    private void InitializeThemeDirectories()
    {
        // Official themes (shipped with app)
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var officialThemes = Path.Combine(appDir, "Themes");
        if (!Directory.Exists(officialThemes))
        {
            Directory.CreateDirectory(officialThemes);
        }
        _themeDirectories.Add(officialThemes);

        // Radoub-level shared themes (~/Radoub/Themes/)
        // Available to all tools
        var sharedThemes = RadoubSettings.Instance.GetSharedThemesPath();
        if (!_themeDirectories.Contains(sharedThemes))
        {
            _themeDirectories.Add(sharedThemes);
        }

        // User themes (user home folder - consistent with SettingsService)
        // Location: ~/Radoub/{ToolName}/Themes
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var userThemes = Path.Combine(userProfile, "Radoub", _toolName, "Themes");
        if (!Directory.Exists(userThemes))
        {
            Directory.CreateDirectory(userThemes);
        }
        _themeDirectories.Add(userThemes);

        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"[{_toolName}] Theme directories initialized: {_themeDirectories.Count} locations (shared themes: {_useSharedTheme})");
    }

    /// <summary>
    /// Discover all available themes from theme directories.
    /// Thread-safe: locks _themes during mutation.
    /// </summary>
    public void DiscoverThemes()
    {
        // Load manifests from disk outside the lock (I/O can be slow)
        var discovered = new Dictionary<string, ThemeManifest>();

        foreach (var directory in _themeDirectories)
        {
            if (!Directory.Exists(directory))
                continue;

            foreach (var themeFile in Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    var manifest = LoadThemeManifest(themeFile);
                    if (manifest != null)
                    {
                        discovered[manifest.Plugin.Id] = manifest;
                        UnifiedLogger.LogApplication(LogLevel.DEBUG,
                            $"[{_toolName}] Discovered theme: {manifest.Plugin.Name} ({manifest.Plugin.Id})");
                    }
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"[{_toolName}] Failed to load theme: {ex.Message}");
                }
            }
        }

        // Swap atomically under lock
        lock (_lock)
        {
            _themes.Clear();
            foreach (var kvp in discovered)
            {
                _themes[kvp.Key] = kvp.Value;
            }
        }

        UnifiedLogger.LogApplication(LogLevel.INFO, $"[{_toolName}] Discovered {_themes.Count} themes");
    }

    /// <summary>
    /// Load theme manifest from JSON file
    /// </summary>
    private ThemeManifest? LoadThemeManifest(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        var manifest = JsonSerializer.Deserialize<ThemeManifest>(json, options);

        if (manifest != null)
        {
            manifest.SourcePath = filePath;
        }

        return manifest;
    }

    /// <summary>
    /// Apply a theme by ID
    /// </summary>
    public bool ApplyTheme(string themeId)
    {
        ThemeManifest? theme;
        lock (_lock)
        {
            if (!_themes.TryGetValue(themeId, out theme))
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"[{_toolName}] Theme not found: {themeId}");
                return false;
            }
        }

        return ApplyTheme(theme);
    }

    /// <summary>
    /// Apply a theme manifest
    /// </summary>
    public bool ApplyTheme(ThemeManifest theme)
    {
        try
        {
            var app = Application.Current;
            if (app == null)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"[{_toolName}] Application instance not available");
                return false;
            }

            var targetVariant = theme.BaseTheme.ToLower() switch
            {
                "dark" => ThemeVariant.Dark,
                "light" => ThemeVariant.Light,
                _ => ThemeVariant.Light
            };

            // Step 1: Set opposite variant to force Fluent to fully re-derive
            // its internal resource dictionaries. This ensures controls like
            // Button, CheckBox, TabItem pick up the new colors.
            var oppositeVariant = targetVariant == ThemeVariant.Light
                ? ThemeVariant.Dark
                : ThemeVariant.Light;
            app.RequestedThemeVariant = oppositeVariant;

            // Step 2: Yield to the UI thread so Avalonia processes the variant
            // change (style detach/reattach, resource re-derivation). Then set
            // the real variant and apply our color overrides.
            var capturedTheme = theme;
            Dispatcher.UIThread.Post(() =>
            {
                app.RequestedThemeVariant = targetVariant;
                ApplyThemeResources(app, capturedTheme);
            }, DispatcherPriority.Send);

            return true;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"[{_toolName}] Failed to apply theme {theme.Plugin.Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get theme by ID
    /// </summary>
    public ThemeManifest? GetTheme(string themeId)
    {
        lock (_lock)
        {
            return _themes.TryGetValue(themeId, out var theme) ? theme : null;
        }
    }

    /// <summary>
    /// Reload themes from disk
    /// </summary>
    public void RefreshThemes()
    {
        DiscoverThemes();
    }

    /// <summary>
    /// Get the effective theme ID to apply.
    /// If useSharedTheme is enabled and a shared theme is configured in RadoubSettings,
    /// returns the shared theme ID. Otherwise returns the provided tool-specific theme ID.
    /// </summary>
    /// <param name="toolThemeId">The tool's configured theme ID</param>
    /// <param name="useSharedTheme">If specified, overrides the instance-level _useSharedTheme setting.
    /// Pass the tool's SettingsService.UseSharedTheme to respect per-tool override (#1533).</param>
    /// <returns>The effective theme ID to apply</returns>
    public string GetEffectiveThemeId(string toolThemeId, bool? useSharedTheme = null)
    {
        var effectiveUseShared = useSharedTheme ?? _useSharedTheme;
        if (effectiveUseShared && RadoubSettings.Instance.HasSharedTheme)
        {
            var sharedThemeId = RadoubSettings.Instance.SharedThemeId;
            bool found;
            lock (_lock)
            {
                found = _themes.ContainsKey(sharedThemeId);
            }
            if (found)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"[{_toolName}] Using shared theme: {sharedThemeId}");
                return sharedThemeId;
            }
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"[{_toolName}] Shared theme '{sharedThemeId}' not found, falling back to tool theme");
        }

        return toolThemeId;
    }

    /// <summary>
    /// Apply the effective theme (shared or tool-specific).
    /// Checks shared settings first if useSharedTheme is enabled.
    /// </summary>
    /// <param name="toolThemeId">The tool's configured theme ID as fallback</param>
    /// <param name="useSharedTheme">If specified, overrides the instance-level setting (#1533).</param>
    /// <returns>True if a theme was applied successfully</returns>
    public bool ApplyEffectiveTheme(string toolThemeId, bool? useSharedTheme = null)
    {
        var effectiveThemeId = GetEffectiveThemeId(toolThemeId, useSharedTheme);
        return ApplyTheme(effectiveThemeId);
    }

    /// <summary>
    /// Check if shared theme is being used.
    /// </summary>
    public bool IsUsingSharedTheme => _useSharedTheme && RadoubSettings.Instance.HasSharedTheme;
}
