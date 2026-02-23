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

    /// <summary>
    /// Configure test mode with isolated settings directory.
    /// MUST be called before first access to Instance.
    /// </summary>
    /// <param name="testDirectory">Temp directory for test settings</param>
    public static void ConfigureForTesting(string testDirectory)
    {
        lock (_lock)
        {
            if (_instance != null)
                throw new InvalidOperationException("ConfigureForTesting must be called before first Instance access");
            _testSettingsDirectory = testDirectory;
        }
    }

    /// <summary>
    /// Reset for testing - allows re-initialization with different settings.
    /// Only for use in test teardown.
    /// </summary>
    public static void ResetForTesting()
    {
        lock (_lock)
        {
            _instance = null;
            _testSettingsDirectory = null;
        }
    }

    private static string? _testSettingsDirectory;

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

    private SettingsService()
    {
        if (_testSettingsDirectory != null)
            SettingsDirectory = _testSettingsDirectory;

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

    protected override void LoadToolSettings(SettingsData settings)
    {
        _leftPanelWidth = Math.Max(200, Math.Min(600, settings.LeftPanelWidth));
        _rightPanelWidth = Math.Max(250, Math.Min(800, settings.RightPanelWidth));
        _sidebarWidth = Math.Max(150, Math.Min(300, settings.SidebarWidth));
        _creatureBrowserPanelWidth = Math.Max(150, Math.Min(400, settings.CreatureBrowserPanelWidth));
        _creatureBrowserPanelVisible = settings.CreatureBrowserPanelVisible;
        _levelHistoryEncoding = settings.LevelHistoryEncoding;
        _recordLevelHistory = settings.RecordLevelHistory;
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
    }
}
