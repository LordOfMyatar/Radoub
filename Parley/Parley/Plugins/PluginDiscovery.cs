using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using DialogEditor.Utils;

namespace DialogEditor.Plugins
{
    /// <summary>
    /// Discovers and catalogs available plugins
    /// </summary>
    public class PluginDiscovery
    {
        private readonly string _officialPluginsPath;
        private readonly string _communityPluginsPath;
        private readonly List<DiscoveredPlugin> _discoveredPlugins = new();

        public IReadOnlyList<DiscoveredPlugin> DiscoveredPlugins => _discoveredPlugins;

        public PluginDiscovery()
        {
            // Official plugins shipped with Parley
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            _officialPluginsPath = Path.Combine(appDir, "Plugins", "Official");

            // Community plugins in user data directory
            // New location: ~/Radoub/Parley (matches toolset structure)
            var userDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Radoub", "Parley"
            );
            _communityPluginsPath = Path.Combine(userDataDir, "Plugins", "Community");

            // Only log the community path - official path is app-relative and not useful to users
            UnifiedLogger.LogPlugin(LogLevel.INFO, $"Plugin paths configured:");
            UnifiedLogger.LogPlugin(LogLevel.INFO, $"  Community: {UnifiedLogger.SanitizePath(_communityPluginsPath)}");
        }

        /// <summary>
        /// Scan all plugin directories and discover available plugins
        /// </summary>
        public void ScanForPlugins()
        {
            _discoveredPlugins.Clear();

            UnifiedLogger.LogPlugin(LogLevel.INFO, "Scanning for plugins...");

            // Scan official plugins
            ScanDirectory(_officialPluginsPath, isOfficial: true);

            // Scan community plugins
            ScanDirectory(_communityPluginsPath, isOfficial: false);

            UnifiedLogger.LogPlugin(LogLevel.INFO, $"Discovery complete: {_discoveredPlugins.Count} plugins found");
        }

        private void ScanDirectory(string directory, bool isOfficial)
        {
            if (!Directory.Exists(directory))
            {
                // Directory doesn't exist - skip silently (expected for Official plugins in dev builds)
                return;
            }

            UnifiedLogger.LogPlugin(LogLevel.INFO, $"Scanning: {UnifiedLogger.SanitizePath(directory)}");

            try
            {
                // Search for plugin.json files recursively
                var manifestFiles = Directory.GetFiles(directory, "plugin.json", SearchOption.AllDirectories);

                UnifiedLogger.LogPlugin(LogLevel.INFO, $"Found {manifestFiles.Length} manifest files in {UnifiedLogger.SanitizePath(directory)}");

                foreach (var manifestPath in manifestFiles)
                {
                    ProcessManifest(manifestPath, isOfficial);
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Error scanning directory {UnifiedLogger.SanitizePath(directory)}: {ex.Message}");
            }
        }

        private void ProcessManifest(string manifestPath, bool isOfficial)
        {
            try
            {
                var manifest = PluginManifest.LoadFromFile(manifestPath);
                if (manifest == null)
                {
                    return; // Error already logged by LoadFromFile
                }

                // Override trust level for official plugins
                if (isOfficial && manifest.TrustLevel != "official")
                {
                    manifest.TrustLevel = "official";
                }

                // Get plugin directory
                var pluginDir = Path.GetDirectoryName(manifestPath);
                if (string.IsNullOrEmpty(pluginDir))
                {
                    UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Could not determine plugin directory for {UnifiedLogger.SanitizePath(manifestPath)}");
                    return;
                }

                // Resolve entry point path
                var entryPointPath = Path.Combine(pluginDir, manifest.EntryPoint);
                if (!File.Exists(entryPointPath))
                {
                    UnifiedLogger.LogPlugin(LogLevel.ERROR,
                        $"Entry point not found for plugin {manifest.Plugin.Id}: {UnifiedLogger.SanitizePath(entryPointPath)}");
                    return;
                }

                // Check version compatibility
                var parleyVersion = VersionHelper.Version;
                if (!manifest.IsCompatibleWith(parleyVersion))
                {
                    UnifiedLogger.LogPlugin(LogLevel.WARN,
                        $"Plugin {manifest.Plugin.Id} v{manifest.Plugin.Version} is not compatible with Parley {parleyVersion}");
                    return;
                }

                // Check for duplicate IDs
                if (_discoveredPlugins.Any(p => p.Manifest.Plugin.Id == manifest.Plugin.Id))
                {
                    UnifiedLogger.LogPlugin(LogLevel.WARN,
                        $"Duplicate plugin ID found: {manifest.Plugin.Id} at {UnifiedLogger.SanitizePath(manifestPath)}");
                    return;
                }

                var plugin = new DiscoveredPlugin
                {
                    Manifest = manifest,
                    ManifestPath = manifestPath,
                    PluginDirectory = pluginDir,
                    EntryPointPath = entryPointPath,
                    IsOfficial = isOfficial
                };

                _discoveredPlugins.Add(plugin);

                var trustIndicator = isOfficial ? "[OFFICIAL]" : $"[{manifest.TrustLevel.ToUpperInvariant()}]";
                UnifiedLogger.LogPlugin(LogLevel.INFO,
                    $"Discovered: {manifest.Plugin.Name} v{manifest.Plugin.Version} {trustIndicator}");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR,
                    $"Error processing manifest {UnifiedLogger.SanitizePath(manifestPath)}: {ex.Message}");
            }
        }

        /// <summary>
        /// Get plugin by ID or folder name (#235).
        /// Matches against both the manifest plugin ID and the folder name.
        /// </summary>
        public DiscoveredPlugin? GetPluginById(string pluginId)
        {
            // First try exact match on manifest ID
            var byId = _discoveredPlugins.FirstOrDefault(p =>
                p.Manifest.Plugin.Id.Equals(pluginId, StringComparison.OrdinalIgnoreCase));
            if (byId != null)
                return byId;

            // Also try matching by folder name (for panel reopen support)
            return _discoveredPlugins.FirstOrDefault(p =>
                Path.GetFileName(p.PluginDirectory).Equals(pluginId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get all plugins with a specific permission
        /// </summary>
        public List<DiscoveredPlugin> GetPluginsWithPermission(string permission)
        {
            return _discoveredPlugins
                .Where(p => p.Manifest.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Get all official plugins
        /// </summary>
        public List<DiscoveredPlugin> GetOfficialPlugins()
        {
            return _discoveredPlugins.Where(p => p.IsOfficial).ToList();
        }

        /// <summary>
        /// Get all community plugins
        /// </summary>
        public List<DiscoveredPlugin> GetCommunityPlugins()
        {
            return _discoveredPlugins.Where(p => !p.IsOfficial).ToList();
        }

        /// <summary>
        /// Filter plugins by trust level
        /// </summary>
        public List<DiscoveredPlugin> GetPluginsByTrustLevel(string trustLevel)
        {
            return _discoveredPlugins
                .Where(p => p.Manifest.TrustLevel.Equals(trustLevel, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    /// <summary>
    /// Represents a discovered plugin
    /// </summary>
    public class DiscoveredPlugin
    {
        public PluginManifest Manifest { get; set; } = new();
        public string ManifestPath { get; set; } = string.Empty;
        public string PluginDirectory { get; set; } = string.Empty;
        public string EntryPointPath { get; set; } = string.Empty;
        public bool IsOfficial { get; set; }

        /// <summary>
        /// Get display name for UI
        /// </summary>
        public string DisplayName => $"{Manifest.Plugin.Name} v{Manifest.Plugin.Version}";

        /// <summary>
        /// Get trust level indicator for UI
        /// </summary>
        public string TrustLevelDisplay => IsOfficial ? "Official" :
            Manifest.TrustLevel.Equals("verified", StringComparison.OrdinalIgnoreCase) ? "Verified" : "Unverified";
    }
}
