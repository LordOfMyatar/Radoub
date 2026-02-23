using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.UI.Services;

namespace RadoubLauncher.Services;

/// <summary>
/// Settings service for Trebuchet.
/// Stores tool-specific settings in ~/Radoub/Trebuchet/TrebuchetSettings.json
/// Game paths and TLK settings are in shared RadoubSettings.
///
/// Trebuchet has RecentModules (directories/files) instead of RecentFiles,
/// and syncs logging settings to RadoubSettings for cross-tool sharing.
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

    protected override string ToolName => "Trebuchet";
    protected override string SettingsEnvironmentVariable => "TREBUCHET_SETTINGS_DIR";
    protected override string SettingsFileName => "TrebuchetSettings.json";
    protected override double DefaultWindowWidth => 900;
    protected override double DefaultWindowHeight => 600;

    // UI settings
    private double _fontSizeScale = 1.0;

    // Recent modules (Trebuchet tracks modules, not individual files)
    private const int DefaultMaxRecentModules = 10;
    private List<string> _recentModules = new();
    private int _maxRecentModules = DefaultMaxRecentModules;

    // Build settings
    private bool _compileScriptsEnabled;
    private bool _buildUncompiledScriptsEnabled;
    private bool _alwaysSaveBeforeTesting;
    private string _codeEditorPath = "";
    private string _scriptCompilerPath = "";

    private SettingsService()
    {
        if (_testSettingsDirectory != null)
            SettingsDirectory = _testSettingsDirectory;

        Initialize();
    }

    /// <summary>
    /// Sync log retention to RadoubSettings so other tools pick it up.
    /// </summary>
    protected override void OnLoggingRetentionChanged(int sessions)
    {
        RadoubSettings.Instance.SharedLogRetentionSessions = sessions;
    }

    /// <summary>
    /// Sync log level to RadoubSettings so other tools pick it up.
    /// </summary>
    protected override void OnLoggingLevelChanged(LogLevel level)
    {
        RadoubSettings.Instance.SharedLogLevel = level;
    }

    // UI properties
    public double FontSizeScale
    {
        get => _fontSizeScale;
        set { if (SetProperty(ref _fontSizeScale, Math.Max(0.8, Math.Min(1.5, value)))) SaveSettings(); }
    }

    // Recent Modules
    public List<string> RecentModules => _recentModules.ToList();

    public int MaxRecentModules
    {
        get => _maxRecentModules;
        set
        {
            if (SetProperty(ref _maxRecentModules, Math.Max(1, Math.Min(20, value))))
            {
                TrimRecentModules();
                SaveSettings();
            }
        }
    }

    public void AddRecentModule(string modulePath)
    {
        if (string.IsNullOrEmpty(modulePath))
            return;

        if (!File.Exists(modulePath) && !Directory.Exists(modulePath))
            return;

        _recentModules.Remove(modulePath);
        _recentModules.Insert(0, modulePath);
        TrimRecentModules();
        OnPropertyChanged(nameof(RecentModules));
        SaveSettings();
    }

    public void RemoveRecentModule(string modulePath)
    {
        if (_recentModules.Remove(modulePath))
        {
            OnPropertyChanged(nameof(RecentModules));
            SaveSettings();
        }
    }

    public void ClearRecentModules()
    {
        if (_recentModules.Count > 0)
        {
            _recentModules.Clear();
            OnPropertyChanged(nameof(RecentModules));
            SaveSettings();
        }
    }

    private void TrimRecentModules()
    {
        while (_recentModules.Count > MaxRecentModules)
            _recentModules.RemoveAt(_recentModules.Count - 1);
    }

    // Build Settings
    public bool CompileScriptsEnabled
    {
        get => _compileScriptsEnabled;
        set { if (SetProperty(ref _compileScriptsEnabled, value)) SaveSettings(); }
    }

    public bool BuildUncompiledScriptsEnabled
    {
        get => _buildUncompiledScriptsEnabled;
        set { if (SetProperty(ref _buildUncompiledScriptsEnabled, value)) SaveSettings(); }
    }

    public bool AlwaysSaveBeforeTesting
    {
        get => _alwaysSaveBeforeTesting;
        set { if (SetProperty(ref _alwaysSaveBeforeTesting, value)) SaveSettings(); }
    }

    public string CodeEditorPath
    {
        get => _codeEditorPath;
        set { if (SetProperty(ref _codeEditorPath, value ?? "")) SaveSettings(); }
    }

    public string ScriptCompilerPath
    {
        get => _scriptCompilerPath;
        set { if (SetProperty(ref _scriptCompilerPath, value ?? "")) SaveSettings(); }
    }

    protected override void LoadToolSettings(SettingsData settings)
    {
        _fontSizeScale = Math.Max(0.8, Math.Min(1.5, settings.FontSizeScale));

        // Load recent modules
        _recentModules = PathHelper.ExpandPaths(settings.RecentModules ?? new List<string>()).ToList();
        _maxRecentModules = settings.MaxRecentModules > 0 && settings.MaxRecentModules <= 20
            ? settings.MaxRecentModules
            : DefaultMaxRecentModules;

        var removedCount = _recentModules.RemoveAll(m => !File.Exists(m) && !Directory.Exists(m));
        if (removedCount > 0)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Removed {removedCount} missing modules from recent list");
        }

        // Build settings
        _compileScriptsEnabled = settings.CompileScriptsEnabled;
        _buildUncompiledScriptsEnabled = settings.BuildUncompiledScriptsEnabled;
        _alwaysSaveBeforeTesting = settings.AlwaysSaveBeforeTesting;
        _codeEditorPath = settings.CodeEditorPath ?? "";
        _scriptCompilerPath = settings.ScriptCompilerPath ?? "";
    }

    protected override void SaveToolSettings(SettingsData settings)
    {
        settings.FontSizeScale = FontSizeScale;
        settings.RecentModules = PathHelper.ContractPaths(_recentModules).ToList();
        settings.MaxRecentModules = MaxRecentModules;
        settings.CompileScriptsEnabled = CompileScriptsEnabled;
        settings.BuildUncompiledScriptsEnabled = BuildUncompiledScriptsEnabled;
        settings.AlwaysSaveBeforeTesting = AlwaysSaveBeforeTesting;
        settings.CodeEditorPath = CodeEditorPath;
        settings.ScriptCompilerPath = ScriptCompilerPath;
    }

    public class SettingsData : BaseSettingsData
    {
        public double FontSizeScale { get; set; } = 1.0;

        public List<string> RecentModules { get; set; } = new();
        public int MaxRecentModules { get; set; } = DefaultMaxRecentModules;

        public bool CompileScriptsEnabled { get; set; }
        public bool BuildUncompiledScriptsEnabled { get; set; }
        public bool AlwaysSaveBeforeTesting { get; set; }
        public string CodeEditorPath { get; set; } = "";
        public string ScriptCompilerPath { get; set; } = "";
    }
}
