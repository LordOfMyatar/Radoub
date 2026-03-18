using System;
using Radoub.Formats.Logging;
using Radoub.UI.Services;

namespace ItemEditor.Services;

/// <summary>
/// Settings service for ItemEditor.
/// Stores tool-specific settings in ~/Radoub/ItemEditor/ItemEditorSettings.json
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

    protected override string ToolName => "ItemEditor";
    protected override string SettingsEnvironmentVariable => "ITEMEDITOR_SETTINGS_DIR";
    protected override string SettingsFileName => "ItemEditorSettings.json";

    // Panel settings
    private double _browserPanelWidth = 250;

    private SettingsService()
    {
        Initialize();
    }

    public double BrowserPanelWidth
    {
        get => _browserPanelWidth;
        set { if (SetProperty(ref _browserPanelWidth, Math.Max(150, Math.Min(500, value)))) SaveSettings(); }
    }

    protected override void LoadToolSettings(SettingsData settings)
    {
        _browserPanelWidth = Math.Max(150, Math.Min(500, settings.BrowserPanelWidth));
    }

    protected override void SaveToolSettings(SettingsData settings)
    {
        settings.BrowserPanelWidth = BrowserPanelWidth;
    }

    public class SettingsData : BaseSettingsData
    {
        public double BrowserPanelWidth { get; set; } = 250;
    }
}
