using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using DialogEditor.Services;

namespace DialogEditor.Plugins
{
    /// <summary>
    /// Plugin manifest loaded from plugin.json
    /// </summary>
    public class PluginManifest
    {
        [JsonPropertyName("manifest_version")]
        public string ManifestVersion { get; set; } = "1.0";

        [JsonPropertyName("plugin")]
        public PluginInfo Plugin { get; set; } = new();

        [JsonPropertyName("permissions")]
        public List<string> Permissions { get; set; } = new();

        [JsonPropertyName("entry_point")]
        public string EntryPoint { get; set; } = string.Empty;

        [JsonPropertyName("trust_level")]
        public string TrustLevel { get; set; } = "unverified";

        /// <summary>
        /// Validate the manifest
        /// </summary>
        public ValidationResult Validate()
        {
            var errors = new List<string>();

            // Validate manifest version
            if (string.IsNullOrWhiteSpace(ManifestVersion))
            {
                errors.Add("manifest_version is required");
            }
            else if (ManifestVersion != "1.0")
            {
                errors.Add($"Unsupported manifest_version: {ManifestVersion}");
            }

            // Validate plugin info
            if (Plugin == null)
            {
                errors.Add("plugin section is required");
            }
            else
            {
                errors.AddRange(Plugin.Validate());
            }

            // Validate entry point
            if (string.IsNullOrWhiteSpace(EntryPoint))
            {
                errors.Add("entry_point is required");
            }

            // Validate trust level
            if (!string.IsNullOrWhiteSpace(TrustLevel))
            {
                var validTrustLevels = new[] { "official", "verified", "unverified" };
                if (!validTrustLevels.Contains(TrustLevel.ToLowerInvariant()))
                {
                    errors.Add($"Invalid trust_level: {TrustLevel}. Must be one of: {string.Join(", ", validTrustLevels)}");
                }
            }

            // Validate permissions
            foreach (var permission in Permissions)
            {
                if (!IsValidPermission(permission))
                {
                    errors.Add($"Invalid permission: {permission}");
                }
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            };
        }

        private static bool IsValidPermission(string permission)
        {
            var validPrefixes = new[]
            {
                "audio.",
                "ui.",
                "dialog.",
                "file."
            };

            return validPrefixes.Any(prefix => permission.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Load manifest from JSON file
        /// </summary>
        public static PluginManifest? LoadFromFile(string manifestPath)
        {
            try
            {
                if (!File.Exists(manifestPath))
                {
                    UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Manifest not found: {manifestPath}");
                    return null;
                }

                var json = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<PluginManifest>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });

                if (manifest == null)
                {
                    UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Failed to deserialize manifest: {manifestPath}");
                    return null;
                }

                var validation = manifest.Validate();
                if (!validation.IsValid)
                {
                    UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Invalid manifest {manifestPath}:");
                    foreach (var error in validation.Errors)
                    {
                        UnifiedLogger.LogPlugin(LogLevel.ERROR, $"  - {error}");
                    }
                    return null;
                }

                UnifiedLogger.LogPlugin(LogLevel.INFO, $"Loaded manifest: {manifest.Plugin.Id} v{manifest.Plugin.Version}");
                return manifest;
            }
            catch (JsonException ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"JSON error in manifest {manifestPath}: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Error loading manifest {manifestPath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if plugin version is compatible with Parley version
        /// </summary>
        public bool IsCompatibleWith(string parleyVersion)
        {
            if (string.IsNullOrWhiteSpace(Plugin.ParleyVersion))
            {
                // No version requirement means compatible
                return true;
            }

            try
            {
                return SemanticVersionMatcher.IsCompatible(Plugin.ParleyVersion, parleyVersion);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Error checking version compatibility: {ex.Message}");
                return false;
            }
        }
    }

    public class PluginInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("author")]
        public string Author { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("parley_version")]
        public string ParleyVersion { get; set; } = string.Empty;

        public List<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Id))
            {
                errors.Add("plugin.id is required");
            }

            if (string.IsNullOrWhiteSpace(Name))
            {
                errors.Add("plugin.name is required");
            }

            if (string.IsNullOrWhiteSpace(Version))
            {
                errors.Add("plugin.version is required");
            }
            else if (!SemanticVersionMatcher.IsValidVersion(Version))
            {
                errors.Add($"Invalid plugin.version: {Version}");
            }

            if (string.IsNullOrWhiteSpace(Author))
            {
                errors.Add("plugin.author is required");
            }

            return errors;
        }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// Simple semantic version matching
    /// Supports: >=1.0.0, ^1.0.0, ~1.2.3, 1.0.0
    /// </summary>
    public static class SemanticVersionMatcher
    {
        public static bool IsValidVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return false;
            }

            // Remove any leading operators
            version = version.TrimStart('>', '<', '=', '^', '~');

            var parts = version.Split('.');
            if (parts.Length != 3)
            {
                return false;
            }

            return parts.All(p => int.TryParse(p, out _));
        }

        public static bool IsCompatible(string requirement, string actualVersion)
        {
            requirement = requirement.Trim();

            // Parse operator
            string op = "";
            if (requirement.StartsWith(">="))
            {
                op = ">=";
                requirement = requirement.Substring(2);
            }
            else if (requirement.StartsWith("^"))
            {
                op = "^";
                requirement = requirement.Substring(1);
            }
            else if (requirement.StartsWith("~"))
            {
                op = "~";
                requirement = requirement.Substring(1);
            }
            else
            {
                op = "=";
            }

            var reqParts = requirement.Split('.').Select(int.Parse).ToArray();
            var actualParts = actualVersion.Split('.').Select(int.Parse).ToArray();

            if (reqParts.Length != 3 || actualParts.Length != 3)
            {
                return false;
            }

            return op switch
            {
                ">=" => CompareVersions(actualParts, reqParts) >= 0,
                "^" => CompatibleWithCaret(reqParts, actualParts),
                "~" => CompatibleWithTilde(reqParts, actualParts),
                "=" => CompareVersions(actualParts, reqParts) == 0,
                _ => false
            };
        }

        private static int CompareVersions(int[] v1, int[] v2)
        {
            for (int i = 0; i < 3; i++)
            {
                if (v1[i] > v2[i]) return 1;
                if (v1[i] < v2[i]) return -1;
            }
            return 0;
        }

        private static bool CompatibleWithCaret(int[] req, int[] actual)
        {
            // ^1.2.3 allows >=1.2.3 <2.0.0
            if (actual[0] != req[0]) return false;
            return CompareVersions(actual, req) >= 0;
        }

        private static bool CompatibleWithTilde(int[] req, int[] actual)
        {
            // ~1.2.3 allows >=1.2.3 <1.3.0
            if (actual[0] != req[0] || actual[1] != req[1]) return false;
            return actual[2] >= req[2];
        }
    }
}
