using Radoub.UI.Services;

namespace PlaceableEditor.Services;

/// <summary>
/// Settings service for Reliquary.
/// Stores tool-specific settings in ~/Radoub/Reliquary/ReliquarySettings.json
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

    protected override string ToolName => "Reliquary";
    protected override string SettingsEnvironmentVariable => "RELIQUARY_SETTINGS_DIR";
    protected override string SettingsFileName => "ReliquarySettings.json";

    private SettingsService()
    {
        Initialize();
    }

    // BrowserPanelWidth is provided by BaseToolSettingsService (#2356),
    // reading/writing the same "BrowserPanelWidth" JSON key for compatibility.
    // Reliquary has no other tool-specific settings.

    protected override void LoadToolSettings(SettingsData settings) { }

    protected override void SaveToolSettings(SettingsData settings) { }

    public class SettingsData : BaseSettingsData { }
}
