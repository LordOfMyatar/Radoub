using System;
using System.IO;
using Radoub.Formats.Logging;
using Radoub.UI.Services;

namespace ItemEditor.Services;

/// <summary>
/// Settings service for Relique.
/// Stores tool-specific settings in ~/Radoub/Relique/ReliqueSettings.json
/// Game paths and TLK settings are in shared RadoubSettings.
/// </summary>
public class SettingsService : BaseToolSettingsService<SettingsService.SettingsData>
{
    private static SettingsService? _instance;
    private static readonly object _lock = new();

    public static SettingsService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new SettingsService();
                }
            }
            return _instance;
        }
    }

    protected override string ToolName => "Relique";
    protected override string SettingsEnvironmentVariable => "RELIQUE_SETTINGS_DIR";
    protected override string SettingsFileName => "ReliqueSettings.json";

    // Wizard settings
    private bool _openInEditorAfterCreate = true;

    private SettingsService()
    {
        MigrateLegacySettings();
        Initialize();
    }

    /// <summary>
    /// Migrate settings from legacy ~/Radoub/ItemEditor/ to ~/Radoub/Relique/
    /// if the old directory exists and the new one does not yet have a settings file.
    /// </summary>
    private void MigrateLegacySettings()
    {
        try
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var legacyDir = Path.Combine(userProfile, "Radoub", "ItemEditor");
            var legacyFile = Path.Combine(legacyDir, SettingsFileName);

            if (!File.Exists(legacyFile))
                return;

            var newDir = SettingsDirectory;
            var newFile = Path.Combine(newDir, SettingsFileName);

            if (File.Exists(newFile))
                return;

            Directory.CreateDirectory(newDir);
            File.Copy(legacyFile, newFile);
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Migrated settings from ~/Radoub/ItemEditor/ to ~/Radoub/Relique/");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Settings migration from ItemEditor failed: {ex.Message}");
        }
    }

    // BrowserPanelWidth is provided by BaseToolSettingsService (#2356),
    // reading/writing the same "BrowserPanelWidth" JSON key for compatibility.

    public bool OpenInEditorAfterCreate
    {
        get => _openInEditorAfterCreate;
        set { if (SetProperty(ref _openInEditorAfterCreate, value)) SaveSettings(); }
    }

    protected override void LoadToolSettings(SettingsData settings)
    {
        _openInEditorAfterCreate = settings.OpenInEditorAfterCreate;
    }

    protected override void SaveToolSettings(SettingsData settings)
    {
        settings.OpenInEditorAfterCreate = OpenInEditorAfterCreate;
    }

    public class SettingsData : BaseSettingsData
    {
        public bool OpenInEditorAfterCreate { get; set; } = true;
    }
}
