using System.Text.Json;
using System.Text.Json.Serialization;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;

namespace Radoub.Formats.Settings;

/// <summary>
/// Persistence logic for RadoubSettings: Load, Save, AutoDetect, and SettingsData DTO.
/// </summary>
public partial class RadoubSettings
{
    private void AutoDetectPaths()
    {
        // Try to detect base game installation
        var basePath = ResourcePathDetector.AutoDetectBaseGamePath();
        if (!string.IsNullOrEmpty(basePath))
        {
            _baseGameInstallPath = basePath;
        }

        // Try to detect user documents path
        var docsPath = ResourcePathDetector.AutoDetectGamePath();
        if (!string.IsNullOrEmpty(docsPath))
        {
            _neverwinterNightsPath = docsPath;

            // Try to find modules folder
            var modulePath = ResourcePathDetector.AutoDetectModulePath(docsPath);
            if (!string.IsNullOrEmpty(modulePath))
            {
                _currentModulePath = modulePath;
            }
        }

        if (HasGamePaths)
        {
            SaveSettings();
        }
    }

    private void LoadSettings()
    {
        try
        {
            if (!Directory.Exists(SettingsDirectory))
            {
                Directory.CreateDirectory(SettingsDirectory);
            }

            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var data = JsonSerializer.Deserialize<SettingsData>(json);

                if (data != null)
                {
                    _baseGameInstallPath = PathHelper.ExpandPath(data.BaseGameInstallPath ?? "");
                    _neverwinterNightsPath = PathHelper.ExpandPath(data.NeverwinterNightsPath ?? "");
                    _currentModulePath = PathHelper.ExpandPath(data.CurrentModulePath ?? "");
                    _tlkLanguage = data.TlkLanguage ?? "";
                    _tlkUseFemale = data.TlkUseFemale;
                    _defaultLanguage = data.DefaultLanguage;

                    // Custom content paths
                    _customTlkPath = PathHelper.ExpandPath(data.CustomTlkPath ?? "");
                    _hakSearchPaths = (data.HakSearchPaths ?? new List<string>())
                        .Select(PathHelper.ExpandPath)
                        .Where(p => !string.IsNullOrEmpty(p))
                        .ToList();

                    // Theme and font settings
                    _sharedThemeId = data.SharedThemeId ?? "";
                    if (string.IsNullOrEmpty(_sharedThemeId))
                        _sharedThemeId = "org.radoub.theme.light";
                    _sharedFontSize = Math.Max(8, Math.Min(32, data.SharedFontSize));
                    _sharedFontFamily = data.SharedFontFamily ?? "Segoe UI";

                    // Logging settings
                    _sharedLogLevel = data.SharedLogLevel;
                    _sharedLogRetentionSessions = Math.Max(1, Math.Min(10, data.SharedLogRetentionSessions));
                    _useSharedLogging = data.UseSharedLogging;

                    // Backup settings
                    _backupRetentionDays = Math.Max(1, Math.Min(90, data.BackupRetentionDays));

                    // Garbage filters
                    if (data.GarbageFilters != null && data.GarbageFilters.Count > 0)
                        _garbageFilters = data.GarbageFilters.Where(f => !string.IsNullOrWhiteSpace(f)).ToList();
                    // else: keep defaults from field initializer

                    // Tool paths
                    _parleyPath = PathHelper.ExpandPath(data.ParleyPath ?? "");
                    _manifestPath = PathHelper.ExpandPath(data.ManifestPath ?? "");
                    _quartermasterPath = PathHelper.ExpandPath(data.QuartermasterPath ?? "");
                    _fencePath = PathHelper.ExpandPath(data.FencePath ?? "");
                    _trebuchetPath = PathHelper.ExpandPath(data.TrebuchetPath ?? "");
                    _reliquePath = PathHelper.ExpandPath(data.ReliquePath ?? "");
                }
            }
        }
        catch (Exception ex)
        {
            // Use defaults on error, but log so failures aren't invisible (#1384)
            UnifiedLogger.Log(LogLevel.WARN,
                $"Failed to load RadoubSettings: {ex.Message}", "RadoubSettings", "Settings");
        }
    }

    private void SaveSettings()
    {
        try
        {
            if (!Directory.Exists(SettingsDirectory))
            {
                Directory.CreateDirectory(SettingsDirectory);
            }

            var data = new SettingsData
            {
                BaseGameInstallPath = PathHelper.ContractPath(_baseGameInstallPath),
                NeverwinterNightsPath = PathHelper.ContractPath(_neverwinterNightsPath),
                CurrentModulePath = PathHelper.ContractPath(_currentModulePath),
                TlkLanguage = _tlkLanguage,
                TlkUseFemale = _tlkUseFemale,
                DefaultLanguage = _defaultLanguage,

                // Custom content paths
                CustomTlkPath = PathHelper.ContractPath(_customTlkPath),
                HakSearchPaths = _hakSearchPaths.Select(PathHelper.ContractPath).ToList(),

                // Theme and font settings
                SharedThemeId = _sharedThemeId,
                SharedFontSize = _sharedFontSize,
                SharedFontFamily = _sharedFontFamily,

                // Logging settings
                SharedLogLevel = _sharedLogLevel,
                SharedLogRetentionSessions = _sharedLogRetentionSessions,
                UseSharedLogging = _useSharedLogging,

                // Tool paths
                ParleyPath = PathHelper.ContractPath(_parleyPath),
                ManifestPath = PathHelper.ContractPath(_manifestPath),
                QuartermasterPath = PathHelper.ContractPath(_quartermasterPath),
                FencePath = PathHelper.ContractPath(_fencePath),
                TrebuchetPath = PathHelper.ContractPath(_trebuchetPath),
                ReliquePath = PathHelper.ContractPath(_reliquePath),

                // Backup settings
                BackupRetentionDays = _backupRetentionDays,

                // Garbage filters
                GarbageFilters = _garbageFilters,
            };

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            using var fs = new FileStream(SettingsFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(fs);
            writer.Write(json);
            writer.Flush();
            fs.Flush(flushToDisk: true);
        }
        catch (Exception ex)
        {
            // Log save errors so failures aren't invisible (#1384)
            UnifiedLogger.Log(LogLevel.WARN,
                $"Failed to save RadoubSettings: {ex.Message}", "RadoubSettings", "Settings");
        }
    }

    private class SettingsData
    {
        public string? BaseGameInstallPath { get; set; }
        public string? NeverwinterNightsPath { get; set; }
        public string? CurrentModulePath { get; set; }
        public string? TlkLanguage { get; set; }
        public bool TlkUseFemale { get; set; }
        public Language DefaultLanguage { get; set; } = Language.English;

        // Custom content paths
        public string? CustomTlkPath { get; set; }
        public List<string>? HakSearchPaths { get; set; }

        // Theme and font settings (shared across all tools — Trebuchet is sole authority)
        public string? SharedThemeId { get; set; }
        public double SharedFontSize { get; set; } = 14.0;
        public string? SharedFontFamily { get; set; }

        // Legacy: UseSharedTheme is no longer used (themes always shared via Trebuchet).
        // Kept as write-ignore read-accept so old JSON files deserialize without error.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool UseSharedTheme { get; set; }

        // Logging settings (shared across all tools)
        public LogLevel SharedLogLevel { get; set; } = LogLevel.INFO;
        public int SharedLogRetentionSessions { get; set; } = 3;
        public bool UseSharedLogging { get; set; } = true;

        // Backup settings (shared across all tools)
        public int BackupRetentionDays { get; set; } = 30;

        // Garbage label filters (shared across all tools)
        public List<string>? GarbageFilters { get; set; }

        // Tool paths for cross-tool discovery
        public string? ParleyPath { get; set; }
        public string? ManifestPath { get; set; }
        public string? QuartermasterPath { get; set; }
        public string? FencePath { get; set; }
        public string? TrebuchetPath { get; set; }
        public string? ReliquePath { get; set; }

        /// <summary>
        /// Legacy key — reads old "ItemEditorPath" from JSON and migrates to ReliquePath.
        /// Never serialized (getter returns null, WhenWritingNull suppresses output).
        /// </summary>
        [JsonPropertyName("ItemEditorPath")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LegacyItemEditorPath
        {
            get => null;
            set
            {
                if (value != null && ReliquePath == null)
                    ReliquePath = value;
            }
        }
    }
}
