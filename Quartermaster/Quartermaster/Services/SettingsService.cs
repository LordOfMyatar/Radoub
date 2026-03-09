using System;
using Radoub.Formats.Logging;
using Radoub.UI.Services;

namespace Quartermaster.Services;

/// <summary>
/// Settings service for Quartermaster.
/// Stores tool-specific settings in ~/Radoub/Quartermaster/QuartermasterSettings.json
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

    protected override string ToolName => "Quartermaster";
    protected override string SettingsEnvironmentVariable => "QUARTERMASTER_SETTINGS_DIR";
    protected override string SettingsFileName => "QuartermasterSettings.json";

    // Panel settings
    private double _leftPanelWidth = 350;
    private double _rightPanelWidth = 400;
    private double _sidebarWidth = 200;

    // Creature browser panel settings (#1145)
    private double _creatureBrowserPanelWidth = 200;
    private bool _creatureBrowserPanelVisible = true;

    // Level history settings
    private LevelHistoryEncoding _levelHistoryEncoding = LevelHistoryEncoding.Readable;
    private bool _recordLevelHistory = true;

    // Validation level for wizards (NCW/LUW)
    private ValidationLevel _validationLevel = ValidationLevel.Warning;

    private SettingsService()
    {
        Initialize();
    }

    // Panel properties
    public double LeftPanelWidth
    {
        get => _leftPanelWidth;
        set { if (SetProperty(ref _leftPanelWidth, Math.Max(200, Math.Min(600, value)))) SaveSettings(); }
    }

    public double RightPanelWidth
    {
        get => _rightPanelWidth;
        set { if (SetProperty(ref _rightPanelWidth, Math.Max(250, Math.Min(800, value)))) SaveSettings(); }
    }

    public double SidebarWidth
    {
        get => _sidebarWidth;
        set { if (SetProperty(ref _sidebarWidth, Math.Max(150, Math.Min(300, value)))) SaveSettings(); }
    }

    // Creature browser panel properties (#1145)
    public double CreatureBrowserPanelWidth
    {
        get => _creatureBrowserPanelWidth;
        set { if (SetProperty(ref _creatureBrowserPanelWidth, Math.Max(150, Math.Min(400, value)))) SaveSettings(); }
    }

    public bool CreatureBrowserPanelVisible
    {
        get => _creatureBrowserPanelVisible;
        set { if (SetProperty(ref _creatureBrowserPanelVisible, value)) SaveSettings(); }
    }

    // Level history properties
    public LevelHistoryEncoding LevelHistoryEncoding
    {
        get => _levelHistoryEncoding;
        set { if (SetProperty(ref _levelHistoryEncoding, value)) SaveSettings(); }
    }

    public bool RecordLevelHistory
    {
        get => _recordLevelHistory;
        set { if (SetProperty(ref _recordLevelHistory, value)) SaveSettings(); }
    }

    /// <summary>
    /// Validation strictness for NCW and LUW character creation rules.
    /// Default: Warning (True Neutral) — shows warnings but allows rule violations.
    /// </summary>
    public ValidationLevel ValidationLevel
    {
        get => _validationLevel;
        set { if (SetProperty(ref _validationLevel, value)) SaveSettings(); }
    }

    protected override void LoadToolSettings(SettingsData settings)
    {
        _leftPanelWidth = Math.Max(200, Math.Min(600, settings.LeftPanelWidth));
        _rightPanelWidth = Math.Max(250, Math.Min(800, settings.RightPanelWidth));
        _sidebarWidth = Math.Max(150, Math.Min(300, settings.SidebarWidth));
        _creatureBrowserPanelWidth = Math.Max(150, Math.Min(400, settings.CreatureBrowserPanelWidth));
        _creatureBrowserPanelVisible = settings.CreatureBrowserPanelVisible;
        _levelHistoryEncoding = settings.LevelHistoryEncoding;
        _recordLevelHistory = settings.RecordLevelHistory;
        _validationLevel = settings.ValidationLevel;
    }

    protected override void SaveToolSettings(SettingsData settings)
    {
        settings.LeftPanelWidth = LeftPanelWidth;
        settings.RightPanelWidth = RightPanelWidth;
        settings.SidebarWidth = SidebarWidth;
        settings.CreatureBrowserPanelWidth = CreatureBrowserPanelWidth;
        settings.CreatureBrowserPanelVisible = CreatureBrowserPanelVisible;
        settings.LevelHistoryEncoding = LevelHistoryEncoding;
        settings.RecordLevelHistory = RecordLevelHistory;
        settings.ValidationLevel = ValidationLevel;
    }

    public class SettingsData : BaseSettingsData
    {
        public double LeftPanelWidth { get; set; } = 350;
        public double RightPanelWidth { get; set; } = 400;
        public double SidebarWidth { get; set; } = 200;

        // Creature browser panel (#1145)
        public double CreatureBrowserPanelWidth { get; set; } = 200;
        public bool CreatureBrowserPanelVisible { get; set; } = true;

        public LevelHistoryEncoding LevelHistoryEncoding { get; set; } = LevelHistoryEncoding.Readable;
        public bool RecordLevelHistory { get; set; } = true;
        public ValidationLevel ValidationLevel { get; set; } = ValidationLevel.Warning;
    }
}
