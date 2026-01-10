using System;
using System.IO;
using Radoub.Formats.Logging;

namespace Radoub.UI.Services;

/// <summary>
/// Provides SafeMode functionality for Radoub tools.
/// SafeMode resets visual settings (theme, fonts) to defaults to recover from
/// unusable UI states. Optionally clears caches.
/// </summary>
public class SafeModeService
{
    private readonly string _toolName;
    private readonly string _settingsDirectory;

    /// <summary>
    /// Whether SafeMode was activated this session
    /// </summary>
    public bool SafeModeActive { get; private set; }

    /// <summary>
    /// Default theme ID for the tool (e.g., "org.parley.theme.light")
    /// </summary>
    public string DefaultThemeId { get; }

    /// <summary>
    /// Default font size
    /// </summary>
    public const double DefaultFontSize = 14.0;

    /// <summary>
    /// Default font family (empty = system default)
    /// </summary>
    public const string DefaultFontFamily = "";

    /// <summary>
    /// Creates a SafeModeService for the specified tool.
    /// </summary>
    /// <param name="toolName">Tool name (e.g., "Parley", "Manifest", "Quartermaster")</param>
    public SafeModeService(string toolName)
    {
        _toolName = toolName;
        DefaultThemeId = $"org.{toolName.ToLowerInvariant()}.theme.light";

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _settingsDirectory = Path.Combine(userProfile, "Radoub", toolName);
    }

    /// <summary>
    /// Activates SafeMode - resets visual settings and optionally clears caches.
    /// Call this before SettingsService is initialized.
    /// </summary>
    /// <param name="clearParameterCache">Clear parameter cache (Parley only)</param>
    /// <param name="clearPluginData">Clear plugin settings (Parley only)</param>
    public void ActivateSafeMode(bool clearParameterCache = true, bool clearPluginData = true)
    {
        SafeModeActive = true;

        try
        {
            Console.Error.WriteLine($"[{_toolName}] SafeMode activated - resetting visual settings to defaults");

            // Clear parameter cache if requested (Parley-specific)
            if (clearParameterCache)
            {
                ClearParameterCache();
            }

            // Clear plugin data if requested (Parley-specific)
            if (clearPluginData)
            {
                ClearPluginData();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[{_toolName}] SafeMode warning: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears the parameter cache file.
    /// </summary>
    private void ClearParameterCache()
    {
        var cachePath = Path.Combine(_settingsDirectory, "Cache", "parameter_cache.json");
        if (File.Exists(cachePath))
        {
            try
            {
                File.Delete(cachePath);
                Console.Error.WriteLine($"  Cleared parameter cache");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  Warning: Could not clear parameter cache: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Clears plugin settings data (resets enabled/disabled state, crash history).
    /// </summary>
    private void ClearPluginData()
    {
        var pluginSettingsPath = Path.Combine(_settingsDirectory, "PluginSettings.json");
        if (File.Exists(pluginSettingsPath))
        {
            try
            {
                File.Delete(pluginSettingsPath);
                Console.Error.WriteLine($"  Cleared plugin data");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  Warning: Could not clear plugin data: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Clears the scrap data file (deleted nodes history).
    /// </summary>
    public void ClearScrapData()
    {
        var scrapPath = Path.Combine(_settingsDirectory, "Cache", "scrap.json");
        if (File.Exists(scrapPath))
        {
            try
            {
                File.Delete(scrapPath);
                Console.Error.WriteLine($"  Cleared scrap data");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  Warning: Could not clear scrap data: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Gets the Cache directory path for the tool.
    /// </summary>
    public string GetCacheDirectory() => Path.Combine(_settingsDirectory, "Cache");

    /// <summary>
    /// Gets the settings directory path for the tool.
    /// </summary>
    public string GetSettingsDirectory() => _settingsDirectory;
}
