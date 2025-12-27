using System;
using System.Collections.Generic;
using System.Linq;
using DialogEditor.Services;
using Radoub.Formats.Logging;

namespace DialogEditor.Plugins
{
    /// <summary>
    /// Checks plugin permissions before API calls
    /// </summary>
    public class PermissionChecker
    {
        private readonly PluginManifest _manifest;
        private readonly HashSet<string> _grantedPermissions;

        public PermissionChecker(PluginManifest manifest)
        {
            _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            _grantedPermissions = new HashSet<string>(manifest.Permissions, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Check if plugin has a specific permission
        /// </summary>
        public bool HasPermission(string permission)
        {
            if (string.IsNullOrWhiteSpace(permission))
            {
                return false;
            }

            // Check exact permission
            if (_grantedPermissions.Contains(permission))
            {
                return true;
            }

            // Check wildcard permissions (e.g., "audio.*" grants "audio.play")
            var parts = permission.Split('.');
            if (parts.Length >= 2)
            {
                var wildcardPermission = $"{parts[0]}.*";
                if (_grantedPermissions.Contains(wildcardPermission))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check permission and throw if denied
        /// </summary>
        public void RequirePermission(string permission)
        {
            if (!HasPermission(permission))
            {
                var errorMessage = $"Plugin '{_manifest.Plugin.Id}' does not have permission: {permission}";
                UnifiedLogger.LogPlugin(LogLevel.WARN, errorMessage);
                throw new PermissionDeniedException(permission, _manifest.Plugin.Id);
            }
        }

        /// <summary>
        /// Get all granted permissions
        /// </summary>
        public IReadOnlyList<string> GetGrantedPermissions()
        {
            return _grantedPermissions.ToList();
        }
    }

    /// <summary>
    /// Exception thrown when a plugin tries to use an API without permission
    /// </summary>
    public class PermissionDeniedException : Exception
    {
        public string Permission { get; }
        public string PluginId { get; }

        public PermissionDeniedException(string permission, string pluginId)
            : base($"Permission denied: '{permission}' for plugin '{pluginId}'")
        {
            Permission = permission;
            PluginId = pluginId;
        }

        public PermissionDeniedException(string permission, string pluginId, Exception innerException)
            : base($"Permission denied: '{permission}' for plugin '{pluginId}'", innerException)
        {
            Permission = permission;
            PluginId = pluginId;
        }
    }
}
